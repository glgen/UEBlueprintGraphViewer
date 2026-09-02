using CUE4Parse.UE4.Objects.UObject;
using System.Collections.Generic;
using UEBlueprintGraphViewer.Assets;
using UEBlueprintGraphViewer.Engine;

namespace UEBlueprintGraphViewer.Decompiler
{
    public class GlobalDecompilerContext
    {
        public readonly Asset CurrentAsset;
        public readonly GameSettings Game;
        public readonly bool IsUbergraph;
        public readonly UFunction FunctionToDecompile;
        public readonly UFunction CurrentFunction;
        public readonly List<PropertyData> FunctionLocals = [];

        public readonly List<int> ParsedInstructions = [];
        public readonly HashSet<int> ParsedInstructionsCanVisitAgain = [];
        
        public bool IsParsingMacros = true;
        public bool IsClearingTempVars = true;

        public GlobalDecompilerContext(Asset asset, GameSettings game, UFunction function)
        {
            CurrentAsset = asset;
            Game = game;
            IsUbergraph = asset.IsEvent(function);
            FunctionToDecompile = function;
            CurrentFunction = IsUbergraph ? CurrentAsset.UbergraphFunction! : FunctionToDecompile;
        }

        public bool CanVisitThisInstruction(int statementIndex)
        {
            return !ParsedInstructions.Contains(statementIndex) || ParsedInstructionsCanVisitAgain.Contains(statementIndex);
        }

        public void MarkAsParsed(int index)
        {
            if (!ParsedInstructions.Contains(index))
                ParsedInstructions.Add(index);
        }

        public void MarkAsParsedAndCanVisitAgain(int index)
        {
            MarkAsParsed(index);
            ParsedInstructionsCanVisitAgain.Add(index);
        }
    }
}
