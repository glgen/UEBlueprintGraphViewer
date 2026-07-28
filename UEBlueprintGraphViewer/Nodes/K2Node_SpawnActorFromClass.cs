using CUE4Parse.UE4.Kismet;
using System.Collections.Generic;

namespace UEBlueprintGraphViewer.Nodes
{
    internal class K2Node_SpawnActorFromClass : BPNode
    {
        public K2Node_SpawnActorFromClass(List<GraphPin> Parms, KismetExpression Instr) : base("", Instr)
        {
            MakePins(true, true, Parms);
            Name = $"SpawnActor {Parms[1].Value}";
        }
    }
}
