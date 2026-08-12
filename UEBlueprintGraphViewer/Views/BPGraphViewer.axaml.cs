using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;
using CUE4Parse.UE4.Objects.UObject;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using CUE4Parse.Utils;
using UEBlueprintGraphViewer.Assets;
using UEBlueprintGraphViewer.Decompiler;
using UEBlueprintGraphViewer.Engine;
using UEBlueprintGraphViewer.Nodes;
using UEBlueprintGraphViewer.ViewModels;

namespace UEBlueprintGraphViewer.Views
{
    public partial class BPGraphViewer : UserControl
    {
        public EditorViewModel GraphViewModel { get; set; } = new();
        public EditorViewModel GraphViewModel2 { get; set; } = new();
        public string Json;

        public BPGraphViewerViewModel VM { get; } = new();

        BPGraph _graph;
        BPGraph _graph2;

        UFunction? _lastFunc;

        public BPGraphViewer()
        {
            InitializeComponent();

            DataContext = VM;
            
            FirstViewer.OnSelectionChanged += () => GraphEditor_SelectionChanged(FirstViewer);
            SecondViewer.OnSelectionChanged += () => GraphEditor_SelectionChanged(SecondViewer);
            FirstViewer.OnPanned += delta => PanAnotherViewer(delta, FirstViewer, SecondViewer);
            SecondViewer.OnPanned += delta => PanAnotherViewer(delta, SecondViewer, FirstViewer);
            
            AssetInfoPanel.OnPropertySearchInCurrentGraph += OnPropertySearchInCurrentGraph;
            AssetInfoPanel.OnFunctionSearchInCurrentGraph += OnFunctionSearchInCurrentGraph;
            AssetInfoPanel.OnObjectSelected += OnObjectSelected;
            
            UpdateSecondViewerState();
        }

        private void OnObjectSelected(object? sender, object e)
        {
            VM.SelectedObject = e;
        }

        private void OnPropertySearchInCurrentGraph(object? sender, string e)
        {
            GraphViewModel.IsSearchExact = true;
            GraphViewModel.IsSearchingVariable = true;
            GraphViewModel.IsSearchingFunction = false;
            GraphViewModel.SearchTerm = e;
            GraphViewModel.IsSearchVisible = true;
        }
        
        private void OnFunctionSearchInCurrentGraph(object? sender, string e)
        {
            GraphViewModel.IsSearchExact = true;
            GraphViewModel.IsSearchingVariable = false;
            GraphViewModel.IsSearchingFunction = true;
            GraphViewModel.SearchTerm = e;
            GraphViewModel.IsSearchVisible = true;
        }

        public BPGraphViewer(AssetViewModel asset) : this()
        {
            VM.Asset = asset;
        }

        private bool _autoOpened;

        protected override async void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            
            if (_autoOpened) return;
            _autoOpened = true;

            var target = VM.Asset.Events.FirstOrDefault() ?? VM.Asset.Functions.FirstOrDefault();
            if (target != null)
            {
                AssetInfoPanel.SelectFunction(target);
                await OpenFunction(target);
            }
        }

        public void DisableProgress()
        {
            VM.IsProgressVisible = false;
        }

        public void SetProgressStateName(string name)
        {
            VM.IsProgressVisible = true;
            VM.ProgressMessage = name;
        }

        public void Clear()
        {
            GraphViewModel.ClearGraph();
            GraphViewModel2.ClearGraph();
        }

        public void AddNodesToLoad(BPGraph graph1, BPGraph graph2)
        {
            GraphViewModel.Graph = graph1;
            GraphViewModel.AddNodes(graph1.Nodes);
            GraphViewModel2.Graph = graph2;
            GraphViewModel2.AddNodes(graph2.Nodes);
        }

        public void UpdateProgress(int count, int countMax)
        {
            VM.ProgressValue = count;
            VM.ProgressMax = countMax;
        }

        public void RepositionViewport(GraphView2 view, BPNode? functionStart)
        {
            view.Autopanner.PanToCentered(new Point(functionStart?.X ?? 0, functionStart?.Y ?? 0));
        }

        public void RepositionViewport(int statementIndex)
        {
            var n = _graph.Nodes.FirstOrDefault(o => o.StatementIndex == statementIndex);
            FirstViewer.Autopanner.PanToCentered(new Point(n?.X ?? 0, n?.Y ?? 0));
        }

        public async void AssetInfoPanel_FunctionChoosen(object sender, AssetFunctionViewModel func)
        {
            OpenFunction(func);
        }

        public async Task OpenFunction(AssetFunctionViewModel func)
        {
            VM.CurrentFunction = func;
            if (Settings.Instance.IsInCompareMode)
            {
                await DecompileFunctionCompare(func);
            }
            else
            {
                await DecompileFunction(func);
            }
        }

