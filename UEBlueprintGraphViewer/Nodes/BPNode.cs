using Avalonia;
using CUE4Parse.UE4.Kismet;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UEBlueprintGraphViewer.Comparing;
using static UEBlueprintGraphViewer.Engine.EngineBPData;
using static UEBlueprintGraphViewer.Engine.EngineEnums;
using static UEBlueprintGraphViewer.Engine.Utils;

namespace UEBlueprintGraphViewer.Nodes
{
    public class BPNode
    {
        public int StatementIndex => NodeInstr?.StatementIndex ?? -1;
        public KismetExpression? NodeInstr;

        public float X;
        public float Y;
        
        public int NodeWidth;
        public int NodeHeight;
        
        public float Right => X + NodeWidth;
        public float Bottom => Y + NodeHeight;

        public GraphPin? ExecPin;
        public GraphPin? ExecOutPin;

        public string NodeType;

        public string Name;

        public bool HeaderHidden;
        public bool HeaderCenter;

        public bool ShowNameAsBody;

        public bool Pure;
    
        public ChangeStatus ChangeStatus;

        public string NodeText => $"StatementIndex: {StatementIndex}\nNodeWidth: {NodeWidth}\nNodeHeight: {NodeHeight}\nX: {X}\nY: {Y}";

        public string NodeJson => JsonConvert.SerializeObject(NodeInstr, Formatting.Indented);

        public readonly List<GraphPin> Input = [];
        public readonly List<GraphPin> Output = [];

        public int InputParamsCount => Input.Count - (ExecPin == null ? 0 : 1);
        public int OutputParamsCount => Output.Count - (ExecOutPin == null ? 0 : 1);


        public void AddInputPin(GraphPin Pin)
        {
            Pin.BindToNode(this);
            Input.Add(Pin);
        }

        public void AddInputPin(GraphPin Pin, int Index)
        {
            Pin.BindToNode(this);
            if (ExecPin != null)
                Index += 1;
            Input.Insert(Index, Pin);
        }

        public void SetInputPin(GraphPin Pin, int Index)
        {
            Pin.BindToNode(this);
            if (ExecPin != null)
                Index += 1;
            Input[Index] = Pin;
        }

        public void AddOutputPin(GraphPin Pin)
        {
            Pin.BindToNode(this);
            Output.Add(Pin);
        }

        public GraphPin? GetFirstInputParam()
        {
            return Input.Find(o => o != ExecPin);
        }

        public GraphPin? GetFirstOutputParam()
        {
            return Output.Find(o => o != ExecOutPin);
        }

        public void SetPosition(float x, float y)
        {
            X = x;
            Y = y;
            foreach (var pin in Input)
                pin.UpdateAnchor();
            foreach (var pin in Output)
                pin.UpdateAnchor();
        }

        public BPNode() { }

        public BPNode(string name, KismetExpression? Instr)
        {
            Name = name;
            NodeInstr = Instr;
            NodeType = GetType().Name;
        }

        public void CalculateSize()
        {
            //Approximate size

            const int CharSize = 7;
            const int CharSizeBodyText = 15;
            const int Padding = 32;
            const int ValueBoxMinSize = 30;
            const int ValueBoxMaxSize = 400;

            const int PinHeight = 32;

            int InputSize = 0;
            int OutputSize = 0;
            int InputPinsHeight = 0;
            int OutputPinsHeight = 0;

            NodeHeight = HeaderHidden ? 0 : 34; // node header

            foreach (GraphPin Pin in Input)
            {
                if (Pin.IsHidden) continue;

                InputPinsHeight += PinHeight;

                int InpNameSize = Pin.IsNameHidden ? 0 : Pin.PinFriendlyName.Length * CharSize;
                int InputValueSize = 0;

                if (Pin.LinkedTo.Count == 0)
                {
                    InputValueSize = Math.Clamp(Pin.Value.Length * CharSize + 16, ValueBoxMinSize, ValueBoxMaxSize) + 4;
                }
                InputSize = Math.Max(InputSize, InpNameSize + InputValueSize);
            }

            foreach (GraphPin Pin in Output)
            {
                if (Pin.IsHidden) continue;

                OutputPinsHeight += PinHeight;
                OutputSize = Math.Max(OutputSize, Pin.IsNameHidden ? 0 : Pin.PinFriendlyName.Length * CharSize);
            }

            int InputPadding = InputPinsHeight == 0 ? 0 : Padding;
            int OutputPadding = OutputPinsHeight == 0 ? 0 : Padding;

            int BodySize = 35;
            if (ShowNameAsBody)
            {
                BodySize = 12 + Math.Max(23, Name.Length * CharSizeBodyText);
            }

            NodeWidth = InputPadding + InputSize + BodySize + OutputSize + OutputPadding;
            NodeWidth = Math.Max(NodeWidth, Name.Length * CharSize);
            NodeHeight += Math.Max(InputPinsHeight, OutputPinsHeight);
        }

