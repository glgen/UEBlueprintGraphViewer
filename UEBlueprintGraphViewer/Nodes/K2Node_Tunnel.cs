using CUE4Parse.UE4.Kismet;
using System.Collections.Generic;

namespace UEBlueprintGraphViewer.Nodes
{
    internal class K2Node_Tunnel : BPNode
    {
        public bool CanHaveInputs;
        public bool CanHaveOutputs;
        public K2Node_Tunnel(bool isOut, List<GraphPin> Parms, KismetExpression Instr) : base(isOut ? "Outputs" : "Inputs", Instr)
        {
            CanHaveInputs = isOut;
            CanHaveOutputs = !isOut;
            MakePins(false, false, Parms);
        }
    }
}
