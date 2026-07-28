using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UEBlueprintGraphViewer.Decompiler;
using UEBlueprintGraphViewer.Nodes;
using static UEBlueprintGraphViewer.Engine.EngineBPData;

namespace UEBlueprintGraphViewer.Engine
{
    public static class Utils
    {
        // connect two pins to each other
        public static void Connect(GraphPin? pin1, GraphPin? pin2)
        {
            if (pin1 == null || pin2 == null)
                throw new Exception("One of pins is null");

            pin1.Connect(pin2);
            pin2.Connect(pin1);
        }

        // make pin type struct with needed type
        public static GraphPinType MakePinType(PinType type)
        {
            return new() { PinCategory = type };
        }

        public static uint GetUbergraphEntryPoint(KismetExpression[] script)
        {
            KismetExpression? entryPoint = script[^3] switch
            {
                EX_LocalFinalFunction ex => ex.Parameters[0],
                EX_VirtualFunction ex => ex.Parameters[0],
                _ => null,
            };

            if (entryPoint is not EX_IntConst intConst)
                throw new DecompilerException("Failed to get ubergraph entry point");

            return Convert.ToUInt32(intConst.Value);
        }

        public static string PackageIndexToName(FPackageIndex index)
        {
            // try more faster (probably?) method if not iostore
            if (GetObjectResource(index) is { } resource)
            {
                return resource.ObjectName.ToString();
            }

            // if iostore
            return index.Name;
        }

        public static (string Name, string Outer) PackageIndexToNameAndOuter(FPackageIndex index)
        {
            // try more faster (probably?) method if not iostore
            if (GetObjectResource(index) is { } resource)
            {
                string name = resource.ObjectName.ToString();
                string outer = GetObjectResource(resource.OuterIndex!)!.ObjectName.ToString();
                return (name, outer);
            }

            // if iostore
            ResolvedObject? import = index.ResolvedObject;
            return (import!.Name.ToString(), import.Outer!.Name.ToString());
        }

        public static FunctionData GetFuncDataOfFuncCall(EX_FinalFunction instr, ParamMappings dump)
        {
            (string name, string outer) = instr.GetNameAndOuter();

            if (dump.IsObjectNameUnique(outer))
            {
                return dump.GetFunction(outer, name);
            }
            else
            {
                return dump.GetFunctionPathName(instr.StackNode.ResolvedObject!.Outer!.GetPathName(), name);
            }
        }


        // gets object resource for non-iostore games
        // used for getting names without resolving the objects
        private static FObjectResource? GetObjectResource(FPackageIndex index)
        {
            if (index.IsNull || index.Owner is not Package pkg)
                return null;

            return index.IsImport ? pkg.ImportMap[-index.Index - 1] : pkg.ExportMap[index.Index - 1];
        }

        public static bool IsLatentFunc(KismetExpression expr, out int? actionOffset)
        {
            actionOffset = null;
            if (expr is EX_CallMath callMath)
            {
                EX_StructConst? latentActionInfo = callMath.Parameters.OfType<EX_StructConst>().FirstOrDefault(o => PackageIndexToName(o.Struct) == "LatentActionInfo");
                if (latentActionInfo != null)
                {
                    actionOffset = latentActionInfo.Properties[0] switch
                    {
                        EX_SkipOffsetConst skip => (int)skip.Value,
                        EX_IntConst intConst => intConst.Value,
                        _ => -1,
                    };
                    return true;
                }
            }
            return false;
        }

        public static bool IsCallMathFunc(KismetExpression expr, string name, string outer)
        {
            if (expr is not EX_CallMath) { return false; }
            return IsFinalFunc(expr, name, outer);
        }

        public static bool IsFinalFunc(KismetExpression expr, string name, string outer)
        {
            if (expr is not EX_FinalFunction final) { return false; }
            (string funcName, string outerName) = final.GetNameAndOuter();
            return funcName == name && outerName == outer;
        }

        public static void ParseJumpExpr(KismetExpression expr, out KismetExpression? boolExpr)
        {
            switch (expr)
            {
                case EX_JumpIfNot jmp:
                    boolExpr = jmp.BooleanExpression;
                    break;
                case EX_PopExecutionFlowIfNot pop:
                    boolExpr = pop.BooleanExpression;
                    break;
                default:
                    throw new DecompilerException($"ParseJumpExpr: unknown jump instruction of type {expr.GetType()}");
            }
        }

