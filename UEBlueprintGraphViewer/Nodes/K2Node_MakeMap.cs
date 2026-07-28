using CUE4Parse.UE4.Kismet;
using System.Collections.Generic;
using static UEBlueprintGraphViewer.Engine.EngineEnums;
using static UEBlueprintGraphViewer.Engine.EngineBPData;

namespace UEBlueprintGraphViewer.Nodes
{
    internal class K2Node_MakeMap : BPNode
    {
        public K2Node_MakeMap(List<GraphPin> ArrayContent, GraphPinType Type, KismetExpression Instr) : base("Make Map", Instr)
        {
            Pure = true;

            for (int i = 0; i < ArrayContent.Count; i += 2)
            {
                ArrayContent[i].SetName($"Key {i/2}");
                ArrayContent[i + 1].SetName($"Value {i/2}");
                AddInputPin(ArrayContent[i]);
                AddInputPin(ArrayContent[i + 1]);
            }

            GraphPin OutPin = new GraphPin("Map", EEdGraphPinDirection.EGPD_Output, Type);
            AddOutputPin(OutPin);
        }
    }
}