        public async Task DecompileFunction(AssetFunctionViewModel func)
        {
            if (!VM.Asset.Asset.IsEvent(_lastFunc) || !VM.Asset.Asset.IsEvent(func.Function))
                await Decompile(func);

            _lastFunc = func.Function;

            RepositionViewport(FirstViewer, _graph.FindFuncStartNode(func.Name));
            RepositionViewport(SecondViewer, _graph2.FindFuncStartNode(func.Name));
        }

        public async Task DecompileFunctionCompare(AssetFunctionViewModel func)
        {
            await Decompile(func);

            RepositionViewport(FirstViewer, _graph.FindFuncStartNode(func.Name));
            RepositionViewport(SecondViewer, _graph2.FindFuncStartNode(func.Name));
        }

        private async Task Decompile(AssetFunctionViewModel func)
        {
            Clear();

            SetProgressStateName("Decompiling...");

            if (func.Function != null)
            {
                var decompiler = new FunctionDecompiler(VM.Asset.Asset, Settings.Instance.Game, func.Function);
                await DecompileAndCheck(func, decompiler);
                _graph = decompiler.Graph;
                _graph2 = new();
                Json = JsonConvert.SerializeObject(decompiler.GlobalContext.CurrentFunction, Formatting.Indented);
            }
            else
            {
                _graph = new();
                if (func.FunctionCompare1 != null && VM.Asset.AssetCompare1 != null)
                {
                    var decompiler = new FunctionDecompiler(VM.Asset.AssetCompare1, Settings.Instance.CompareGame1!, func.FunctionCompare1);
                    await DecompileAndCheck(func, decompiler);
                    _graph = decompiler.Graph;
                }

                _graph2 = new();
                if (func.FunctionCompare2 != null && VM.Asset.AssetCompare2 != null)
                {
                    var decompiler = new FunctionDecompiler(VM.Asset.AssetCompare2, Settings.Instance.CompareGame2!, func.FunctionCompare2);
                    await DecompileAndCheck(func, decompiler);
                    _graph2 = decompiler.Graph;
                }

                BPGraph.Compare(_graph, _graph2);
                Json = "{}";
            }

            SetProgressStateName("Building graph layout...");

            var task1 = _graph.LayoutNodesMsaglAsync(null);
            var task2 = _graph2.LayoutNodesMsaglAsync(null);
            await Task.WhenAll(task1, task2);

            FirstViewer.Editor = GraphViewModel;
            SecondViewer.Editor = GraphViewModel2;
            AddNodesToLoad(_graph, _graph2);
            InstructionsViewer.SetBytecode(Json);
            DisableProgress();
        }

        private async Task DecompileAndCheck(AssetFunctionViewModel func, FunctionDecompiler decompiler)
        {
            var result = await decompiler.DecompileAsync(UpdateProgress);
            await CheckDecompilationResult(func.ToString(), result);
        }

        private static async Task CheckDecompilationResult(string name, DecompilationResult result)
        {
            if (!result.IsSuccessful)
            {
                using StringWriter sw = new();
                await sw.WriteLineAsync($"Decompiling function {name} failed:\n");
                foreach (var problem in result.Problems)
                {
                    int index = problem.Context?.GetInstr().StatementIndex ?? -1;
                    await sw.WriteLineAsync($"{problem.Message}\nAt statement index {index}\n");
                }
                await DialogWindow.Show(sw.ToString(), "Decompiling error");
            }
        }

        public void UpdateSecondViewerState()
        {
            List<Control> children = [.. ViewersGrid.Children];

            ViewersGrid.Children.Clear();

            SecondViewer.IsVisible = Settings.Instance.IsInCompareMode;
            ViewersGrid.RowDefinitions.Clear();
            if (Settings.Instance.IsInCompareMode)
                ViewersGrid.RowDefinitions.AddRange([new(), new(5, GridUnitType.Pixel), new()]);

            ViewersGrid.Children.AddRange(children);
        }

        public void GraphEditor_SelectionChanged(GraphView2 view)
        {
            VM.SelectedObject = null;
            VM.SelectedObject = GraphViewModel;
            var node = view.Editor?.SelectedNodes.FirstOrDefault();
            view.Editor?.DetailsNodeText = node == null ? "" :
                            $"StatementIndex: {node.StatementIndex}\n" +
                            $"NodeType: {node.NodeType}\n" +
                            $"NodeWidth: {node.NodeWidth}\n" +
                            $"NodeHeight: {node.NodeHeight}\n" +
                            $"X: {node.X}\nY: {node.Y}";
            var pin = view.Editor?.SelectedPin;
            view.Editor?.DetailsPinText = pin == null ? "" :
                                  $"{pin.PinFriendlyName}\n" +
                                  $"{pin.Guid}\n" +
                                  $"Category: {pin.PinType.PinCategory}\n" +
                                  $"Subcategory: {pin.PinType.PinSubCategory}\n" +
                                  $"Subcategory object: {pin.PinType.PinSubCategoryObject}\n" +
                                  $"Container type: {pin.PinType.ContainerType}\n" +
                                  $"Property: {pin.Property}";
            DetailsContent.GetVisualDescendants().OfType<BytecodeViewer>().FirstOrDefault()?.SetBytecode(node?.NodeJson);
        }

