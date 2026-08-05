using CUE4Parse.UE4.Kismet;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
        
        public string NodeJson => JsonConvert.SerializeObject(NodeInstr, Formatting.Indented);

        public readonly List<GraphPin> Input = [];
        public readonly List<GraphPin> Output = [];

        public int InputParamsCount => Input.Count - (ExecPin == null ? 0 : 1);
        public int OutputParamsCount => Output.Count - (ExecOutPin == null ? 0 : 1);


        public void AddInputPin(GraphPin pin)
        {
            pin.BindToNode(this);
            Input.Add(pin);
        }

        public void AddInputPin(GraphPin pin, int index)
        {
            pin.BindToNode(this);
            if (ExecPin != null)
                index += 1;
            Input.Insert(index, pin);
        }

        public void SetInputPin(GraphPin pin, int index)
        {
            pin.BindToNode(this);
            if (ExecPin != null)
                index += 1;
            Input[index] = pin;
        }

        public void AddOutputPin(GraphPin pin)
        {
            pin.BindToNode(this);
            Output.Add(pin);
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

        public BPNode(string name, KismetExpression? instr)
        {
            Name = name;
            NodeInstr = instr;
            NodeType = GetType().Name;
        }

        public void CalculateSize()
        {
            //Approximate size

            const int charSize = 7;
            const int charSizeBodyText = 15;
            const int padding = 32;
            const int valueBoxMinSize = 30;
            const int valueBoxMaxSize = 400;

            const int pinHeight = 32;

            int inputSize = 0;
            int outputSize = 0;
            int inputPinsHeight = 0;
            int outputPinsHeight = 0;

            NodeHeight = HeaderHidden ? 0 : 34; // node header

            foreach (GraphPin pin in Input)
            {
                if (pin.IsHidden) continue;

                inputPinsHeight += pinHeight;

                int inpNameSize = pin.IsNameHidden ? 0 : pin.PinFriendlyName.Length * charSize;
                int inputValueSize = 0;

                if (pin.LinkedTo.Count == 0)
                {
                    inputValueSize = Math.Clamp(pin.Value.Length * charSize + 16, valueBoxMinSize, valueBoxMaxSize) + 4;
                }
                inputSize = Math.Max(inputSize, inpNameSize + inputValueSize);
            }

            foreach (GraphPin pin in Output)
            {
                if (pin.IsHidden) continue;

                outputPinsHeight += pinHeight;
                outputSize = Math.Max(outputSize, pin.IsNameHidden ? 0 : pin.PinFriendlyName.Length * charSize);
            }

            int inputPadding = inputPinsHeight == 0 ? 0 : padding;
            int outputPadding = outputPinsHeight == 0 ? 0 : padding;

            int bodySize = 35;
            if (ShowNameAsBody)
            {
                bodySize = 12 + Math.Max(23, Name.Length * charSizeBodyText);
            }

            NodeWidth = inputPadding + inputSize + bodySize + outputSize + outputPadding;
            NodeWidth = Math.Max(NodeWidth, Name.Length * charSize + 20);
            NodeHeight += Math.Max(inputPinsHeight, outputPinsHeight);
        }

        protected virtual void MakePins(bool needExec, bool needThen)
        {
            MakePins(needExec, needThen, []);
        }

        protected virtual void MakePins(bool needExec, bool needThen, GraphPin contextPin)
        {
            MakePins(needExec, needThen, [], contextPin);
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
