using CUE4Parse.UE4.Kismet;
using static UEBlueprintGraphViewer.Engine.EngineBPData;
using static UEBlueprintGraphViewer.Engine.EngineEnums;
using static UEBlueprintGraphViewer.Engine.Utils;

namespace UEBlueprintGraphViewer.Nodes
{
    internal class K2Node_IfThenElse : BPNode
    {
        public GraphPin ExecElsePin;
        public GraphPin ConditionPin;
        public K2Node_IfThenElse(GraphPin ConditionPin, KismetExpression Instr) : base("Branch", Instr)
        {
            ConditionPin.SetName("Condition");
            this.ConditionPin = ConditionPin;
            MakePins();
        }

        protected void MakePins()
        {
            GraphPinType ExecPinType = MakePinType(PinType.exec);

            ExecPin = new GraphPin("execute", EEdGraphPinDirection.EGPD_Input, ExecPinType);
            ExecPin.IsNameHidden = true;
            ExecOutPin = new GraphPin("then", EEdGraphPinDirection.EGPD_Output, ExecPinType, "True");
            ExecElsePin = new GraphPin("else", EEdGraphPinDirection.EGPD_Output, ExecPinType, "False");

            AddInputPin(ExecPin);
            AddInputPin(ConditionPin);
            AddOutputPin(ExecOutPin);
            AddOutputPin(ExecElsePin);
        }
    }
}
