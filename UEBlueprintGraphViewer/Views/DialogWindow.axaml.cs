using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Threading.Tasks;

namespace UEBlueprintGraphViewer;

public enum DialogWindowResult
{
    Ok,
    Cancel,
}

public partial class DialogWindow : Window
{
    public string MessageText { get; set; }
    public string EnteredText { get; set; }
    public bool AskForText { get; set; }
    public bool CanCancel { get; set; }
    public DialogWindowResult Result = DialogWindowResult.Cancel;

    public DialogWindow()
    {
        InitializeComponent();
    }

    public DialogWindow(string message, string title, bool canCancel = false, bool askForText = false)
    {
        MessageText = message;
        AskForText = askForText;
        CanCancel = canCancel;
        MaxHeight = Screens.Primary?.WorkingArea.Height - 150 ?? double.PositiveInfinity;
        InitializeComponent();
        Title = title;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (AskForText)
            ValueTextBox.Focus();
    }

    public void Ok_Click(object sender, RoutedEventArgs e)
    {
        Result = DialogWindowResult.Ok;
        Close();
    }

    public void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Result = DialogWindowResult.Cancel;
        Close();
    }

    public async static Task<DialogWindow> Show(string message, string title, bool canCancel = false, bool askForText = false, Window? windowOverride = null)
    {
        var window = new DialogWindow(message, title, canCancel, askForText);
        await window.ShowDialog(windowOverride ?? App.MainWindow);
        return window;
    }
}