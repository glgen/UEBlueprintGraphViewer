using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using UEBlueprintGraphViewer.Assets;

namespace UEBlueprintGraphViewer.Views;

public partial class AssetReferencesResultView : UserControl
{
    public AssetReferencesResultView()
    {
        InitializeComponent();
    }

    public AssetReferencesResultView(string text, List<AssetFile> assets) : this()
    {
        MessageText.Text = text;
        ResultListBox.ItemsSource = assets;
    }
    
    private async void InputElement_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Visual { DataContext: AssetFile file })
            await MainWindow.Instance.LoadAsset(file);
    }
}
