using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Platform.Storage;
using CUE4Parse.UE4.Versions;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using UEBlueprintGraphViewer.Nodes;

namespace UEBlueprintGraphViewer;

public partial class AddNodeMenu : UserControl
{
    private static List<AddNodeMenuItemViewModel> items =
    [
        new("Node spawning is not available yet", typeof(K2Node_CallFunction)),
        new("-", typeof(K2Node_CallFunction)),
        new("-", typeof(K2Node_CallFunction)),
        new("-", typeof(K2Node_CallFunction)),
    ];
    
    
    public AddNodeMenu()
    {
        InitializeComponent();
        
        DataList.ItemsSource = items;
    }

    private void DataList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        //Activator.CreateInstance((DataList.SelectedItem as AddNodeMenuItemViewModel).Type);
    }
}

public class AddNodeMenuItemViewModel
{
    public string Name { get; set; }
    public Type Type { get; set; }

    public AddNodeMenuItemViewModel(string name, Type type)
    {
        Name = name;
        Type = type;
    }
}