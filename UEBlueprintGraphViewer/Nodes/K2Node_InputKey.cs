using CUE4Parse.UE4.Kismet;
using System.Collections.Generic;

namespace UEBlueprintGraphViewer.Nodes
{
    internal class K2Node_InputKey : K2Node_InputAction
    {
        public K2Node_InputKey(string funcName, string eventName, List<GraphPin> parms, KismetExpression? instr) : base(
            funcName, eventName, parms, instr) { }
    }
}