        protected virtual void MakePins(bool needExec, bool needThen)
        {
            MakePins(needExec, needThen, []);
        }

        protected virtual void MakePins(bool needExec, bool needThen, GraphPin ContextPin)
        {
            MakePins(needExec, needThen, [], ContextPin);
        }

        protected virtual void MakePins(bool needExec, bool needThen, List<GraphPin> parms)
        {
            MakePins(needExec, needThen, parms, null);
        }

        protected virtual void MakePins(bool needExec, bool needThen, List<GraphPin> parms, GraphPin? contextPin)
        {
            Pure = !needExec && !needThen;

            GraphPinType execPinType = MakePinType(PinType.exec);
            if (needExec)
            {
                ExecPin = new GraphPin("execute", EEdGraphPinDirection.EGPD_Input, execPinType);
                ExecPin.IsNameHidden = true;
                AddInputPin(ExecPin);
            }
            if (needThen)
            {
                ExecOutPin = new GraphPin("then", EEdGraphPinDirection.EGPD_Output, execPinType);
                ExecOutPin.IsNameHidden = true;
                AddOutputPin(ExecOutPin);
            }

            if (contextPin != null)
            {
                AddInputPin(contextPin);
            }

            foreach (GraphPin pin in parms)
            {
                switch (pin.Direction)
                {
                    case EEdGraphPinDirection.EGPD_Input:
                        {
                            AddInputPin(pin);
                            break;
                        }
                    case EEdGraphPinDirection.EGPD_Output:
                        {
                            AddOutputPin(pin);
                            break;
                        }
                }
            }
        }

        public void RemoveFakeConnections()
        {
            List<GraphPin> pins = [.. Input, .. Output];
            foreach (GraphPin pin in pins)
            {
                List<GraphPin> connections = [.. pin.LinkedTo];
                foreach (GraphPin connectedPin in connections)
                {
                    if (connectedPin.ParentNode == null)
                    {
                        pin.Disconnect(connectedPin);
                    }
                }
            }
        }

        public bool TestEquality(BPNode node)
        {
            if (GetType() != node.GetType())
                return false;

            if (Name != node.Name ||
                NodeWidth != node.NodeWidth ||
                NodeHeight != node.NodeHeight ||
                Input.Count != node.Input.Count ||
                Output.Count != node.Output.Count)
                return false;

            return true;
        }

        public bool CheckIsChanged(BPNode node)
        {
            for (int i = 0; i < node.Input.Count; i++)
            {
                GraphPin pin1 = Input[i];
                
                GraphPin pin2 = node.Input[i];
                if (CheckPin(pin1, pin2))
                    return true;

                if (!pin1.IsConnected &&
                    pin1.Value != pin2.Value)
                    return true;
            }
            for (int i = 0; i < node.Output.Count; i++)
            {
                GraphPin pin1 = Output[i];
                
                GraphPin pin2 = node.Output[i];
                if (CheckPin(pin1, pin2))
                    return true;
            }

            return false;

            static bool CheckPin(GraphPin pin1, GraphPin pin2)
            {
                if (GraphPin.IsBasicallyDifferent(pin1, pin2))
                    return true;

                if (pin1.IsConnected)
                {
                    if (pin1.LinkedTo.Count != pin2.LinkedTo.Count)
                        return true;
                }

                return false;
            }
        }
    }
}
