using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using UEBlueprintGraphViewer.Assets;
using UEBlueprintGraphViewer.ReferencesSearch;
using UEBlueprintGraphViewer.ViewModels;

namespace UEBlueprintGraphViewer.Views;

public partial class AssetReferencesResultView : UserControl
{
    public AssetReferencesResultView()
    {
        InitializeComponent();
    }

    public AssetReferencesResultView(string text, List<ReferenceResult> assets) : this()
    {
        MessageText.Text = text;
        ResultListBox.ItemsSource = assets;
    }
    
    private async void InputElement_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Visual { DataContext: ReferenceResult result })
        {
            await MainWindow.Instance.LoadAsset(result.File);
            
            if (!string.IsNullOrEmpty(result.Function))
            {
                if (MainWindow.Instance.AssetsTabs.SelectedItem is TabItem tab &&
                    tab.Content is BPGraphViewer { DataContext: BPGraphViewerViewModel vm } viewer &&
                    vm.Asset is { Asset: not null } asset)
                {
                    var func = asset.Asset.UbergraphFunction?.Name == result.Function
                        ? asset.Asset.SortedEvents.FirstOrDefault()
                        : asset.Asset.Functions.FirstOrDefault(o => o.Name == result.Function);
                    await viewer.OpenFunction(new(result.Function, func!));
                    await Task.Delay(30);
                    if (result.NodeStatementIndex is {} index)
                        viewer.RepositionViewport(index);
                }
            }
        }
    }
}
