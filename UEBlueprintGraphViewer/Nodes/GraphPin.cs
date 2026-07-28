using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
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

        public void SetName(string Name)
        {
            PinName = Name;
            PinFriendlyName = Name;
        }

        public void HidePin()
        {
            if (LinkedTo.Count > 0)
                throw new Exception("Unable to hide connected pin");
            IsHidden = true;
        }

        public void BindToNode(BPNode Node)
        {
            ParentNode = Node;
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

        public void Connect(GraphPin Pin)
        {
            LinkedTo.Add(Pin);
            IsConnected = true;
        }

        public void Disconnect(GraphPin Pin)
        {
            LinkedTo.Remove(Pin);
            IsConnected = LinkedTo.Count > 0;
        }

        public GraphPin(string Name, EEdGraphPinDirection Direction, GraphPinType Type)
        {
            SetName(Name);
            this.Direction = Direction;
            PinType = Type;
        }

        public GraphPin(string Name, bool IsInput, GraphPinType Type) :
            this(Name, IsInput ? EEdGraphPinDirection.EGPD_Input : EEdGraphPinDirection.EGPD_Output, Type) { }

        public GraphPin(string Name, EEdGraphPinDirection Direction, GraphPinType Type, string FriendlyName) : this(Name, Direction, Type)
        {
            PinFriendlyName = FriendlyName;
        }

        public GraphPin(string Name, string Value, EEdGraphPinDirection Direction, GraphPinType Type) : this(Name, Direction, Type)
        {
            this.Value = Value;
        }

        public GraphPin(string Name, string Value, EEdGraphPinDirection Direction, GraphPinType Type, PropertyData? Property) : this(Name, Direction, Type)
        {
            this.Value = Value;
            this.Property = Property;
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
