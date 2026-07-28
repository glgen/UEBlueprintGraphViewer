using CUE4Parse.FileProvider.Objects;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Objects.UObject;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UEBlueprintGraphViewer.Assets;
using UEBlueprintGraphViewer.Decompiler;

namespace UEBlueprintGraphViewer.Comparing
{
    internal class AssetsComparer
    {
        public delegate void UpdateProgress(int count, int countMax);

        private const string assetsCacheFile = "compareAssetsCache.txt";


        public async static Task<ComparisonResult> CompareAssets(GameSettings game1, GameSettings game2, PackageData package1, PackageData package2, UpdateProgress? update)
        {
            var assetsPaths1 = package1.Assets.Select(x => x.Path).ToHashSet();
            var assetsPaths2 = package2.Assets.Select(x => x.Path).ToHashSet();

            List<string> newAssets = assetsPaths2.Where(o => !assetsPaths1.Contains(o)).ToList();
            List<string> removedAssets = assetsPaths1.Where(o => !assetsPaths2.Contains(o)).ToList();
            var removedAssetsHash = removedAssets.ToHashSet();
            List<string> allAssets = [.. assetsPaths1, .. newAssets];
            List<GameFile> notNewNotRemoved = package1.Assets.Where(o => (o.Extension == "uasset" || o.Extension == "umap") && !removedAssetsHash.Contains(o.Path)).ToList();

            removedAssetsHash.Clear();
            assetsPaths1.Clear();
            assetsPaths2.Clear();

            BlockingCollection<string> modified = [];

            BlockingCollection<string> decompErrors = [];
            BlockingCollection<string> errors = [];

            int counter = 0;

            ParallelOptions parallelOptions = new() { MaxDegreeOfParallelism = 5 };

            await Parallel.ForEachAsync(notNewNotRemoved, parallelOptions, async (asset, token) =>
            {
                update?.Invoke(counter, notNewNotRemoved.Count);
                counter++;
                
                try
                {
                    var task1 = package1.LoadAsset(asset.Path, asset.NameWithoutExtension);
                    var task2 = package2.LoadAsset(asset.Path, asset.NameWithoutExtension);
                    var result = await Task.WhenAll(task1, task2);

                    if (!result[0].IsBP || !result[1].IsBP)
                        return;

                    if (await CheckTwoAssets(result[0], result[1], game1, game2))
                        modified.Add(asset.Path);
                }
                catch (AssetIsNotBlueprintException) { }
                catch (DecompilerException ex)
                {
                    decompErrors.Add(ex.Message);
                }
                catch (Exception ex)
                {
                    errors.Add(ex.Message + "\n" + ex.StackTrace);
                }
            });

            StringBuilder sb = new();
            foreach (var item in newAssets)
                sb.AppendLine($"NEW ASSET: {item}");
            foreach (var item in removedAssets)
                sb.AppendLine($"REMOVED ASSET: {item}");
            foreach (var item in modified)
                sb.AppendLine($"CHANGED: {item}");
            sb.AppendLine($"CHANGED COUNT: {modified.Count}");
            sb.AppendLine($"DECOMPILER ERRORS:");
            sb.AppendLine(string.Join('\n', decompErrors));
            sb.AppendLine($"OTHER EXCEPTIONS:");
            sb.AppendLine(string.Join('\n', errors));

            await File.WriteAllTextAsync("test.txt", sb.ToString());

            return new ComparisonResult()
            {
                NewAssets = newAssets,
                RemovedAssets = removedAssets,
                ModifiedAssets = [.. modified],
                AllAssets = allAssets,
                UnchangedAssets = [.. notNewNotRemoved.Select(o => o.Path).Except(modified)],
            };
        }

