using CUE4Parse.UE4.Kismet;
using System.Collections.Generic;

namespace UEBlueprintGraphViewer.Nodes
{
    internal class K2Node_FunctionEntry : BPNode
    {
        public string FunctionName;
        public K2Node_FunctionEntry(string FuncName, List<GraphPin> Parms, KismetExpression Instr) : base(FuncName, Instr)
        {
            FunctionName = FuncName;
            MakePins(false, true, Parms);
        }
    }
}
