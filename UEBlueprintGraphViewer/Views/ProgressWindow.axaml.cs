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

    public void Update(int count, int countMax, string infoText)
    {
        vm.ProgressValue = count;
        vm.ProgressMax = countMax;
        vm.InfoText = infoText;
    }
}

public partial class ProgressWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private int _progressValue;

    [ObservableProperty]
    private int _progressMax;
    
    [ObservableProperty]
    private string _infoText = "";
}