using CUE4Parse.UE4.Kismet;
using System.Collections.Generic;
using UEBlueprintGraphViewer.Engine;
using static UEBlueprintGraphViewer.Engine.EngineBPData;
using static UEBlueprintGraphViewer.Engine.Utils;

namespace UEBlueprintGraphViewer.Nodes
{
    internal class K2Node_InputAction : K2Node_Event
    {
        public GraphPin Pressed;
        public GraphPin Released;
        public readonly string InputEventName;
        
        public K2Node_InputAction(string funcName, string eventName, List<GraphPin> parms, KismetExpression? instr) : base(funcName, parms,
            instr)
        {
            Name = eventName;
            InputEventName = eventName;
        }
        
        protected override void MakePins(bool needExec, bool needThen, List<GraphPin> parms)
        {
            GraphPinType execPinType = MakePinType(PinType.exec);
            Pressed = new GraphPin("Pressed", EngineEnums.EEdGraphPinDirection.EGPD_Output, execPinType);
            Released = new GraphPin("Released", EngineEnums.EEdGraphPinDirection.EGPD_Output, execPinType);
            AddOutputPin(Pressed);
            AddOutputPin(Released);
            MakePins(false, false, parms, null);
        }
    }
}
