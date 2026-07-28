using CUE4Parse.UE4.Kismet;
using System.Collections.Generic;

namespace UEBlueprintGraphViewer.Nodes
{
    internal class K2Node_CreateWidget : BPNode
    {
        public K2Node_CreateWidget(List<GraphPin> Parms, KismetExpression Instr) : base("", Instr)
        {
            MakePins(true, true, Parms);
            Name = $"Create {Parms[1].Value} Widget";
        }
    }
}
