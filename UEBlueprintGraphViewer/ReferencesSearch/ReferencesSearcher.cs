using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.FileProvider.Vfs;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.IO.Objects;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.Utils;
using UEBlueprintGraphViewer.Assets;
using UEBlueprintGraphViewer.Decompiler;
using UEBlueprintGraphViewer.Engine;
using UEBlueprintGraphViewer.Nodes;

namespace UEBlueprintGraphViewer.ReferencesSearch;

public class ReferencesSearcher
{
    private static ParallelOptions _parallelOptions = new() { MaxDegreeOfParallelism = Environment.ProcessorCount };
    
    public delegate void UpdateProgress(int count, int countMax);
    
    public static async Task<GameFile[]> FindAssetReference(PackageData package, GameFile file, UpdateProgress? update)
    {
        var toFind = await package.LoadPackage(file.Path);
        bool isIoStore = false;
        BlockingCollection<GameFile> result = [];
        if (toFind is IoPackage io && io.Provider is AbstractVfsFileProvider provider)
        {
            isIoStore = true;
            foreach (var r in provider.ScanForPackageRefs(file))
                result.Add(r);
        }

        List<GameFile> allAssets = package.Assets.Where(o => (o.Extension == "uasset" || o.Extension == "umap")).ToList();
        allAssets.RemoveAll(o => o.Path == file.Path);
        int counter = 0;
        
        string pathToFind = AssetsUtils.FixAssetPath(file.PathWithoutExtension);
        
        await Parallel.ForEachAsync(allAssets, _parallelOptions, async (asset, token) =>
        {
            update?.Invoke(counter, allAssets.Count);
            counter++;
            
            try
            {
                string[]? names = await GetNameMap(package, isIoStore, asset);
                if (names != null && names.Any(o => o.EqualsFName(pathToFind)))
                    result.Add(asset);
            }
            catch (Exception ex)
            {
                //errors.Add(ex.Message + "\n" + ex.StackTrace);
            }
        });
        
        return result.ToArray();
    }

    private static async Task<string[]?> GetNameMap(PackageData package, bool isIoStore, GameFile asset)
    {
        if (isIoStore)
        {
            if (await package.LoadPackage(asset.Path) is IoPackage io)
            {
                return io.NameMap.Select(o => o.Name).OfType<string>().ToArray();
            }
        }
        else
        {
            if (await package.LoadImportsOnlyPackageAsync(asset) is ImportsOnlyPackage p)
            {
                HashSet<string> names = p.ImportMap.Select(o => o.ClassPackage.ToString()).ToHashSet();
                foreach (string name in p.NameMap.Select(o => o.Name).OfType<string>())
                    names.Add(name);
                return [.. names];
            }
        }

        return null;
    }
    
    public static async Task<GameFile[]> FindUnreferencedAssets(PackageData package, UpdateProgress? update)
    {
        GameFile[] allAssets = package.Assets.Where(o => (o.Extension == "uasset" || o.Extension == "umap")).ToArray();
        int counter = 0;
        
        BlockingCollection<GameFile> result = [];
        
        ConcurrentDictionary<string, byte> refs = [];
        
        await Parallel.ForEachAsync(allAssets, _parallelOptions, async (asset, token) =>
        {
            update?.Invoke(counter, allAssets.Length);
            counter++;
            
            try
            {
                string[]? names = await GetNameMap(package, asset is FIoStoreEntry, asset);
                if (names != null)
                {
                    string fixedAssetPath = AssetsUtils.FixAssetPath(asset.PathWithoutExtension);
                    foreach (var i in names.Where(o =>
                                 o != null &&
                                 (o.Starts("/Game/") || o.Starts("/Engine/")) &&
                                 !o.EqualsFName(fixedAssetPath)))
                    {
                        refs[i!] = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                //errors.Add(ex.Message + "\n" + ex.StackTrace);
            }
        });

        foreach (var asset in allAssets)
        {
            bool ioHaveRefs = false;
            if (asset is FIoStoreEntry)
                ioHaveRefs = package.Provider.ScanForPackageRefs(asset).Count > 0;
            
            if (!refs.ContainsKey(AssetsUtils.FixAssetPath(asset.PathWithoutExtension)) && !ioHaveRefs)
                result.Add(asset);
        }
        
        return result.ToArray();
    }
    
    public static async Task<(GameFile, string, int)[]> FindFunctionReferences(PackageData package, GameSettings game, Asset asset, string funcName, UpdateProgress? update)
    {
        BlockingCollection<(GameFile, string, int)> result = [];
        await IterateInstructions(package, game, update, (file, func, graph) =>
        {
            foreach (var node in graph.Nodes)
            {
                if (node is K2Node_CallFunction call && call.OuterName.SubstringBeforeLast('.') == asset.Name && call.FunctionName == funcName)
                    result.Add((file, func, call.StatementIndex));
            }
        });
        return result.ToArray();
    }
    
    public static async Task<(GameFile, string, int)[]> FindPropertyReferences(PackageData package, GameSettings game, Asset asset, string propName, UpdateProgress? update)
    {
        BlockingCollection<(GameFile, string, int)> result = [];
        await IterateInstructions(package, game, update, (file, func, graph) =>
        {
            foreach (var node in graph.Nodes)
            {
                if ((node is K2Node_VariableGet getter && getter.Property.Owner.SubstringBeforeLast('.') == asset.Name && getter.Property.Name == propName) ||
                    (node is K2Node_VariableSet setter && setter.Property.Owner.SubstringBeforeLast('.') == asset.Name && setter.Property.Name == propName))
                    result.Add((file, func, node.StatementIndex));
            }
        });
        return result.ToArray();
    }

    private static async Task IterateInstructions(PackageData package, GameSettings game, UpdateProgress? update, Action<GameFile, string, BPGraph> action)
    {
        GameFile[] allAssets = package.Assets.Where(o => (o.Extension == "uasset" || o.Extension == "umap")).ToArray();
        int counter = 0;
        
        
        await Parallel.ForEachAsync(allAssets, _parallelOptions, async (file, token) =>
        {
            if (counter % 20 == 0)
                update?.Invoke(counter, allAssets.Length);
            counter++;
            
            try
            {
                var asset = await package.LoadAsset(file.Path, file.NameWithoutExtension);
            
                foreach (var func in asset.Functions)
                {
                    var d = await DecompileFunc(asset, game, func);
                    action.Invoke(file, func.Name, d.Graph);
                }
            
                if (asset.SortedEvents.Count > 0)
                {
                    var d = await DecompileFunc(asset, game, asset.SortedEvents[0]);
                    action.Invoke(file, asset.UbergraphFunction.Name, d.Graph);
                }
            }
            catch (Exception ex)
            {
                //errors.Add(ex.Message + "\n" + ex.StackTrace);
            }
            
            static Task<FunctionDecompiler> DecompileFunc(Asset asset, GameSettings game, UFunction func)
            {
                return Task.Run(() =>
                {
                    var decompiler = new FunctionDecompiler(asset, game, func);
                    decompiler.GlobalContext.IsParsingMacros = false;
                    decompiler.Decompile(null);
                    return decompiler;
                });
            }
        });
    }
}