using CUE4Parse.UE4.FMod.Nodes;
using CUE4Parse.UE4.Kismet;
using NAudio.SoundFont;
using System.Collections.Generic;
using UEBlueprintGraphViewer.Engine;
using static UEBlueprintGraphViewer.Engine.EngineBPData;
using static UEBlueprintGraphViewer.Engine.Utils;

namespace UEBlueprintGraphViewer.Nodes
{
    internal class K2Node_WidgetAnimationEvent : K2Node_Event
    {
        public readonly string AnimationPropertyName;
        public readonly WidgetAnimationEventType Action;

        public K2Node_WidgetAnimationEvent(string funcName, WidgetAnimationEventType action, string animationName, KismetExpression? instr) : base(funcName, [],
            instr)
        {
            Name = $"Animation {(action == WidgetAnimationEventType.Started ? "Started" : "Finished")} ({animationName})";
            Action = action;
            AnimationPropertyName = animationName;
        }
    }
}
