using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using UEBlueprintGraphViewer.ViewModels;

namespace UEBlueprintGraphViewer;

public partial class MacroViewerWindow : Window
{
    private string selectedMacroName = "";
    private BPGraph macroGraph;
    
    public MacroViewerWindow()
    {
        InitializeComponent();
        LoadList();
    }

    public MacroViewerWindow(string name, BPGraph graph) : this()
    {
        selectedMacroName = name;
        LoadGraph(graph);
    }

    private void LoadList()
    {
        MacroList.ItemsSource = Settings.Instance.Macros.Keys;
    }
    
    private void MacroList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        selectedMacroName = MacroList.SelectedItem as string ?? "";
        LoadGraph(Settings.Instance.Macros.GetValueOrDefault(selectedMacroName) ?? new());
    }

    private void LoadGraph(BPGraph graph)
    {
        macroGraph = graph;
        EditorViewModel vm = new();
        vm.Graph = graph;
        vm.AddNodes(graph.Nodes);
        Viewer.Editor = vm;
    }

    private void SaveSelectedButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Settings.SaveMacro(selectedMacroName, macroGraph);
    }
    
    private void ReloadButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Settings.LoadMacros(Settings.Instance);
        LoadList();
        LoadGraph(Settings.Instance.Macros.GetValueOrDefault(selectedMacroName) ?? new());
    }

    private void OpenFolderButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo()
        {
            FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Macros"),
            UseShellExecute = true,
            Verb = "open"
        });
    }
}