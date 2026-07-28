using CUE4Parse.UE4.Kismet;
using System.Collections.Generic;
using UEBlueprintGraphViewer.Engine;
using static UEBlueprintGraphViewer.Engine.EngineBPData;
using static UEBlueprintGraphViewer.Engine.Utils;

namespace UEBlueprintGraphViewer.Nodes
{
    internal class K2Node_EnhancedInputAction : K2Node_Event
    {
        public GraphPin Triggered;
        public GraphPin Started;
        public GraphPin Ongoing;
        public GraphPin Canceled;
        public GraphPin Completed;
        public readonly string InputAction;
        
        public K2Node_EnhancedInputAction(string funcName, string action, List<GraphPin> parms, KismetExpression? instr) : base(funcName, parms,
            instr)
        {
            Name = action;
            InputAction = action;
        }
        
        protected override void MakePins(bool needExec, bool needThen, List<GraphPin> parms)
        {
            GraphPinType execPinType = MakePinType(PinType.exec);
            Triggered = new GraphPin("Triggered", EngineEnums.EEdGraphPinDirection.EGPD_Output, execPinType);
            Started = new GraphPin("Started", EngineEnums.EEdGraphPinDirection.EGPD_Output, execPinType);
            Ongoing = new GraphPin("Ongoing", EngineEnums.EEdGraphPinDirection.EGPD_Output, execPinType);
            Canceled = new GraphPin("Canceled", EngineEnums.EEdGraphPinDirection.EGPD_Output, execPinType);
            Completed = new GraphPin("Completed", EngineEnums.EEdGraphPinDirection.EGPD_Output, execPinType);
            AddOutputPin(Triggered);
            AddOutputPin(Started);
            AddOutputPin(Ongoing);
            AddOutputPin(Canceled);
            AddOutputPin(Completed);
            MakePins(false, false, parms, null);
        }
    }
}
