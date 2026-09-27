using CUE4Parse.UE4.FMod.Nodes;
using CUE4Parse.UE4.Kismet;
using NAudio.SoundFont;
using System.Collections.Generic;
using UEBlueprintGraphViewer.Engine;
using static UEBlueprintGraphViewer.Engine.EngineBPData;
using static UEBlueprintGraphViewer.Engine.Utils;

namespace UEBlueprintGraphViewer.Nodes
{
    internal class K2Node_WidgetAnimationEvt : K2Node_Event
    {
        public GraphPin WidgetAnimExec;
        public readonly string InputEventName;

        public K2Node_WidgetAnimationEvt(string funcName, string eventName, List<GraphPin> parms, KismetExpression? instr) : base(funcName, parms,
            instr)
        {
            Name = eventName;
            InputEventName = eventName;
        }

        protected override void MakePins(bool needExec, bool needThen, List<GraphPin> parms)
        {
            GraphPinType execPinType = MakePinType(PinType.exec);
            WidgetAnimExec = new GraphPin("", EngineEnums.EEdGraphPinDirection.EGPD_Output, execPinType);
            AddOutputPin(WidgetAnimExec);
            MakePins(false, false, parms, null);
        }
    }
}
