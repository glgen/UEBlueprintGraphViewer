using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Objects.UObject;
using UEBlueprintGraphViewer.Assets;
using UEBlueprintGraphViewer.ReferencesSearch;
using UEBlueprintGraphViewer.ViewModels;

namespace UEBlueprintGraphViewer.Views
{
    public partial class AssetInfoPanel : UserControl
    {
        public event EventHandler<AssetFunctionViewModel>? FunctionChoosen;
        
        public EventHandler<string>? OnPropertySearchInCurrentGraph;
        public EventHandler<string>? OnFunctionSearchInCurrentGraph;
        
        public EventHandler<object>? OnObjectSelected;

        private AssetFunctionViewModel lastSelected;

        public AssetInfoPanel()
        {
            InitializeComponent();
        }

        private void FunctionList_DoubleTapped(object sender, TappedEventArgs e)
        {
            if (sender is Visual { DataContext: AssetFunctionViewModel func } && DataContext is AssetViewModel asset)
            {
                lastSelected = func;
                InvokeSelected();
            }
        }
        
        private void PropertyList_DoubleTapped(object sender, TappedEventArgs e)
        {
            if (sender is Visual { DataContext: AssetPropertyViewModel prop } && DataContext is AssetViewModel asset)
            {
                
            }
        }

        public void InvokeSelected()
        {
            FunctionChoosen?.Invoke(this, lastSelected);
        }
        
        private void PropertySearchInCurrentGraph_OnClick(object? sender, RoutedEventArgs e)
        {
            if (PropertyList.SelectedItem is AssetPropertyViewModel prop)
                OnPropertySearchInCurrentGraph?.Invoke(this, prop.Name);
        }
        
        private void EventSearchInCurrentGraph_OnClick(object? sender, RoutedEventArgs e)
        {
            if (EventList.SelectedItem is AssetFunctionViewModel func)
                OnFunctionSearchInCurrentGraph?.Invoke(this, func.Name);
        }
        
        private void FunctionSearchInCurrentGraph_OnClick(object? sender, RoutedEventArgs e)
        {
            if (FunctionList.SelectedItem is AssetFunctionViewModel func)
                OnFunctionSearchInCurrentGraph?.Invoke(this, func.Name);
        }
        
        private async void FunctionSearchGlobal_OnClick(object? sender, RoutedEventArgs e)
        {
            if (Settings.Instance.IsInCompareMode)
            {
                await DialogWindow.Show("Finding assets references is not available in compare mode", "Search");
                return;
            }
            
            if (sender is Visual { DataContext: AssetFunctionViewModel func } && DataContext is AssetViewModel asset)
            {
                var dialog = new ProgressWindow("Search", "Finding references:");
                dialog.Open(MainWindow.Instance);
                var result = await ReferencesSearcher.FindFunctionReferences(MainWindow.Package, Settings.Instance.Game, asset.Asset, func.Name, dialog.Update);
                MainWindow.Instance.AddTab(new()
                {
                    Header = $"References of {asset.Asset.ObjectName}:{func.Name}",
                    Classes = { "Closeable" },
                    Content = new AssetReferencesResultView($"Found {result.Length} references of {asset.Asset.ObjectName}:{func.Name}",
                        result.Select(o => new ReferenceResult()
                        {
                            File = new AssetFile(o.Item1.Name, o.Item1.Path),
                            Function = o.Item2,
                            NodeStatementIndex = o.Item3
                        }).ToList())
                });
                
                dialog.Close();
            }
        }
        
        private async void PropertySearchGlobal_OnClick(object? sender, RoutedEventArgs e)
        {
            if (Settings.Instance.IsInCompareMode)
            {
                await DialogWindow.Show("Finding references is not available in compare mode", "Search");
                return;
            }
            
            if (sender is Visual { DataContext: AssetPropertyViewModel prop } && DataContext is AssetViewModel asset)
            {
                var dialog = new ProgressWindow("Search", "Finding references:");
                dialog.Open(MainWindow.Instance);
                var result = await ReferencesSearcher.FindPropertyReferences(MainWindow.Package, Settings.Instance.Game, asset.Asset, prop.Name, dialog.Update);
                MainWindow.Instance.AddTab(new()
                {
                    Header = $"References of {asset.Asset.ObjectName}:{prop.Name}",
                    Classes = { "Closeable" },
                    Content = new AssetReferencesResultView($"Found {result.Length} references of {asset.Asset.ObjectName}:{prop.Name}",
                        result.Select(o => new ReferenceResult()
                        {
                            File = new AssetFile(o.Item1.Name, o.Item1.Path),
                            Function = o.Item2,
                            NodeStatementIndex = o.Item3
                        }).ToList())
                });
                
                dialog.Close();
            }
        }

        private void EventList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                OnObjectSelected?.Invoke(sender, e.AddedItems[0]);
                FunctionList.SelectedIndex = -1;
                PropertyList.SelectedIndex = -1;
            }
        }

        private void FunctionList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                OnObjectSelected?.Invoke(sender, e.AddedItems[0]);
                EventList.SelectedIndex = -1;
                PropertyList.SelectedIndex = -1;
            }
        }

        private void PropertyList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                OnObjectSelected?.Invoke(sender, e.AddedItems[0]);
                EventList.SelectedIndex = -1;
                FunctionList.SelectedIndex = -1;
            }
        }
    }
}
