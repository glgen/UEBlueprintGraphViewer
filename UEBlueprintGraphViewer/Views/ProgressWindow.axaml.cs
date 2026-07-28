using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UEBlueprintGraphViewer;

public partial class ProgressWindow : Window
{
    ProgressWindowViewModel vm = new();

    public ProgressWindow()
    {
        InitializeComponent();
        DataContext = vm;
    }

    public ProgressWindow(string title, string text) : this()
    {
        Title = title;
        StatusRun.Text = text;
    }

    public async void Open(Window parent) => await ShowDialog(parent);
    protected override void OnClosing(WindowClosingEventArgs e) => e.Cancel = !e.IsProgrammatic;

    public void Update(int count, int countMax)
    {
        vm.ProgressValue = count;
        vm.ProgressMax = countMax;
    }
}

public partial class ProgressWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private int progressValue;

    [ObservableProperty]
    private int progressMax;
}