        public static bool ParseConstExpr(KismetExpression expr, GameSettings game, out string value, out GraphPinType type)
        {
            value = "";
            PinType pinType;

            switch (expr)
            {
                case EX_Self:
                    value = "self";
                    pinType = PinType.Object;
                    break;
                case EX_NoObject:
                    value = "None";
                    pinType = PinType.Object;
                    break;
                case EX_NoInterface:
                    value = "None";
                    pinType = PinType.Interface;
                    break;
                case EX_True:
                    value = "true";
                    pinType = PinType.Bool;
                    break;
                case EX_False:
                    value = "false";
                    pinType = PinType.Bool;
                    break;
                case EX_IntZero:
                    value = "0";
                    pinType = PinType.Int;
                    break;
                case EX_IntOne:
                    value = "1";
                    pinType = PinType.Int;
                    break;
                case EX_StringConst exp:
                    value = exp.Value;
                    pinType = PinType.String;
                    break;
                case EX_UnicodeStringConst exp:
                    value = exp.Value;
                    pinType = PinType.String;
                    break;
                case EX_TextConst exp:
                    value = exp.GetSourceString();
                    pinType = PinType.Text;
                    break;
                case EX_NameConst exp:
                    value = exp.Value.ToString();
                    pinType = PinType.Name;
                    break;
                case EX_FloatConst exp:
                    value = exp.Value.ToString();
                    pinType = PinType.Float;
                    break;
                case EX_DoubleConst exp:
                    value = exp.Value.ToString();
                    pinType = PinType.Float;
                    break;
                case EX_IntConst exp:
                    value = exp.Value.ToString();
                    pinType = PinType.Int;
                    break;
                case EX_Int64Const exp:
                    value = exp.Value.ToString();
                    pinType = PinType.Int64;
                    break;
                case EX_UInt64Const exp:
                    value = exp.Value.ToString();
                    pinType = PinType.Int64;
                    break;
                case EX_IntConstByte exp:
                    value = exp.Value.ToString();
                    pinType = PinType.Byte;
                    break;
                case EX_ByteConst exp:
                    value = exp.Value.ToString();
                    pinType = PinType.Byte;
                    break;
                case EX_VectorConst exp:
                    value = $"({exp.Value})";
                    pinType = PinType.Vector;
                    break;
                case EX_Vector3fConst exp:
                    value = $"({exp.Value})";
                    pinType = PinType.Vector;
                    break;
                case EX_RotationConst exp:
                    value = $"({exp.Value})";
                    pinType = PinType.Rotator;
                    break;
                case EX_TransformConst exp:
                    value = exp.Value.ToString();
                    pinType = PinType.Transform;
                    break;
                case EX_SoftObjectConst exp:
                    EX_StringConst path = (EX_StringConst)exp.Value;
                    value = path.Value;
                    pinType = PinType.SoftObject;
                    break;
                case EX_ObjectConst exp:
                    value = PackageIndexToName(exp.Value);
                    pinType = PinType.Class;
                    break;
                case EX_StructConst exp:
                    value = exp.GetStructValue(game);
                    pinType = PinType.Struct;
                    break;
                case EX_MapConst exp:
                    value = "";
                    // can only be empty
                    if (exp.Elements.Length > 0)
                        throw new DecompilerException($"EX_MapConst has {exp.Elements.Length} elements ({exp.StatementIndex})");

                    type = PropertiesUtils.KismetPointerToPropertyUnknownType(exp.KeyProperty, game).PinType;
                    return true;
                case EX_ArrayConst exp:
                    value = exp.GetArrayValue(game);
                    type = PropertiesUtils.KismetPointerToPropertyUnknownType(exp.InnerProperty, game).PinType;
                    return true;
                case EX_InstanceDelegate exp:
                    value = exp.FunctionName.ToString();
                    pinType = PinType.Delegate;
                    break;
                default:
                    type = new GraphPinType();
                    return false;
            }

            type = MakePinType(pinType);

            return true;
        }

        public static bool IsCallFuncInstr(KismetExpression expr)
        {
            return expr is EX_CallMath or EX_FinalFunction or EX_VirtualFunction;
        }

        // Convert compiled bp struct member name to friendly name
        // BP struct member name format: {name}_{id}_{uuid(32)}
        public static string StructMemberNameToFriendlyName(string name)
        {
            // minimal length of "_{id}_{uuid}" postfix 
            if (name.Length < 35) { return name; }

            List<string> parts = name.Split("_").ToList();

            if (parts.Count < 3 || parts.Last().Length != 32) { return name; }

            parts.RemoveRange(parts.Count - 2, 2);
            return string.Join("_", parts);
        }
        
        public static string MatrixToString(bool[,] matrix)
        {
            StringBuilder sb = new();
            for (int i = 0; i < matrix.GetLength(0); i++) 
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    sb.Append($"{(matrix[i, j] ? "1" : "0")} ");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        public static string ToValidFileName(string fileName)
        {
            string result = fileName;
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                result = result.Replace(c, '_');
            }
            return result;
        }
        
        public static bool Starts(this string str, string compare)
        {
            return str.StartsWith(compare, StringComparison.Ordinal);
        }

        public static bool Ends(this string str, string compare)
        {
            return str.EndsWith(compare, StringComparison.Ordinal);
        }

        public static bool EqualsFName(this string str, string compare)
        {
            return str.Equals(compare, StringComparison.OrdinalIgnoreCase);
        }
        
        public static T? GetValueOfType<T>(this JObject obj, string name)
        {
            var value = obj.GetValue(name, StringComparison.Ordinal);
            if (value != null) return value.Value<T>();
            return default;
        }
    }
}