        private void PanAnotherViewer(Vector delta, GraphView2 thisView, GraphView2 anotherView)
        {
            if (thisView.Scaling != anotherView.Scaling)
            {
                anotherView.Zoom(thisView.Scaling - anotherView.Scaling, thisView.MousePosition);
            }
            else
            {
                var newVector = anotherView.Translation + delta;
                anotherView.Scaling = thisView.Scaling;
                anotherView.SetTranslation(new Point(newVector.X, newVector.Y));
            }
        }

        public async void OpenInDisassemblyButton_Click(object sender, RoutedEventArgs e)
        {
            if (GraphViewModel.SelectedNodes.FirstOrDefault() is {} node)
            {
                BPTabControl.SelectedIndex = 1;
                await Task.Delay(20);
                InstructionsViewer.JumpToInstr(node.StatementIndex);
            }
        }

        private async void SaveAsPNG_OnClick(object? sender, RoutedEventArgs e)
        {
            string functionName = VM.Asset.Asset?.IsEvent(_lastFunc) == true ? "Ubergraph" : _lastFunc?.Name ?? "Unknown";
            
            var storageProvider = this.FindLogicalAncestorOfType<Window>()?.StorageProvider!;
            var file = await storageProvider.SaveFilePickerAsync(new()
            {
                Title = "Save file",
                FileTypeChoices = [new(".png file") { Patterns = ["*.png"] }],
                SuggestedFileName = $"{VM.Asset.Asset?.Name}_{functionName}.png"
            });

            if (file is not {} f)
                return;
            
            int xMin = (int)_graph.Nodes.Min(o => o.X);
            int xMax = (int)_graph.Nodes.Max(o => o.X + o.NodeWidth);
            int yMin = (int)_graph.Nodes.Min(o => o.Y);
            int yMax = (int)_graph.Nodes.Max(o => o.Y + o.NodeHeight);
            
            GraphView2 view = new GraphView2()
            {
                Width = xMax - xMin + 300,
                Height = yMax - yMin + 300,
                Editor = GraphViewModel,
            };
            view.SetTranslation(new Point(xMin - 150, yMin - 150));
            
            var pixelSize = new PixelSize((int)view.Width, (int)view.Height - 25);
            var size = new Size(view.Width, view.Height);
            using var renderBitmap = new RenderTargetBitmap(pixelSize);
            view.Measure(size);
            view.Arrange(new Rect(size));
            renderBitmap.Render(view);
            renderBitmap.Save(file.Path.AbsolutePath, new PngBitmapEncoderOptions());
        }

        private async void AssetParentClass_OnClick(object? sender, RoutedEventArgs e)
        {
            if (VM.Asset.SuperStruct.Starts("/Script/"))
            {
                DialogWindow dialog = new DialogWindow("Cannot open native C++ class", "Info");
                dialog.Show(MainWindow.Instance);
            }
            else
            {
                string path = VM.Asset.SuperStruct.SubstringBeforeLast('.') + ".uasset";
                await MainWindow.Instance.LoadAsset(new AssetFile(path.SubstringAfterLast('/'),path));
            }
        }

        private async void PlayButton_OnClick(object? sender, RoutedEventArgs e)
        {
            GraphViewModel.CurrentDebuggerNode = null;
            await MainWindow.DebuggerOutput!.WriteLineAsync("DEBUGGER - UNPAUSE");
        }
        
        private async void NextButton_OnClick(object? sender, RoutedEventArgs e)
        {
            GraphViewModel.CurrentDebuggerNode = null;
            await MainWindow.DebuggerOutput!.WriteLineAsync("DEBUGGER - NEXT");
        }

        private async void InputElement_OnDoubleTapped(object? sender, TappedEventArgs e)
        {
            var window = await DialogWindow.Show("Input a new property value", "Set local value", true, true);
            if (window.Result == DialogWindowResult.Ok)
            {
                if ((sender as ListBox).SelectedValue is AssetPropertyViewModel prop)
                {
                    await MainWindow.DebuggerOutput!.WriteLineAsync($"DEBUGGER - SET VALUE | {prop.Name} | {window.ValueTextBox.Text}");
                    prop.DefaultValue = window.ValueTextBox.Text ?? "";
                }
            }
        }
    }

    public partial class BPGraphViewerViewModel : ObservableObject
    {
        [ObservableProperty]
        private AssetViewModel _asset;
        
        [ObservableProperty]
        private bool _isProgressVisible;

        [ObservableProperty]
        private string _progressMessage = "Message";

        [ObservableProperty]
        private double _progressValue;
        [ObservableProperty]
        private double _progressMax = 100;
        
        [ObservableProperty]
        private object? _selectedObject;
        
        [ObservableProperty]
        private GridLength _blueprintDetailsValueColumnWidth = new(120, GridUnitType.Pixel);
        
        
        [ObservableProperty]
        private AssetFunctionViewModel? _currentFunction;
    }
}
