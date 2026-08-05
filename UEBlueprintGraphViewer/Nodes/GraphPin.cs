using System;
using System.Collections.Generic;
using UEBlueprintGraphViewer.Engine;
using static UEBlueprintGraphViewer.Engine.EngineBPData;
using static UEBlueprintGraphViewer.Engine.EngineEnums;

namespace UEBlueprintGraphViewer.Nodes
{

    public class GraphPin
    {
        public Guid Guid = Guid.NewGuid();
        public EEdGraphPinDirection Direction;
        public List<GraphPin> LinkedTo = [];
        public string PinName { get; private set; }
        public string PinFriendlyName;
        public GraphPinType PinType;

        public BPNode ParentNode { get; private set; }

        public PropertyData? Property;

        public GraphPin Clone()
        {
            GraphPin result = new GraphPin(PinName, Value, Direction, PinType);
            foreach (GraphPin pin in LinkedTo)
                Utils.Connect(result, pin);
            return result;
        }

        public string Value = "";

        public bool IsNameHidden;
        public bool IsHidden { get; private set; }

        public float X;
        public float Y;

        public bool IsConnected { get; private set; }
        
        public bool IsInput => Direction == EEdGraphPinDirection.EGPD_Input;
        public bool IsOutput => Direction == EEdGraphPinDirection.EGPD_Output;

        public void SetName(string name)
        {
            PinName = name;
            PinFriendlyName = name;
        }

        public void HidePin()
        {
            if (LinkedTo.Count > 0)
                throw new Exception("Unable to hide connected pin");
            IsHidden = true;
        }

        public void BindToNode(BPNode node)
        {
            ParentNode = node;
            UpdateAnchor();
        }

        public void UpdateAnchor()
        {
            bool isOut = IsOutput;
            var collection = isOut ? ParentNode.Output : ParentNode.Input;
            int index = 0;
            foreach (var pin in collection)
            {
                if (pin.IsHidden) continue;
                if (pin == this) break;
                index++;
            }

            X = isOut ? ParentNode.Right : ParentNode.X;
            Y = ParentNode.Y + 17 + (ParentNode.HeaderHidden ? 0 : 30) + (index * 32);
        }

        public void Connect(GraphPin pin)
        {
            LinkedTo.Add(pin);
            IsConnected = true;
        }

        public void Disconnect(GraphPin pin)
        {
            LinkedTo.Remove(pin);
            IsConnected = LinkedTo.Count > 0;
        }

        public GraphPin(string name, EEdGraphPinDirection direction, GraphPinType type)
        {
            SetName(name);
            Direction = direction;
            PinType = type;
        }

        public GraphPin(string name, bool isInput, GraphPinType type) :
            this(name, isInput ? EEdGraphPinDirection.EGPD_Input : EEdGraphPinDirection.EGPD_Output, type) { }

        public GraphPin(string name, EEdGraphPinDirection direction, GraphPinType type, string friendlyName) : this(name, direction, type)
        {
            PinFriendlyName = friendlyName;
        }

        public GraphPin(string name, string value, EEdGraphPinDirection direction, GraphPinType type) : this(name, direction, type)
        {
            Value = value;
        }

        public GraphPin(string name, string value, EEdGraphPinDirection direction, GraphPinType type, PropertyData? property) : this(name, direction, type)
        {
            Value = value;
            Property = property;
        }
        
        public static bool IsBasicallyDifferent(GraphPin pin1, GraphPin pin2)
        {
            return pin1.PinName != pin2.PinName ||
                   pin1.PinFriendlyName != pin2.PinFriendlyName ||
                   pin1.PinType.PinCategory != pin2.PinType.PinCategory ||
                   pin1.PinType.ContainerType != pin2.PinType.ContainerType ||
                   pin1.IsConnected != pin2.IsConnected ||
                   pin1.IsHidden != pin2.IsHidden;
        }
    }
}
