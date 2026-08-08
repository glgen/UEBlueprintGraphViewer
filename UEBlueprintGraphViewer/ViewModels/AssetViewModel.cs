using CommunityToolkit.Mvvm.ComponentModel;
using CUE4Parse.UE4.Objects.UObject;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CUE4Parse.UE4.Assets.Exports;
using UEBlueprintGraphViewer.Assets;
using UEBlueprintGraphViewer.Comparing;
using UEBlueprintGraphViewer.Decompiler;
using UEBlueprintGraphViewer.Engine;

namespace UEBlueprintGraphViewer.ViewModels
{
    public partial class AssetViewModel : ObservableObject
    {
        [ObservableProperty]
        private Asset? _asset;

        [ObservableProperty]
        private Asset? _assetCompare1;

        [ObservableProperty]
        private Asset? _assetCompare2;

        [ObservableProperty]
        private string _name;
        
        [ObservableProperty]
        private List<AssetFunctionViewModel> _events;

        [ObservableProperty]
        private List<AssetFunctionViewModel> _functions;

        [ObservableProperty]
        private List<AssetFunctionViewModel> _eventsFiltered;

        [ObservableProperty]
        private List<AssetFunctionViewModel> _functionsFiltered;
        
        [ObservableProperty]
        private List<AssetPropertyViewModel> _properties;
        
        [ObservableProperty]
        private List<AssetPropertyViewModel> _parentProperties;
        
        [ObservableProperty]
        private string _superStruct;
        
        [ObservableProperty]
        private EObjectFlags _flags;
        
        [ObservableProperty]
        private EClassFlags _classFlags;

        private string? _filterText;
        public string? FilterText
        {
            get => _filterText;
            set
            {
                _filterText = value;
                ApplyFilter(_filterText);
            }
        }

        public AssetViewModel(Asset asset)
        {
            Asset = asset;
            Name = asset.ObjectName;
            Events = [.. asset.Events.Select(o => new AssetFunctionViewModel(o.Key, o.Value))];
            Functions = [.. asset.Functions.Select(o => new AssetFunctionViewModel(null, o))];
            asset.LoadAllProperties();
            Properties = [.. asset.LoadedProperties.Values.Select(o => new AssetPropertyViewModel(o))];
            ParentProperties = [.. asset.ParentProperties.Values.Select(o => new AssetPropertyViewModel(o))];
            SuperStruct = asset.SuperStruct;
            EventsFiltered = Events;
            FunctionsFiltered = Functions;
            Flags = asset.GeneratedClass?.Flags ?? EObjectFlags.RF_NoFlags;
            ClassFlags = asset.GeneratedClass?.ClassFlags ?? EClassFlags.CLASS_None;
            ApplyFilter(null);
        }

        public AssetViewModel(Asset? asset1, Asset? asset2)
        {
            AssetCompare1 = asset1;
            AssetCompare2 = asset2;
            
            asset1?.LoadAllProperties();
            asset2?.LoadAllProperties();
            
            List<UFunction> a1F = asset1?.Functions ?? [];
            List<UFunction> a2F = asset2?.Functions ?? [];
            Dictionary<string, UFunction> a1E = asset1?.Events ?? [];
            Dictionary<string, UFunction> a2E = asset2?.Events ?? [];
            List<PropertyData> a1P = asset1?.LoadedProperties.Values.ToList() ?? [];
            List<PropertyData> a2P = asset2?.LoadedProperties.Values.ToList() ?? [];

            var events = a1E.UnionBy(a2E, o => o.Key);
            var functions = a1F.UnionBy(a2F, o => o.Name);
            var properties = a1P.UnionBy(a2P, o => o.Name);

            Events = [];
            Functions = [];
            Properties = [];
            EventsFiltered = Events;
            FunctionsFiltered = Functions;

            foreach (var e in events)
            {
                var f1 = a1E.FirstOrDefault(o => o.Key == e.Key);
                var f2 = a2E.FirstOrDefault(o => o.Key == e.Key);
                Events.Add(new(f1.Value != null ? f1.Key : f2.Key, f1.Value, f2.Value));
            }

            foreach (var f in functions)
            {
                var f1 = a1F.Find(o => o.Name == f.Name);
                var f2 = a2F.Find(o => o.Name == f.Name);
                Functions.Add(new(null, f1, f2));
            }
            
            foreach (var p in properties)
            {
                var p1 = a1P.Find(o => o.Name == p.Name);
                var p2 = a2P.Find(o => o.Name == p.Name);
                Properties.Add(new(p1, p2));
            }

            if (asset1 != null && asset2 != null)
                CheckWithDecompiler(asset1, asset2);

            ApplyFilter(null);
        }

