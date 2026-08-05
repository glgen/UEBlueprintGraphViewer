using CUE4Parse.UE4.Kismet;
using System;
using System.Collections.Generic;
using System.Linq;
using CUE4Parse.UE4.Objects.UObject;
using UEBlueprintGraphViewer.Decompiler;
using static UEBlueprintGraphViewer.Engine.Utils;

namespace UEBlueprintGraphViewer.Engine
{
    internal static class ExpressionsExtensions
    {
        public static string GetSourceString(this EX_TextConst ex)
        {
            return ex.Value.TextLiteralType switch
            {
                EBlueprintTextLiteralType.Empty => "",
                EBlueprintTextLiteralType.LocalizedText or
                EBlueprintTextLiteralType.InvariantText or
                EBlueprintTextLiteralType.LiteralString => ex.Value.SourceString!.GetStringConstValue(),
                EBlueprintTextLiteralType.StringTableEntry => ex.Value.KeyString!.GetStringConstValue(),
                _ => throw new NotImplementedException(),
            };
        }

        private static string GetStringConstValue(this KismetExpression ex)
        {
            return ex switch
            {
                EX_StringConst s => s.Value,
                EX_UnicodeStringConst s => s.Value,
                _ => throw new DecompilerException($"Trying to get string const value of non-stringconst instruction: {ex.GetType().Name}"),
            };
        }
        
        public static string GetStructValue(this EX_StructConst ex, GameSettings game)
        {
            List<string>? properties = null;
            var struc = ex.Struct.Load();
            
            if (struc is UScriptClass or UScriptStruct && game.Jmap.TryFindProperties(ex.Struct, out List<PropertyData>? props))
                properties = [.. props!.Select(o => o.Name)];
            
            if (properties == null && struc is UStruct str)
                properties = [.. str.ChildProperties.Select(o => o.Name.ToString())];
            
            if (properties == null)
                throw new DecompilerException($"Failed to find struct EX_StructConst is referencing to. Struct: {PackageIndexToName(ex.Struct)}");
            
            if (properties.Count != ex.Properties.Length)
                throw new DecompilerException($"Struct member count mismatch. Found {properties.Count} members in dump, expected in EX_StructConst: {ex.Properties.Length} Struct: {PackageIndexToName(ex.Struct)}");
            
            List<string> parms = [];
            for (int i = 0; i < ex.Properties.Length; i++)
            {
                ParseConstExpr(ex.Properties[i], game, out string value, out _);
                parms.Add($"{properties![i]}={value}");
            }

            return $"({string.Join("; ", parms)})";
        }

        public static string GetArrayValue(this EX_ArrayConst ex, GameSettings game)
        {
            var parms = ex.Elements.Select(o =>
            {
                ParseConstExpr(o, game, out string value, out _);
                return value;
            });

            return $"[{string.Join(", ", parms)}]";
        }

        public static (string Name, string Outer) GetNameAndOuter(this EX_FinalFunction expr)
        {
            return PackageIndexToNameAndOuter(expr.StackNode);
        }
    }
}
