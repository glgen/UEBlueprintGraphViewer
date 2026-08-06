using CUE4Parse.UE4.Kismet;
using static UEBlueprintGraphViewer.Engine.EngineBPData;
using static UEBlueprintGraphViewer.Engine.EngineEnums;
using static UEBlueprintGraphViewer.Engine.Utils;

namespace UEBlueprintGraphViewer.Nodes
{
    public class K2Node_DynamicCast : BPNode
    {
        public GraphPin? ExecFailedPin;
        public GraphPin ObjectPin;
        public GraphPin AsObjectPin;
        public string ClassName;
        
        public K2Node_DynamicCast(GraphPin ObjectPin, GraphPin AsObjectPin, string ClassName, KismetExpression Instr, bool IsInterface = false) : base($"Cast To {ClassName}", Instr)
        {
            this.ObjectPin = ObjectPin;
            this.AsObjectPin = AsObjectPin;
            this.ClassName = ClassName;

            this.ObjectPin.SetName("Object");
            this.AsObjectPin.SetName($"As {ClassName}");
            this.AsObjectPin.PinType = MakePinType(IsInterface ? PinType.Interface : PinType.Object);

            MakePins();
        }

        protected void MakePins()
        {
            GraphPinType ExecPinType = MakePinType(PinType.exec);

            ExecPin = new GraphPin("execute", EEdGraphPinDirection.EGPD_Input, ExecPinType);
            ExecPin.IsNameHidden = true;
            ExecOutPin = new GraphPin("then", EEdGraphPinDirection.EGPD_Output, ExecPinType, "");
            ExecOutPin.IsNameHidden = true;
            ExecFailedPin = new GraphPin("CastFailed", EEdGraphPinDirection.EGPD_Output, ExecPinType, "Cast Failed");

            AddInputPin(ExecPin);
            AddInputPin(ObjectPin);
            AddOutputPin(ExecOutPin);
            AddOutputPin(ExecFailedPin);
            AddOutputPin(AsObjectPin);
        }
    }
}
