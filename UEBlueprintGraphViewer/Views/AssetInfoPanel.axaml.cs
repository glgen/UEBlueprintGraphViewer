using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using CUE4Parse.UE4.Objects.UObject;
using UEBlueprintGraphViewer.Assets;
using UEBlueprintGraphViewer.ViewModels;

namespace UEBlueprintGraphViewer.Views
{
    public partial class AssetInfoPanel : UserControl
    {
        public event FunctionSelectedHandler? FunctionChoosen;
        public delegate void FunctionSelectedHandler(AssetFunctionViewModel func);

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
    }
}
