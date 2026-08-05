using CUE4Parse.UE4.Kismet;
using UEBlueprintGraphViewer.Engine;
using static UEBlueprintGraphViewer.Engine.EngineBPData;
using static UEBlueprintGraphViewer.Engine.EngineEnums;

namespace UEBlueprintGraphViewer.Nodes
{
    public class K2Node_VariableGet : BPNode
    {
        public GraphPin? TargetPin;
        public GraphPin VarPin;
        public PropertyData Property;
        
        public K2Node_VariableGet(PropertyData data, string variableName, GraphPin? targetPin, KismetExpression instr) : base("", instr)
        {
            Property = data;
            HeaderHidden = true;
            Pure = true;
            TargetPin = targetPin;
            VarPin = new GraphPin(variableName, EEdGraphPinDirection.EGPD_Output, data.PinType);
            AddOutputPin(VarPin);
            if (TargetPin != null)
                AddInputPin(TargetPin);
        }
    }
}