        private void ApplyFilter(string? filter)
        {
            if (string.IsNullOrEmpty(filter))
            {
                EventsFiltered = Events;
                FunctionsFiltered = Functions;
                return;
            }

            EventsFiltered = [.. Events.Where(o => o.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase))];
            FunctionsFiltered = [.. Functions.Where(o => o.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase))];
        }

        private async void CheckWithDecompiler(Asset asset1, Asset asset2)
        {
            var firstEvent1 = asset1.SortedEvents.FirstOrDefault();
            var firstEvent2 = asset2.SortedEvents.FirstOrDefault();
            if (firstEvent1 != null && firstEvent2 != null)
            {
                var context1 = new GlobalDecompilerContext(asset1, Settings.Instance.CompareGame1!, firstEvent1) { IsParsingMacros = false };
                var context2 = new GlobalDecompilerContext(asset2, Settings.Instance.CompareGame2!, firstEvent2) { IsParsingMacros = false };
                var decompiler1 = new FunctionDecompiler(context1);
                var decompiler2 = new FunctionDecompiler(context2);
                var task1 = decompiler1.DecompileAsync(null);
                var task2 = decompiler2.DecompileAsync(null);
                await Task.WhenAll(task1, task2);
                var names = BPGraph.TestUbergraphEquality(decompiler1.Graph, decompiler2.Graph);
                foreach (var name in names)
                {
                    Events.Find(o => o.ToString() == name)!.ChangeStatus = ChangeStatus.Changed;
                }
            }

            foreach (var func in Functions)
            {
                if (func.ChangeStatus != ChangeStatus.None || func.FunctionCompare1 is null || func.FunctionCompare2 is null)
                    continue;
                
                var context1 = new GlobalDecompilerContext(asset1, Settings.Instance.CompareGame1!, func.FunctionCompare1) { IsParsingMacros = false };
                var context2 = new GlobalDecompilerContext(asset2, Settings.Instance.CompareGame2!, func.FunctionCompare2) { IsParsingMacros = false };
                var decompiler1 = new FunctionDecompiler(context1);
                var decompiler2 = new FunctionDecompiler(context2);
                var task1 = decompiler1.DecompileAsync(null);
                var task2 = decompiler2.DecompileAsync(null);
                await Task.WhenAll(task1, task2);
                if (!BPGraph.IsEquals(decompiler1.Graph, decompiler2.Graph))
                    func.ChangeStatus = ChangeStatus.Changed;
            }
        }
    }

    public partial class AssetFunctionViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _name;
        
        [ObservableProperty]
        private UFunction? _function;

        [ObservableProperty]
        private UFunction? _functionCompare1;

        [ObservableProperty]
        private UFunction? _functionCompare2;

        [ObservableProperty]
        private ChangeStatus _changeStatus;

        [ObservableProperty]
        private EFunctionFlags _flags;
        
        [ObservableProperty]
        private List<AssetPropertyViewModel> _properties;
        
        [ObservableProperty]
        private List<AssetPropertyViewModel> _inputs;
        
        [ObservableProperty]
        private List<AssetPropertyViewModel> _outputs;
        
        [ObservableProperty]
        private List<AssetPropertyViewModel> _locals;
        

        public AssetFunctionViewModel(string? name, UFunction func)
        {
            Function = func;
            Name = name ?? Function.Name;
            Flags = Function.FunctionFlags;
            Properties = func.ChildProperties.Select(o =>
                new AssetPropertyViewModel(new(o as FProperty, func))).ToList();
            Inputs = Properties.Where(o => o.Property.IsInputParam()).ToList();
            Outputs = Properties.Where(o => o.Property.IsOutParam()).ToList();
            Locals = Properties.Where(o => !o.Property.IsFunctionParam()).ToList();
        }

        public AssetFunctionViewModel(string? name, UFunction? func1, UFunction? func2)
        {
            Name = name ?? (func1 != null ? func1.Name : func2?.Name ?? "None");
            FunctionCompare1 = func1;
            FunctionCompare2 = func2;
            if (func1 == null)
            {
                ChangeStatus = ChangeStatus.Added;
            }
            else if (func2 == null)
            {
                ChangeStatus = ChangeStatus.Removed;
            }
            else
            {
                if (func1.ScriptBytecode.Length != func2.ScriptBytecode.Length) // TODO: more checks
                    ChangeStatus = ChangeStatus.Changed;
            }
        }

        public override string ToString() => Name;
    }
    
    public partial class AssetPropertyViewModel : ObservableObject
    {
        [ObservableProperty]
        private PropertyData _property;
        
        [ObservableProperty]
        private string _name;
        
        [ObservableProperty]
        private EngineBPData.GraphPinType _type;
        
        [ObservableProperty]
        private EPropertyFlags _flags;
        
        [ObservableProperty]
        private string _defaultValue;
        
        public ChangeStatus ChangeStatus { get; set; }

        public AssetPropertyViewModel(PropertyData prop)
        {
            Property = prop;
            Name = Property.Name;
            Type = Property.PinType;
            Flags = Property.Flags;
            DefaultValue = Property.DefaultValue;
        }

        public AssetPropertyViewModel(PropertyData? prop1, PropertyData? prop2)
        {
            Name = prop1?.Name ?? prop2?.Name ?? "None";
            Type = prop1?.PinType ?? prop2?.PinType ?? new();
            if (prop1 == null)
            {
                ChangeStatus = ChangeStatus.Added;
            }
            else if (prop2 == null)
            {
                ChangeStatus = ChangeStatus.Removed;
            }
            else
            {
                if (prop1.PinType.PinCategory != prop2.PinType.PinCategory ||
                    prop1.PinType.PinSubCategory != prop2.PinType.PinSubCategory ||
                    prop1.PinType.ContainerType != prop2.PinType.ContainerType) // TODO: more checks
                    ChangeStatus = ChangeStatus.Changed;
            }
        }

        public override string ToString() => Name;
    }
}
