using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Platform.Storage;
using CUE4Parse.UE4.Versions;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Avalonia.Input.Platform;
using CUE4Parse.UE4.Objects.UObject;
using UEBlueprintGraphViewer.Assets;
using UEBlueprintGraphViewer.Engine;
using UEBlueprintGraphViewer.Nodes;
using UEBlueprintGraphViewer.Views;

namespace UEBlueprintGraphViewer;

public partial class NodeContextMenu : UserControl
{
    private GraphView2 editor;
    private Flyout flyout;
    
    public NodeContextMenu()
    {
        InitializeComponent();
    }

    public NodeContextMenu(GraphView2 editor, Flyout flyout) : this()
    {
        this.editor = editor;
        this.flyout = flyout;

        FollowPin.IsEnabled = editor.Editor?.MouseOverPin?.LinkedTo.Count == 1;
        CopyPinValue.IsEnabled = editor.Editor?.MouseOverPin?.LinkedTo.Count == 0 &&
                                 editor.Editor?.MouseOverPin?.Direction == EngineEnums.EEdGraphPinDirection.EGPD_Input;
    }

    private void FollowPin_OnClick(object? sender, RoutedEventArgs e)
    {
        if (editor.Editor?.MouseOverPin?.LinkedTo.FirstOrDefault() is {} pin)
            editor.Autopanner.PanToCentered(new Point(pin.X, pin.Y));
        flyout.Hide();
    }
    
    private void CopyPinValue_OnClick(object? sender, RoutedEventArgs e)
    {
        var value = editor.Editor?.MouseOverPin?.Value;
        if (!string.IsNullOrWhiteSpace(value) && TopLevel.GetTopLevel(this) is {} top)
            top.Clipboard?.SetTextAsync(value);
        flyout.Hide();
    }

    private BPGraphViewer? GetBPGraphViewer()
    {
        return ((MainWindow.Instance.AssetsTabs.SelectedItem as TabItem)?.Content as BPGraphViewer);
    }
    
    private void OpenInJson_OnClick(object? sender, RoutedEventArgs e)
    {
        GetBPGraphViewer()?.OpenInDisassemblyButton_Click(sender, e);
    }
    
    private async void ToggleBreakpoint_OnClick(object? sender, RoutedEventArgs e)
    {
        if (editor.Editor?.SelectedNodes.FirstOrDefault() is {} node && MainWindow.DebuggerOutput != null)
        {
            await GetBPGraphViewer()!.ToggleBreakpoint(node);
            flyout.Hide();
        }
    }
    
    private async void CollapseToMacroButton_OnClick(object? sender, RoutedEventArgs e)
    {
        string macroName = "";
        while (string.IsNullOrWhiteSpace(macroName))
        {
            DialogWindow window = await DialogWindow.Show("New macro name:", "New macro", true, true);
            macroName = window.EnteredText;

            if (window.Result == DialogWindowResult.Cancel)
                return;
        }
        BPGraph macroGraph = BPGraph.ToMacro(editor.Editor?.SelectedNodes ?? [], macroName);
        Settings.Instance.Macros.Add(macroName, macroGraph);
        Settings.SaveMacro(Utils.ToValidFileName(macroName), macroGraph);
        await new MacroViewerWindow(macroName, macroGraph).ShowDialog(MainWindow.Instance);
        
        // decompile selected function again
        ((MainWindow.Instance.AssetsTabs.SelectedItem as TabItem)?.Content as BPGraphViewer)?.AssetInfoPanel.InvokeSelected();
    }

    private void DeleteButton_OnClick(object? sender, RoutedEventArgs e)
    {
        foreach (var node in editor.Editor?.SelectedNodes ?? [])
        {
            editor.Editor?.RemoveNode(node);
        }
        editor.Editor?.SelectedNodes.Clear();
        flyout.Hide();
    }
}