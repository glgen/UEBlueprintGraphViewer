using CUE4Parse.UE4.Kismet;
using System.Collections.Generic;
using UEBlueprintGraphViewer.Engine;
using static UEBlueprintGraphViewer.Engine.EngineBPData;
using static UEBlueprintGraphViewer.Engine.Utils;

namespace UEBlueprintGraphViewer.Nodes
{
    internal class K2Node_Timeline : BPNode
    {
        public readonly GraphPin Play;
        public readonly GraphPin PlayFromStart;
        public readonly GraphPin Stop;
        public readonly GraphPin Reverse;
        public readonly GraphPin ReverseFromEnd;
        public readonly GraphPin SetNewTime;
        public readonly GraphPin NewTime;
        public readonly GraphPin Update;
        public readonly GraphPin Finished;
        public readonly GraphPin Direction;
        public readonly Dictionary<string, GraphPin> Tracks = [];
        
        public K2Node_Timeline(string name, List<FloatTrack> tracks, KismetExpression? instr) : base(name, instr)
        {
            GraphPinType execPinType = MakePinType(PinType.exec);
            GraphPinType floatPinType = MakePinType(PinType.Float);
            GraphPinType enumPinType = MakePinType(PinType.Enum);

            Play = new GraphPin("Play", Engine.EngineEnums.EEdGraphPinDirection.EGPD_Input, execPinType);
            PlayFromStart = new GraphPin("Play from Start", Engine.EngineEnums.EEdGraphPinDirection.EGPD_Input, execPinType);
            Stop = new GraphPin("Stop", Engine.EngineEnums.EEdGraphPinDirection.EGPD_Input, execPinType);
            Reverse = new GraphPin("Reverse", Engine.EngineEnums.EEdGraphPinDirection.EGPD_Input, execPinType);
            ReverseFromEnd = new GraphPin("Reverse from End", Engine.EngineEnums.EEdGraphPinDirection.EGPD_Input, execPinType);
            SetNewTime = new GraphPin("Set New Time", Engine.EngineEnums.EEdGraphPinDirection.EGPD_Input, execPinType);
            NewTime = new GraphPin("New Time", Engine.EngineEnums.EEdGraphPinDirection.EGPD_Input, floatPinType);
            Update = new GraphPin("Update", Engine.EngineEnums.EEdGraphPinDirection.EGPD_Output, execPinType);
            Finished = new GraphPin("Finished", Engine.EngineEnums.EEdGraphPinDirection.EGPD_Output, execPinType);
            Direction = new GraphPin("Direction", Engine.EngineEnums.EEdGraphPinDirection.EGPD_Output, enumPinType);
            
            AddInputPin(Play);
            AddInputPin(PlayFromStart);
            AddInputPin(Stop);
            AddInputPin(Reverse);
            AddInputPin(ReverseFromEnd);
            AddInputPin(SetNewTime);
            AddInputPin(NewTime);
            AddOutputPin(Update);
            AddOutputPin(Finished);
            AddOutputPin(Direction);

            foreach (var floatTrack in tracks)
            {
                GraphPin trackPin = new GraphPin(floatTrack.TrackName, Engine.EngineEnums.EEdGraphPinDirection.EGPD_Output, floatPinType);
                Tracks.Add(floatTrack.TrackName, trackPin);
                AddOutputPin(trackPin);
            }
        }
    }
}
