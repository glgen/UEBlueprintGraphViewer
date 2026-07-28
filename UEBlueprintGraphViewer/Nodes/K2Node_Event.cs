using CUE4Parse.UE4.Kismet;
using System.Collections.Generic;

namespace UEBlueprintGraphViewer.Nodes
{
    internal class K2Node_Event : K2Node_FunctionEntry
    {
        public K2Node_Event(string funcName, List<GraphPin> parms, KismetExpression? instr) : base(funcName, parms, instr)
        {

        }
    }
}
