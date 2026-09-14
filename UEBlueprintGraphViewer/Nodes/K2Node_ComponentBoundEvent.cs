using CUE4Parse.UE4.Kismet;
using System.Collections.Generic;

namespace UEBlueprintGraphViewer.Nodes
{
    internal class K2Node_ComponentBoundEvent : K2Node_Event
    {
        public K2Node_ComponentBoundEvent(string funcName, string action, List<GraphPin> parms, KismetExpression? instr) : base(funcName, parms,
            instr)
        {
            Name = action;
        }
    }
}
