using CUE4Parse.UE4.Kismet;
using static UEBlueprintGraphViewer.Engine.EngineBPData;
using static UEBlueprintGraphViewer.Engine.EngineEnums;

namespace UEBlueprintGraphViewer.Nodes
{
    public class K2Node_VariableGet : BPNode
    {
        public GraphPin? TargetPin;
        public GraphPin VarPin;
        public K2Node_VariableGet(string variableName, GraphPinType type, GraphPin? targetPin, KismetExpression instr) : base("", instr)
        {
            HeaderHidden = true;
            Pure = true;
            TargetPin = targetPin;
            VarPin = new GraphPin(variableName, EEdGraphPinDirection.EGPD_Output, type);
            AddOutputPin(VarPin);
            if (TargetPin != null)
                AddInputPin(TargetPin);
        }
    }
}
