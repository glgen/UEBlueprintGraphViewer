using System;
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
        
        public GraphPin? SelectedPin { get; set; }

        [ObservableProperty]
        private bool _disableMoving;

        [ObservableProperty]
        private Point _viewportLocation;

        [ObservableProperty]
        private Size _viewportSize;

        [ObservableProperty]
        private double _viewportZoom = 1;
        
        [ObservableProperty]
        private bool _isSearchVisible;
        
        [ObservableProperty]
        private string _searchTerm;
        
        [ObservableProperty]
        private bool _isSearchingFunction;
        
        [ObservableProperty]
        private bool _isSearchingVariable;
        
        [ObservableProperty]
        private bool _isSearchExact;
        
        [ObservableProperty]
        private List<BPNode> _searchResult = [];
        
        [ObservableProperty]
        private int _searchResultIndex;

        [ObservableProperty]
        private string _detailsPinText;
        
        [ObservableProperty]
        private string _detailsNodeText;
        
        [ObservableProperty]
        private List<BPNode> _debuggerBreakpoints = [];
        
        [ObservableProperty]
        private string _debuggerStack;
        
        [ObservableProperty]
        private string _debuggerContextObject;
        
        [ObservableProperty]
        private List<AssetPropertyViewModel> _debuggerLocals = [];
        
        [ObservableProperty]
        private BPNode? _currentDebuggerNode;

        
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
        
        partial void OnSearchTermChanged(string? oldValue, string newValue) => UpdateSearchResult();

        partial void OnIsSearchingFunctionChanged(bool value) => UpdateSearchResult();

        partial void OnIsSearchingVariableChanged(bool value) => UpdateSearchResult();

        partial void OnIsSearchExactChanged(bool value) => UpdateSearchResult();

        private void UpdateSearchResult()
        {
            SearchResultIndex = 0;
            
            if (string.IsNullOrEmpty(SearchTerm))
            {
                SearchResult = [];
                return;
            }
            
            SearchResult = Nodes.Where(o =>
            {
                if (IsSearchingFunction && o is K2Node_CallFunction or K2Node_FunctionEntry && Compare(o.Name))
                    return true;
                if (IsSearchingVariable && (
                        (o is K2Node_VariableGet get && Compare(get.VarPin.PinFriendlyName)) ||
                        (o is K2Node_VariableSet set && Compare(set.ValuePin.PinFriendlyName))))
                    return true;
                
                if (!IsSearchingVariable && !IsSearchingFunction)
                    return Compare(o.Name) || 
                           o.Input.Any(o => !o.IsHidden && (Compare(o.PinFriendlyName) || Compare(o.Value))) ||
                           o.Output.Any(o => !o.IsHidden && Compare(o.PinFriendlyName));

                return false;
            }).ToList();

            bool Compare(string source)
            {
                if (IsSearchExact)
                    return source == SearchTerm;
                return source.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}