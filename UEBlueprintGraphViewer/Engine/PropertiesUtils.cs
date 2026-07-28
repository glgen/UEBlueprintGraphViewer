using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;
using System;
using System.Collections.Generic;
using System.Linq;
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

        private static PropertyData ResolvedObjectToFunctionProperty(GameSettings game, string name, ResolvedObject ownerResolved)
        {
            string objName = ownerResolved.Outer.Name.ToString();
            string funcName = ownerResolved.Name.ToString();
            List<PropertyData> props;
            if (game.ParamsDump.IsObjectNameUnique(objName))
            {
                props = game.ParamsDump.GetFunction(objName, funcName).Params;
            }
            else
            {
                props = game.ParamsDump.GetFunctionPathName(ownerResolved.Outer.GetPathName(), funcName).Params;
            }
            PropertyData? localVar = props.Find(o => o.Name.EqualsFName(name));
            if (localVar == null)
                throw new DecompilerException($"Failed to find local var {name} in {objName}.{funcName}");
            localVar.Name = name;
            return localVar;
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

                    if (global.Game.ParamsDump.TryFindProperty(name, ownerObj, out PropertyData? prop))
                    {
                        return prop!;
                    }
                }
            }

            PropertyData newProp = ToProperty(pointer);
            //global.Game.ParamsDump.AddProperty(newProp);
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
                        if (game.ParamsDump.TryFindProperty(ToName(pointer), ownerObj, out PropertyData? prop))
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
                    throw new DecompilerException($"UScriptClass property not found in dump:\nUScriptClass: {field.Name}\nProperty: {fFieldPath.Path[0]}");
                case UStruct struc when fFieldPath.Path.Length > 0 && struc.GetProperty(fFieldPath.Path[0], out var prop):
                    return prop;
            }
            return null;
        }
    }
}
