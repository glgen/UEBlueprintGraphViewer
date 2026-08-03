using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;
using System;
using System.Collections.Generic;
using System.Linq;
using CUE4Parse.UE4.Assets.Exports;
using UEBlueprintGraphViewer.Decompiler;

namespace UEBlueprintGraphViewer.Engine
{
    public static class PropertiesUtils
    {
        // Convert variable expression to property
        public static PropertyData VarInstrToProperty(KismetExpression Instr, GlobalDecompilerContext global)
        {
            if (Instr is not EX_VariableBase Variable)
                throw new DecompilerException($"VarInstrToProperty: Instruction is not EX_VariableBase, but is {Instr.GetType()}");

            if (Instr is EX_LocalVariable or EX_LocalOutVariable)
                return LocalVarKismetPointerToProperty(Variable.Variable, global);

            return KismetPointerToProperty(Variable.Variable, global);
        }

        public static PropertyData LocalVarKismetPointerToProperty(FKismetPropertyPointer pointer, GlobalDecompilerContext global)
        {
            string name = ToName(pointer);
            if (global.FunctionLocals.Find(o => o.Name == name) is { } data)
                return data;

            if (global.CurrentFunction.ChildProperties.FirstOrDefault(o => o.Name.Text == name) is FProperty prop)
            {
                var newData = new PropertyData(prop, global.CurrentFunction);
                global.FunctionLocals.Add(newData);
                return newData;
            }

            throw new DecompilerException($"Failed to find property local var {name}");
        }

        public static PropertyData ResolvedObjectToFunctionProperty(GameSettings game, string name, ResolvedObject ownerResolved)
        {
            string objName = ownerResolved.Outer.Name.ToString();
            string funcName = ownerResolved.Name.ToString();
            List<PropertyData> props = ResolvedObjectToFuncProps(game, ownerResolved, funcName);
            
            PropertyData? localVar = props.Find(o => o.Name.EqualsFName(name));
            if (localVar == null)
                throw new DecompilerException($"Failed to find local var {name} in {objName}.{funcName}");
            localVar.Name = name;
            return localVar;
        }

        public static List<PropertyData> ResolvedObjectToFuncProps(GameSettings game, ResolvedObject ownerResolved, string funcName)
        {
            List<PropertyData> props;
            string pathName = ownerResolved.Outer.GetPathName();
            if (pathName.Starts("/Script/") && game.Jmap.GetFunctionData(pathName, funcName) is {} functionData)
            {
                props = functionData.Params;
            }
            else
            {
                UObject? outer = ownerResolved.Outer.Load();
                props = GetUFunctionProperties(ownerResolved.Load() as UFunction, outer);
            }

            return props;
        }

        public static List<PropertyData> GetUFunctionProperties(UFunction function, UObject outer)
        {
            return function.ChildProperties.Select(o => new PropertyData(o as FProperty, outer)).ToList();
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
            
            PropertyData newProp = ToProperty(pointer);
            return newProp;
        }

        public static PropertyData KismetPointerToPropertyUnknownType(FKismetPropertyPointer pointer, GameSettings game)
        {
            // trying to find property in cache
            if (pointer.bNew)
            {
                FPackageIndex? ownerObj = pointer.New!.ResolvedOwner;
                ResolvedObject? ownerResolved = ownerObj?.ResolvedObject;
                if (ownerResolved != null)
                {

                    if (ownerResolved.Class.Name.Text == "Function")
                    {
                        return ResolvedObjectToFunctionProperty(game, ToName(pointer), ownerResolved);
                    }
                    else
                    {
                        if (game.Jmap.TryFindProperty(ToName(pointer), ownerObj, out PropertyData? prop))
                        {
                            return prop!;
                        }
                        PropertyData newProp = ToProperty(pointer);
                        //game.ParamsDump.AddProperty(newProp);
                        return newProp;
                    }

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
                    return new PropertyData(property, owner);
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
                throw new NotImplementedException();
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
