using CUE4Parse.UE4.Kismet;
using System.Collections.Generic;
using static UEBlueprintGraphViewer.Engine.EngineBPData;
using static UEBlueprintGraphViewer.Engine.EngineEnums;
using static UEBlueprintGraphViewer.Engine.Utils;

namespace UEBlueprintGraphViewer.Nodes
{
    internal class K2Node_Select : BPNode
    {
        public GraphPin IndexPin;
        public List<GraphPin> Cases;
        public K2Node_Select(GraphPin IndexPin, List<GraphPin> Cases, KismetExpression Instr) : base("Select", Instr)
        {
            this.IndexPin = IndexPin;
            this.Cases = Cases;
            MakePins();
        }

        protected void MakePins()
        {
            GraphPinType PinType = Cases[0].PinType;

            GraphPin ResultPin = new GraphPin("Return Value", EEdGraphPinDirection.EGPD_Output, PinType);

            foreach (GraphPin Pin in Cases)
            {
                AddInputPin(Pin);
            }

            IndexPin.SetName("Index");

            AddInputPin(IndexPin);
            AddOutputPin(ResultPin);
        }
    }
}
