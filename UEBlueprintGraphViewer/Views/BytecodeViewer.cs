using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaEdit.TextMate;
using CommunityToolkit.Mvvm.ComponentModel;
using CUE4Parse.Utils;
using TextMateSharp.Grammars;

namespace UEBlueprintGraphViewer.Views
{
    public partial class BytecodeViewer : UserControl
    {
        public static readonly StyledProperty<bool> IsNodeDetailsProperty =
            AvaloniaProperty.Register<BytecodeViewer, bool>(nameof(IsNodeDetails));

        public bool IsNodeDetails
        {
            get => GetValue(IsNodeDetailsProperty);
            set => SetValue(IsNodeDetailsProperty, value);
        }

        readonly BytecodeViewerViewModel viewModel = new();

        public BytecodeViewer()
        {
            InitializeComponent();
            DataContext = viewModel;
            
            Editor.Options.HighlightCurrentLine = true;
            
            var registryOptions = new RegistryOptions(ThemeName.DarkPlus);
            var textMateInstallation = Editor.InstallTextMate(registryOptions);
            Language csharpLanguage = registryOptions.GetLanguageByExtension(".json");
            textMateInstallation.SetGrammar(registryOptions.GetScopeByLanguageId(csharpLanguage.Id));
            
            Editor.TextArea.SelectionBrush = new SolidColorBrush(new Color(255, 60, 60, 60));
            // Editor.TextArea.TextView.CurrentLineBackground = brush;
            // Editor.TextArea.TextView.CurrentLineBorder = new Pen(brush); 
        }

        private void JumpToIndexButton_Click(object sender, RoutedEventArgs e)
        {
            if (viewModel.JumpToIndex != null)
                JumpToInstr(viewModel.JumpToIndex.Value);
        }
        
        public void JumpToInstr(int index)
        {
            var before = viewModel.Json.SubstringBefore($"\"StatementIndex\": {index},");
            Editor.ScrollToLine(before.Count('\n'));
            Editor.Select(before.Length, 0);
        }

        public void SetBytecode(string? bytecode)
        {
            viewModel.Json = bytecode;
            Editor.Text = bytecode;
        }

    }

    public partial class BytecodeViewerViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _json;

        [ObservableProperty]
        private int? jumpToIndex;
    }
}