        private static async Task<bool> CheckTwoAssets(Asset asset1, Asset asset2, GameSettings game1, GameSettings game2)
        {
            if (asset1.Events.Count != asset2.Events.Count ||
                asset1.Functions.Count != asset2.Functions.Count)
            {
                return true;
            }

            if (CheckFunctionNames(asset1.Events.Keys, asset2.Events.Keys) ||
                CheckFunctionNames(asset1.Functions.Select(o => o.Name), asset2.Functions.Select(o => o.Name)))
                return true;

            if (asset1.UbergraphFunction != null && asset2.UbergraphFunction != null &&
                asset1.UbergraphFunction.ScriptBytecode.Length != asset2.UbergraphFunction.ScriptBytecode.Length)
                return true;

            var funcs1 = asset1.Functions.OrderBy(o => o.Name).ToArray();
            var funcs2 = asset2.Functions.OrderBy(o => o.Name).ToArray();

            var events1 = asset1.Events.OrderBy(o => o.Key).Select(o => o.Value).ToArray();
            var events2 = asset2.Events.OrderBy(o => o.Key).Select(o => o.Value).ToArray();

            for (var i = 0; i < funcs1.Length; i++)
            {
                if (CheckBytecodeFast(funcs1[i], funcs2[i]))
                    return true;
            }

            for (var i = 0; i < events1.Length; i++)
            {
                if (CheckBytecodeFast(events1[i], events2[i]))
                    return true;
            }

            asset1.LoadAllProperties();
            asset2.LoadAllProperties();
            // TODO: maybe also check for property type change?
            if (!asset1.LoadedProperties.Keys.ToHashSet().SetEquals(asset2.LoadedProperties.Keys.ToHashSet()))
                return true;
            
            if (events1.Length > 0 && await CheckDecompilation(asset1, asset2, game1, game2, events1[0], events2[0]))
                return true;

            for (var i = 0; i < funcs1.Length; i++)
            {
                if (await CheckDecompilation(asset1, asset2, game1, game2, funcs1[i], funcs2[i]))
                    return true;
            }

            return false;
        }

        private static bool CheckFunctionNames(IEnumerable<string> funcs1, IEnumerable<string> funcs2)
        {
            var hashset1 = funcs1.ToHashSet();
            var hashset2 = funcs2.ToHashSet();
            return !hashset1.SetEquals(hashset2);
        }

        private static bool CheckBytecodeFast(UFunction func1, UFunction func2)
        {
            if (func1.ScriptBytecode.Length != func2.ScriptBytecode.Length)
                return true;

            var b1 = func1.ScriptBytecode.Select(o => o.Token);
            var b2 = func2.ScriptBytecode.Select(o => o.Token);
            if (!b1.SequenceEqual(b2))
                return true;

            return false;
        }

        private static async Task<bool> CheckDecompilation(Asset asset1, Asset asset2, GameSettings game1, GameSettings game2, UFunction func1, UFunction func2)
        {
            GlobalDecompilerContext context1 = new(asset1, game1, func1) { IsParsingMacros = false };
            GlobalDecompilerContext context2 = new(asset2, game2, func2) { IsParsingMacros = false };
            FunctionDecompiler decompiler1 = new(context1);
            FunctionDecompiler decompiler2 = new(context2);
            await Task.WhenAll(
                decompiler1.DecompileAsync(null),
                decompiler2.DecompileAsync(null));

            return decompiler1.Graph.Nodes.Count != decompiler2.Graph.Nodes.Count ||
                   !BPGraph.IsEquals(decompiler1.Graph, decompiler2.Graph);
        }

        public static void SaveAssetsCache(ComparisonResult result)
        {
            StringBuilder sb = new();
            foreach (var asset in result.UnchangedAssets)
                sb.AppendLine($" {asset}");
            foreach (var asset in result.NewAssets)
                sb.AppendLine($"+{asset}");
            foreach (var asset in result.RemovedAssets)
                sb.AppendLine($"-{asset}");
            foreach (var asset in result.ModifiedAssets)
                sb.AppendLine($"*{asset}");
            File.WriteAllText(assetsCacheFile, sb.ToString());
        }

        public static bool AssetsCacheExists()
        {
            return File.Exists(assetsCacheFile);
        }

        public static void DeleteCache()
        {
            if (File.Exists(assetsCacheFile))
                File.Delete(assetsCacheFile);
        }

        public static ComparisonResult LoadAssetsCache()
        {
            ComparisonResult result = new();
            string[] lines = File.ReadAllLines(assetsCacheFile);

            foreach (var line in lines)
            {
                char prefix = line[0];
                string path = line[1..];
                result.AllAssets.Add(path);
                switch (prefix)
                {
                    case ' ':
                        result.UnchangedAssets.Add(path);
                        break;
                    case '+':
                        result.NewAssets.Add(path);
                        break;
                    case '-':
                        result.RemovedAssets.Add(path);
                        break;
                    case '*':
                        result.ModifiedAssets.Add(path);
                        break;
                }
            }
            return result;
        }
    }

    public class ComparisonResult
    {
        public List<string> NewAssets = [];
        public List<string> RemovedAssets = [];
        public List<string> ModifiedAssets = [];
        public List<string> AllAssets = [];
        public List<string> UnchangedAssets = [];
    }
}
