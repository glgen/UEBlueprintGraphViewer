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
                    decompiler.Decompile(null);
                    return decompiler;
                });
            }
        });
    }

    // C# compiler go into infinite loop with infinite RAM usage without this wrapper function
    private static void KismetWalk(KismetExpression expr, Action<KismetExpression> step)
    {
        KismetWalk2(expr, step);
    }
    private static void KismetWalk2(KismetExpression expr, Action<KismetExpression> step)
    {
        step.Invoke(expr);
        
        switch (expr)
        {
            case EX_AddMulticastDelegate e:
            {
                KismetWalk(e.Delegate, step);
                KismetWalk(e.DelegateToAdd, step);
                break;
            }
            case EX_ArrayConst e:
            {
                foreach (var ex in e.Elements)
                    KismetWalk(ex, step);
                break;
            }
            case EX_ArrayGetByRef e:
            {
                KismetWalk(e.ArrayVariable, step);
                KismetWalk(e.ArrayIndex, step);
                break;
            }
            case EX_Assert e:
            {
                KismetWalk(e.AssertExpression, step);
                break;
            }
            case EX_BindDelegate e:
            {
                KismetWalk(e.Delegate, step);
                KismetWalk(e.ObjectTerm, step);
                break;
            }
            case EX_CallMulticastDelegate e:
            {
                KismetWalk(e.Delegate, step);
                foreach (var param in e.Parameters)
                    KismetWalk(param, step);
                break;
            }
            case EX_Cast e:
            {
                KismetWalk(e.Target, step);
                break;
            }
            case EX_CastBase e:
            {
                KismetWalk(e.Target, step);
                break;
            }
            case EX_ClearMulticastDelegate e:
            {
                KismetWalk(e.DelegateToClear, step);
                break;
            }
            case EX_ComputedJump e:
            {
                KismetWalk(e.CodeOffsetExpression, step);
                break;
            }
            case EX_Context e:
            {
                KismetWalk(e.ObjectExpression, step);
                KismetWalk(e.ContextExpression, step);
                break;
            }
            case EX_FinalFunction e:
            {
                foreach (var param in e.Parameters)
                    KismetWalk(param, step);
                break;
            }
            case EX_InterfaceContext e:
            {
                KismetWalk(e.InterfaceValue, step);
                break;
            }
            case EX_JumpIfNot e:
            {
                KismetWalk(e.BooleanExpression, step);
                break;
            }
            case EX_Let e:
            {
                KismetWalk(e.Variable, step);
                KismetWalk(e.Assignment, step);
                break;
            }
            case EX_LetBase e:
            {
                KismetWalk(e.Variable, step);
                KismetWalk(e.Assignment, step);
                break;
            }
            case EX_LetValueOnPersistentFrame e:
            {
                KismetWalk(e.AssignmentExpression, step);
                break;
            }
            case EX_MapConst e:
            {
                foreach (var elem in e.Elements)
                    KismetWalk(elem, step);
                break;
            }
            case EX_PopExecutionFlowIfNot e:
            {
                KismetWalk(e.BooleanExpression, step);
                break;
            }
            case EX_RemoveMulticastDelegate e:
            {
                KismetWalk(e.Delegate, step);
                KismetWalk(e.DelegateToAdd, step);
                break;
            }
            case EX_Return e:
            {
                KismetWalk(e.ReturnExpression, step);
                break;
            }
            case EX_SetArray e:
            {
                KismetWalk(e.AssigningProperty, step);
                foreach (var elem in e.Elements)
                    KismetWalk(elem, step);
                break;
            }
            case EX_SetConst e:
            {
                foreach (var elem in e.Elements)
                    KismetWalk(elem, step);
                break;
            }
            case EX_SetMap e:
            {
                KismetWalk(e.MapProperty, step);
                foreach (var elem in e.Elements)
                    KismetWalk(elem, step);
                break;
            }
            case EX_SetSet e:
            {
                KismetWalk(e.SetProperty, step);
                foreach (var elem in e.Elements)
                    KismetWalk(elem, step);
                break;
            }
            case EX_Skip e:
            {
                KismetWalk(e.SkipExpression, step);
                break;
            }
            case EX_StructConst e:
            {
                foreach (var prop in e.Properties)
                    KismetWalk(prop, step);
                break;
            }
            case EX_StructMemberContext e:
            {
                KismetWalk(e.StructExpression, step);
                break;
            }
            case EX_SwitchValue e:
            {
                KismetWalk(e.IndexTerm, step);
                KismetWalk(e.DefaultTerm, step);
                foreach (var elem in e.Cases)
                {
                    KismetWalk(elem.CaseIndexValueTerm, step);
                    KismetWalk(elem.CaseTerm, step);
                }

                break;
            }
            case EX_VirtualFunction e:
            {
                foreach (var param in e.Parameters)
                    KismetWalk(param, step);
                break;
            }
            case EX_AutoRtfmTransact e:
            {
                foreach (var param in e.Parameters)
                    KismetWalk(param, step);
                break;
            }
        }
    }
}