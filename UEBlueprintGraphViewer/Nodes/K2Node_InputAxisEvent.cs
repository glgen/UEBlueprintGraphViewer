using CUE4Parse.UE4.Kismet;
using System.Collections.Generic;

namespace UEBlueprintGraphViewer.Nodes
{
    internal class K2Node_InputAxisEvent : K2Node_Event
    {
        public readonly string InputAxisName;
        
        public K2Node_InputAxisEvent(string funcName, string axisName, List<GraphPin> parms, KismetExpression? instr) : base(funcName, parms,
            instr)
        {
            Name = axisName;
            InputAxisName = axisName;
        }
    }
}
