using CUE4Parse.UE4.Kismet;
using System.Collections.Generic;
namespace UEBlueprintGraphViewer.Nodes
{
    internal class K2Node_FunctionResult : BPNode
    {
        public K2Node_FunctionResult(List<GraphPin> OutParms, KismetExpression Instr) : base("Return Node", Instr)
        {
            MakePins(true, false, OutParms);
        }
    }
}
