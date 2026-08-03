using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CUE4Parse.UE4.Objects.UObject;
using UEBlueprintGraphViewer.Assets;
using UEBlueprintGraphViewer.ViewModels;

namespace UEBlueprintGraphViewer.Views
{
    public partial class AssetInfoPanel : UserControl
    {
        public event FunctionSelectedHandler? FunctionChoosen;
        public delegate void FunctionSelectedHandler(AssetFunctionViewModel func);
        
        public EventHandler<string>? OnPropertySearchInCurrentGraph;
        public EventHandler<string>? OnFunctionSearchInCurrentGraph;

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
            FunctionChoosen?.Invoke(lastSelected);
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
    }
}
