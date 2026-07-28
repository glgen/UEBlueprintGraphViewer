using CUE4Parse.UE4.Kismet;
using System.Collections.Generic;

namespace UEBlueprintGraphViewer.Nodes
{
    internal class K2Node_InputAxisKey : K2Node_Event
    {
        public K2Node_InputAxisKey(string FuncName, string axisName, List<GraphPin> Parms, KismetExpression Instr) : base(FuncName, Parms,
            Instr)
        {
            Name = axisName;
        }
    }
}
