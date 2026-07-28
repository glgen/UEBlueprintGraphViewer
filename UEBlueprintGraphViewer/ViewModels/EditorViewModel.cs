using System.Collections.Generic;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Linq;
using UEBlueprintGraphViewer.Nodes;
using UEBlueprintGraphViewer.ViewModels.Observable;

namespace UEBlueprintGraphViewer.ViewModels
{
    public partial class EditorViewModel : ObservableObject
    {
        public BPGraph? Graph;
        
        public NodifyObservableCollection<BPNode> Nodes { get; } = [];

        public List<ConnectionViewModel> Connections { get; } = [];

        public List<BPNode> SelectedNodes { get; } = [];
        
        public GraphPin? MouseOverPin { get; set; }

        [ObservableProperty]
        private bool _disableMoving;

        [ObservableProperty]
        private Point _viewportLocation;

        [ObservableProperty]
        private Size _viewportSize;

        [ObservableProperty]
        private double _viewportZoom = 1;

        public void RemoveNode(BPNode node)
        {
            Nodes.Remove(node);
            Graph?.RemoveNode(node);
            Connections.RemoveAll(o => o.Source.ParentNode == node || o.Target.ParentNode == node);
        }

        public void AddNodes(IEnumerable<BPNode> nodes)
        {
            foreach (var node in nodes)
                AddNode(node);
        }
        
        public void AddNode(BPNode node)
        {
            Nodes.Add(node);

            foreach (GraphPin pin in node.Output)
            {
                foreach (GraphPin pinToConnect in pin.LinkedTo)
                    Connect(pin, pinToConnect);
            }

            foreach (GraphPin pin in node.Input)
            {
                foreach (GraphPin pinToConnect in pin.LinkedTo)
                    Connect(pinToConnect, pin);
            }
        }

        public void ClearGraph()
        {
            Nodes.Clear();
            Connections.Clear();
        }
        
        private void Connect(GraphPin from, GraphPin to)
        {
            if (!Connections.Any(x => x.Source == from && x.Target == to))
                Connections.Add(new ConnectionViewModel(from, to));
        }
    }
}