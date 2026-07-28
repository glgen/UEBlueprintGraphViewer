using CUE4Parse.UE4.Kismet;
using System.Collections.Generic;

namespace UEBlueprintGraphViewer.Nodes
{
    internal class K2Node_GenericCreateObject : BPNode
    {
        public K2Node_GenericCreateObject(List<GraphPin> Parms, KismetExpression Instr) : base("", Instr)
        {
            MakePins(true, true, Parms);
            Name = $"Construct {Parms[0].Value}";
        }
    }
}
