using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UEBlueprintGraphViewer.Engine;
using UEBlueprintGraphViewer.Nodes;

namespace UEBlueprintGraphViewer;

public class GraphJson
{
    // Convert a graph to JSON
    public static string ToJson(BPGraph thisGraph)
    {
        object graph = new
        {
            Name = thisGraph.MacroName,
            Nodes = thisGraph.Nodes.Select(o => new JsonNode()
            {
                Name = o.Name,
                NodeType = o.NodeType,
                LocationX = Convert.ToInt32(o.X),
                LocationY = Convert.ToInt32(o.Y),
                NodeWidth = o.NodeWidth,
                NodeHeight = o.NodeHeight,
                HeaderHidden = o.HeaderHidden,
                HeaderCenter = o.HeaderCenter,
                ShowNameAsBody = o.ShowNameAsBody,
                Pure = o.Pure,
                Input = o.Input.Select(o => ConvertPin(o)),
                Output = o.Output.Select(o => ConvertPin(o)),
            })
        };

        return JsonConvert.SerializeObject(graph, Formatting.Indented);

        static JsonPin ConvertPin(GraphPin o)
        {
            return new JsonPin
            {
                Guid = o.Guid,
                PinName = o.PinName,
                PinFriendlyName = o.PinFriendlyName,
                PinType = o.PinType,
                Value = o.Value,
                IsNameHidden = o.IsNameHidden,
                IsHidden = o.IsHidden,
                LinkedTo = o.LinkedTo.Select(o => o.Guid)
            };
        }
    }
    
    // Convert JSON to graph
    public static BPGraph FromJson(string json)
    {
        BPGraph graph = new();

        var jsonExport = JsonConvert.DeserializeObject<JsonExport>(json);
        
        graph.MacroName = jsonExport.Name;
        
        List<JsonNode> nodes = jsonExport.Nodes;

        List<GraphPin> pins = [];
        List<JsonPin> pinsJson = [];

        foreach (var node in nodes)
        {
            //BPNode newNode = (BPNode)Activator.CreateInstance(node.NodeType);
            BPNode newNode = new();

            newNode.Name = node.Name;
            newNode.NodeType = node.NodeType;
            newNode.X = node.LocationX;
            newNode.Y = node.LocationY;
            newNode.NodeWidth = node.NodeWidth;
            newNode.NodeHeight = node.NodeHeight;
            newNode.HeaderHidden = node.HeaderHidden;
            newNode.HeaderCenter = node.HeaderCenter;
            newNode.ShowNameAsBody = node.ShowNameAsBody;
            newNode.Pure = node.Pure;

            foreach (var pin in node.Input)
            {
                var p = ConvertPin(pin, true);
                newNode.AddInputPin(p);

                pins.Add(p);
                pinsJson.Add(pin);
            }
            foreach (var pin in node.Output)
            {
                var p = ConvertPin(pin, false);
                newNode.AddOutputPin(p);

                pins.Add(p);
                pinsJson.Add(pin);
            }

            graph.Nodes.Add(newNode);
        }
        
        foreach (var pin in pins.Where(o => o.IsOutput))
        {
            var jsonPin = pinsJson.Find(o => o.Guid == pin.Guid);
            foreach (var link in jsonPin.LinkedTo)
            {
                var otherPin = pins.Find(o => o.Guid == link);
                Utils.Connect(pin, otherPin);
            }
        }
        
        var tunnels = graph.Nodes.Where(o => o is { NodeType: "K2Node_Tunnel" }).ToArray();
        graph.MacroInputNode = tunnels[0];
        graph.MacroOutputNode = tunnels[1];
        
        return graph;

        static GraphPin ConvertPin(JsonPin o, bool isInput)
        {
            GraphPin pin = new(o.PinName, isInput, o.PinType);
            pin.Guid = o.Guid;
            pin.PinFriendlyName = o.PinFriendlyName;
            pin.Value = o.Value;
            pin.IsNameHidden = o.IsNameHidden;
            if (o.IsHidden)
                pin.HidePin();
            return pin;
        }
    }
    
    internal class JsonExport
    {
        public required string Name;
        public required List<JsonNode> Nodes;
    }

    internal class JsonPin
    {
        public required Guid Guid;
        public required string PinName;
        public required string PinFriendlyName;
        public required EngineBPData.GraphPinType PinType;
        public required string Value;
        public required bool IsNameHidden;
        public required bool IsHidden;
        public required IEnumerable<Guid> LinkedTo;
    }

    internal class JsonNode
    {
        public required string Name;
        public required string NodeType;
        public required int LocationX;
        public required int LocationY;
        public required int NodeWidth;
        public required int NodeHeight;
        public required bool HeaderHidden;
        public required bool HeaderCenter;
        public required bool ShowNameAsBody;
        public required bool Pure;
        public required IEnumerable<JsonPin> Input;
        public required IEnumerable<JsonPin> Output;
    }
}
