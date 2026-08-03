using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using UEBlueprintGraphViewer.ViewModels;

namespace UEBlueprintGraphViewer.Views.Controls;

public partial class GraphViewSearch : UserControl
{
    private EditorViewModel? _vm;
    public GraphViewSearch()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        _vm = DataContext as EditorViewModel;
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _vm?.IsSearchVisible = false;
        (Parent as Control)?.Focus();
    }

    private void NextButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _vm?.SearchResultIndex += 1;
        if (_vm?.SearchResultIndex >= _vm?.SearchResult.Count)
            _vm?.SearchResultIndex = 0;
    }

    private void BackButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _vm?.SearchResultIndex -= 1;
        if (_vm?.SearchResultIndex < 0)
            _vm?.SearchResultIndex = _vm.SearchResult.Count - 1;
    }

    private void InputElement_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            NextButton_OnClick(sender, e);
        }
    }

    private void Root_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty && IsVisible)
            SearchTextBox.Focus();
    }
}