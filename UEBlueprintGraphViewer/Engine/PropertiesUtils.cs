using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;
using System;
using System.Collections.Generic;
using System.Linq;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.Utils;
using UEBlueprintGraphViewer.Assets;
using UEBlueprintGraphViewer.Decompiler;

namespace UEBlueprintGraphViewer.Engine
{
    public static class PropertiesUtils
    {
        // Convert variable expression to property
        public static PropertyData VarInstrToProperty(KismetExpression instr, GlobalDecompilerContext global)
        {
            if (instr is not EX_VariableBase variable)
                throw new DecompilerException($"VarInstrToProperty: Instruction is not EX_VariableBase, but is {instr.GetType()}");

            if (instr is EX_LocalVariable or EX_LocalOutVariable)
                return LocalVarKismetPointerToProperty(variable.Variable, global);

            return KismetPointerToProperty(variable.Variable, global);
        }

        public static PropertyData LocalVarKismetPointerToProperty(FKismetPropertyPointer pointer, GlobalDecompilerContext global)
        {
            string name = ToName(pointer);
            if (global.FunctionLocals.Find(o => o.Name == name) is { } data)
                return data;

            if (GetStructProperties(global.CurrentFunction).FirstOrDefault(o => o.Name == name) is { } prop)
            {
                global.FunctionLocals.Add(prop);
                return prop;
            }

            throw new DecompilerException($"Failed to find property local var {name}");
        }
        
        

        public static List<PropertyData> GetStructProperties(UStruct str)
        {
            return [.. str?.ChildProperties != null
                ? str!.ChildProperties.OfType<FProperty>().Select(o => new PropertyData(o, str))
                : str!.Children.Select(o => o.Load<UProperty>()).OfType<UProperty>().Select(o => new PropertyData(o, str))];
        }
        
        // Get property data from kismet pointer using caching
        public static PropertyData KismetPointerToProperty(FKismetPropertyPointer pointer, GlobalDecompilerContext global)
        {
            string name = ToName(pointer);
            // trying to find property in cache
            if (pointer.bNew)
            {
                FPackageIndex? ownerObj = pointer.New!.ResolvedOwner;
                if (ownerObj != null)
                {
                    // if the property is from this object, try to find it using preloaded class
                    if (ownerObj.IsExport)
                    {
                        if (global.CurrentAsset.LoadedProperties.GetValueOrDefault(name) is { } prop1)
                            return prop1;

                        if (global.CurrentAsset.GeneratedClass.ChildProperties.FirstOrDefault(o => o.Name.Text.EqualsFName(name)) is FProperty prop2)
                        {
                            var newPropData = new PropertyData(prop2, global.CurrentAsset.GeneratedClass);
                            global.CurrentAsset.LoadedProperties.Add(newPropData.Name, newPropData);
                            return newPropData;
                        }
                    }

                    if (ownerObj.ResolvedObject?.GetPathName().Starts("/Script/") == true && global.Game.Jmap.TryFindProperty(name, ownerObj, out PropertyData? prop))
                        return prop!;
                }
            }
            else
            {
                var outer = pointer.Old!.ResolvedObject?.Outer?.GetPathName();
                if (outer?.Starts("/Script/") == true && global.Game.Jmap.TryFindProperty(name, outer, out PropertyData? prop))
                    return prop!;
            }
            
            PropertyData newProp = ToProperty(pointer);
            return newProp;
        }

        public static PropertyData KismetPointerToPropertyUnknownType(FKismetPropertyPointer pointer, GameSettings game)
        {
            // trying to find property in cache
            FPackageIndex? ownerObj = pointer.New != null ? pointer.New!.ResolvedOwner : pointer.Old;
            ResolvedObject? ownerResolved = ownerObj?.ResolvedObject;
            if (ownerResolved != null)
            {
                // assume that after : is a function name, otherwise it is regular object
                if (ownerObj?.ResolvedObject?.GetPathName().SubstringAfterLast(":") == ownerObj?.Name)
                {
                    string funcName = ownerResolved.Name.ToString();
                    List<PropertyData> props = [];
                    string? pathName = ownerResolved.Outer?.GetPathName();
                    if (pathName?.Starts("/Script/") == true && game.Jmap.GetFunctionData(pathName, funcName) is {} functionData)
                    {
                        props = functionData.Params;
                    }
                    else
                    {
                        props = GetStructProperties(ownerResolved.Load() as UFunction);
                    }

                    string name = ToName(pointer);
                    PropertyData? localVar = props.Find(o => o.Name.EqualsFName(name));
                    localVar.Name = name;
                    return localVar;
                }
                else
                {
                    if (game.Jmap.TryFindProperty(ToName(pointer), ownerObj, out PropertyData? prop))
                    {
                        return prop!;
                    }
                    PropertyData newProp = ToProperty(pointer);
                    return newProp;
                }

            }

            throw new DecompilerException($"KismetPointerToPropertyUnknownType: failed to convert {pointer.New!.ResolvedOwner.ResolvedObject.GetPathName()}");
        }

        // Get variable name from expression
        public static string VarInstrToName(KismetExpression instr)
        {
            if (instr is not EX_VariableBase variable)
                throw new DecompilerException($"VarInstrToProperty: Instruction is not EX_VariableBase, but is {instr.GetType()}");

            return ToName(variable.Variable);
        }

        static PropertyData ToProperty(FKismetPropertyPointer value)
        {
            if (value.bNew)
            {
                if (GetPropNew(value.New!, out UField? owner) is FProperty property)
                {
                    return new PropertyData(property, owner!);
                }
            }
            else
            {
                if (value.Old!.TryLoad(out UProperty? property) && property!.Outer?.Object?.Value is UField owner)
                {
                    return new PropertyData(property, owner);
                }
            }
            throw new DecompilerException("Failed to convert kismet property pointer to property");
        }

        public static string ToName(FKismetPropertyPointer value)
        {
            if (value.bNew)
            {
                return value.New!.Path.LastOrDefault().ToString();
            }
            else
            {
                return value.Old!.Name;
            }
        }

        static FField? GetPropNew(FFieldPath fFieldPath, out UField? field)
        {
            if (fFieldPath.ResolvedOwner is null ||
                fFieldPath.ResolvedOwner.IsNull ||
                !fFieldPath.ResolvedOwner.TryLoad<UField>(out field))
            {
                field = null;
                return null;
            }

            switch (field)
            {
                case UScriptClass:
                    // cannot properly get property of UScriptClass
                    throw new DecompilerException($"UScriptClass property not found in dump:\nUScriptClass: {field.GetPathName()}\nProperty: {fFieldPath.Path[0]}");
                case UStruct struc when fFieldPath.Path.Length > 0 && struc.GetProperty(fFieldPath.Path[0], out var prop):
                    return prop;
            }
            return null;
        }
    }
}
