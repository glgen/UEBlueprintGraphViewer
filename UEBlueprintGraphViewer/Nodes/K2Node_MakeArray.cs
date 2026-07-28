using CUE4Parse.UE4.Kismet;
using System.Collections.Generic;
using static UEBlueprintGraphViewer.Engine.EngineEnums;
using static UEBlueprintGraphViewer.Engine.Utils;
using static UEBlueprintGraphViewer.Engine.EngineBPData;

namespace UEBlueprintGraphViewer.Nodes
{
    internal class K2Node_MakeArray : BPNode
    {
        public K2Node_MakeArray(List<GraphPin> ArrayContent, GraphPinType Type, KismetExpression Instr) : base("Make Array", Instr)
        {
            Pure = true;

            for (int i = 0; i < ArrayContent.Count; i++)
            {
                ArrayContent[i].SetName($"[{i}]");
                AddInputPin(ArrayContent[i]);
            }

            GraphPinType PinType = MakePinType(Type.PinCategory);
            PinType.ContainerType = EPinContainerType.Array;

            GraphPin OutPin = new GraphPin("Array", EEdGraphPinDirection.EGPD_Output, PinType);
            AddOutputPin(OutPin);
        }
    }
}
