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
using UEBlueprintGraphViewer.Assets;
using UEBlueprintGraphViewer.Decompiler;
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

        BPGraph Graph;
        BPGraph Graph2;

        UFunction? lastFunc;

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
            
            UpdateSecondViewerState();
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
            var n = Graph.Nodes.FirstOrDefault(o => o.StatementIndex == statementIndex);
            FirstViewer.Autopanner.PanToCentered(new Point(n?.X ?? 0, n?.Y ?? 0));
        }

        public async void AssetInfoPanel_FunctionChoosen(AssetFunctionViewModel func)
        {
            OpenFunction(func);
        }

        public async Task OpenFunction(AssetFunctionViewModel func)
        {
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
            if (!VM.Asset.Asset.IsEvent(lastFunc) || !VM.Asset.Asset.IsEvent(func.Function))
                await Decompile(func);

            lastFunc = func.Function;

            RepositionViewport(FirstViewer, Graph.FindFuncStartNode(func.Name));
            RepositionViewport(SecondViewer, Graph2.FindFuncStartNode(func.Name));
        }

        public async Task DecompileFunctionCompare(AssetFunctionViewModel func)
        {
            await Decompile(func);

            RepositionViewport(FirstViewer, Graph.FindFuncStartNode(func.Name));
            RepositionViewport(SecondViewer, Graph2.FindFuncStartNode(func.Name));
        }

        private async Task Decompile(AssetFunctionViewModel func)
        {
            Clear();

            SetProgressStateName("Decompiling...");

            if (func.Function != null)
            {
                var decompiler = new FunctionDecompiler(VM.Asset.Asset, Settings.Instance.Game, func.Function);
                await DecompileAndCheck(func, decompiler);
                Graph = decompiler.Graph;
                Graph2 = new();
                Json = JsonConvert.SerializeObject(decompiler.GlobalContext.CurrentFunction, Formatting.Indented);
            }
            else
            {
                Graph = new();
                if (func.FunctionCompare1 != null && VM.Asset.AssetCompare1 != null)
                {
                    var decompiler = new FunctionDecompiler(VM.Asset.AssetCompare1, Settings.Instance.CompareGame1!, func.FunctionCompare1);
                    await DecompileAndCheck(func, decompiler);
                    Graph = decompiler.Graph;
                }

                Graph2 = new();
                if (func.FunctionCompare2 != null && VM.Asset.AssetCompare2 != null)
                {
                    var decompiler = new FunctionDecompiler(VM.Asset.AssetCompare2, Settings.Instance.CompareGame2!, func.FunctionCompare2);
                    await DecompileAndCheck(func, decompiler);
                    Graph2 = decompiler.Graph;
                }

                BPGraph.Compare(Graph, Graph2);
                Json = "{}";
            }

            SetProgressStateName("Building graph layout...");

            var task1 = Graph.LayoutNodesMsaglAsync(null);
            var task2 = Graph2.LayoutNodesMsaglAsync(null);
            await Task.WhenAll(task1, task2);

            FirstViewer.Editor = GraphViewModel;
            SecondViewer.Editor = GraphViewModel2;
            AddNodesToLoad(Graph, Graph2);
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
            var node = view.Editor?.SelectedNodes.FirstOrDefault();
            DetailsText.Text = node?.NodeText ?? "";
            DetailsBytecode.SetBytecode(node?.NodeJson);
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
    }

    public partial class BPGraphViewerViewModel : ObservableObject
    {
        [ObservableProperty]
        private AssetViewModel _asset;
        
        [ObservableProperty]
        private bool isProgressVisible = false;

        [ObservableProperty]
        private string progressMessage = "Message";

        [ObservableProperty]
        private double progressValue = 0;
        [ObservableProperty]
        private double progressMax = 100;
    }
}
