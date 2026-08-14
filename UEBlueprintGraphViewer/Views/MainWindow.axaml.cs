using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform;
using Avalonia.Utilities;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.Utils;
using UEBlueprintGraphViewer.Assets;
using UEBlueprintGraphViewer.Comparing;
using UEBlueprintGraphViewer.Engine;
using UEBlueprintGraphViewer.Nodes;
using UEBlueprintGraphViewer.ReferencesSearch;
using UEBlueprintGraphViewer.ViewModels;
using UEBlueprintGraphViewer.Views;

namespace UEBlueprintGraphViewer
{
    public partial class MainWindow : Window
    {
        public static MainWindow Instance { get; private set; }
        
        MainViewModel viewModel = new();
        public static PackageData? Package { get; private set; }
        public static PackageData? PackageCompare1 { get; private set; }
        public static PackageData? PackageCompare2 { get; private set; }
        
        public static NamedPipeClientStream? DebuggerPipeClient { get; private set; }
        public static StreamWriter? DebuggerOutput { get; private set; }
        public static bool DebuggerPipeInitialized { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
            Instance = this;
            DataContext = viewModel;
            Assembly assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            Title = $"UE Blueprint Graph Viewer v{version.Major}.{version.Minor}.{version.Build}";
            if (!Design.IsDesignMode && Settings.DebuggerMode)
            {
                InitDebuggerPipe();
                ReadDebuggerMessages();
            }
        }
        
        public async void InitDebuggerPipe()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            
            DebuggerPipeClient = new NamedPipeClientStream(".", "BPDebuggerPipe", PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                await DebuggerPipeClient.ConnectAsync(5000);
            }
            catch (TimeoutException)
            {
                new DialogWindow($"Timeout", "Error").Show();
                return;
            }

            DebuggerOutput = new StreamWriter(DebuggerPipeClient, Encoding.Unicode) { AutoFlush = true };

            DebuggerPipeInitialized = true;
            
            while (true)
            {
                await Task.Delay(50);
                try
                {
                    await DebuggerOutput.WriteLineAsync("");
                }
                catch
                {
                    break;
                }
            }
            
        }

        private async void ReadDebuggerMessages()
        {
            byte[] buffer = new byte[4096];

            try
            {
                List<byte> bufferComposite = [];
                
                while (true)
                {
                    // wait for named pipe init
                    if (!DebuggerPipeInitialized || DebuggerPipeClient == null)
                    {
                        await Task.Delay(100);
                        continue;
                    }
                    
                    int bytesRead = await DebuggerPipeClient.ReadAsync(buffer, CancellationToken.None);
                    if (bytesRead == 0)
                    {
                        await new DialogWindow("Connection closed", "").ShowDialog(this);
                        Close();
                        break;
                    }
                    
                    bufferComposite.AddRange(buffer[..bytesRead]);
                    while (bytesRead == 4096)
                    {
                        bytesRead = await DebuggerPipeClient.ReadAsync(buffer, CancellationToken.None);
                        bufferComposite.AddRange(buffer[..bytesRead]);
                    }

                    string message = Encoding.Unicode.GetString(bufferComposite.ToArray(), 0, bufferComposite.Count);
                    bufferComposite = [];
                    if (message.StartsWith("DEBUGGER - BREAKPOINT HIT", StringComparison.Ordinal))
                    {
                        var parts = message.Split(" | ");
                        string funcName = parts[1];
                        string objPath = funcName.SubstringBeforeLast('.');
                        
                        bool foundNode = false;
                        
                        if (AssetsTabs.Items.FirstOrDefault(o =>
                                o is TabItem t && AssetsUtils.FixAssetPath(t.Tag?.ToString()?.SubstringBeforeLast('.') ?? "") == objPath) is TabItem tab)
                        {
                            AssetsTabs.SelectedItem = tab;
                            if (tab.Content is BPGraphViewer viewer)
                            {
                                int statementIndex = int.Parse(parts[2]);
                                viewer.GraphViewModel.CurrentDebuggerNode =
                                    viewer.GraphViewModel.Nodes.FirstOrDefault(o => o.StatementIndex == statementIndex);

                                // skip all instructions which are not correspond to any node
                                if (viewer.GraphViewModel.CurrentDebuggerNode != null)
                                {
                                    viewer.DetailsTabControl.SelectedIndex = 2;
                                    
                                    foundNode = true;
                                    
                                    List<string> stack = [];
                                    List<(string, string)> locals = [];
                        
                                    int index = 5;
                                    while (index < parts.Length && parts[index] != "LOCALS")
                                    {
                                        stack.Add(parts[index]);
                                        index++;
                                    }
                                    index++;
                                    while (index + 1 < parts.Length)
                                    {
                                        locals.Add((parts[index], parts[index+1]));
                                        index += 2;
                                    }
                                    
                                    string funcShortName = funcName.SubstringAfterLast(':');
                                    var func = viewer.VM.Asset.Asset?.Functions.FirstOrDefault(o => o.Name == funcShortName);

                                    viewer.GraphViewModel.DebuggerContextObject = parts[3];
                                    viewer.GraphViewModel.DebuggerStack = string.Join('\n', stack);
                                    viewer.GraphViewModel.DebuggerLocals = locals.Select(o =>
                                        new AssetPropertyViewModel(
                                            new PropertyData(func!.ChildProperties.FirstOrDefault(f => f.Name == o.Item1) as FProperty, func!)
                                            {
                                                DefaultValue = o.Item2
                                            }
                                        )
                                        {
                                        }).ToList();
                                }
                                else
                                {
                                    viewer.GraphViewModel.DebuggerStack = "";
                                    viewer.GraphViewModel.DebuggerLocals = [];
                                }
                                
                                
                                // force rerender viewer to update selection
                                viewer.FirstViewer.InvalidateVisual();
                            }
                        }

                        if (!foundNode)
                        {
                            await DebuggerOutput!.WriteLineAsync("DEBUGGER - NEXT");
                        }
                    }
                }

            }
            catch (Exception e)
            {
                new DialogWindow($"{e.Message}\n{e.StackTrace}", "Error while reading debugger messages").Show();
            }
        }
        
        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            if (!Design.IsDesignMode)
                LoadConfig();
        }

        public async void LoadConfig()
        {
            if (!Directory.Exists("Profiles"))
                Directory.CreateDirectory("Profiles");
            if (!Directory.Exists("Macros"))
                Directory.CreateDirectory("Macros");

            if (!Settings.IsConfigExists())
            {
                // this will probably occur only on launch, so wait a little
                // until main window is loaded and positioned. otherwise the
                // settings window will show up in wrong place
                await Task.Delay(10);
                OpenSettings();
                return;
            }
            
            PersistOpenTabs();

            Package?.Dispose();
            PackageCompare1?.Dispose();
            PackageCompare2?.Dispose();

            Settings.Instance = Settings.ReadConfig();

            viewModel.Clear();
            viewModel.IsInCompareMode = Settings.Instance.IsInCompareMode;

            var mainTab = AssetsTabs.Items.First(o => o is TabItem i && (string)i.Tag! == "MainTab");
            AssetsTabs.Items.Clear();
            AssetsTabs.Items.Add(mainTab);

            await Task.Delay(10);
            
            if (Settings.Instance.IsInCompareMode)
            {
                if (!await CheckConfigPathsValid(Settings.Instance.CompareGame1!) ||
                    !await CheckConfigPathsValid(Settings.Instance.CompareGame2!))
                    return;
                StartComparing();
            }
            else
            {
                if (!await CheckConfigPathsValid(Settings.Instance.Game) ||
                    !await TryLoadDump(Settings.Instance.Game) ||
                    await TryLoadPackage(Settings.Instance.Game) is not {} p)
                    return;

                viewModel.StatusText = "Building directory tree...";
                Package = p;
                viewModel.PopulateTree([.. Package.Assets.Select(o => o.Path)]);
                viewModel.StatusText = "";

                await RestoreOpenTabs(Settings.Instance.Game);
            }
        }

        private async Task RestoreOpenTabs(GameSettings game)
        {
            if (!Settings.Instance.RememberOpenTabs)
                return;

            foreach (var path in game.OpenTabs.ToList())
                await LoadAsset(new AssetFile(path.SubstringAfterLast('/'), path));

            if (game.ActiveTab != null &&
                AssetsTabs.Items.FirstOrDefault(o => o is TabItem t && t.Tag?.ToString() == game.ActiveTab) is { } activeTab)
                AssetsTabs.SelectedItem = activeTab;
        }

        private void SaveOpenTabs(GameSettings game)
        {
            var tabs = AssetsTabs.Items.OfType<TabItem>()
                .Select(t => t.Tag?.ToString())
                .Where(tag => tag is not null and not "MainTab")
                .Select(tag => tag!)
                .ToList();

            game.OpenTabs = tabs;
            game.ActiveTab = (AssetsTabs.SelectedItem as TabItem)?.Tag?.ToString() is { } active and not "MainTab"
                ? active
                : null;
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);
            PersistOpenTabs();

            DebuggerOutput?.Dispose();
            DebuggerPipeClient?.Dispose();
        }

        private void PersistOpenTabs()
        {
            if (!Design.IsDesignMode && Settings.Instance.RememberOpenTabs &&
                !Settings.Instance.IsInCompareMode &&
                Settings.Instance.Game is { ProfileName: not null } game)
            {
                SaveOpenTabs(game);
                
                if (GameSettings.IsConfigExists(game.ProfileName))
                {
                    var onDisk = GameSettings.ReadConfig(game.ProfileName);
                    onDisk.OpenTabs = game.OpenTabs;
                    onDisk.ActiveTab = game.ActiveTab;
                    onDisk.WriteConfig();
                }
                else
                {
                    game.WriteConfig();
                }
            }
        }

        private async Task<PackageData?> TryLoadPackage(GameSettings game)
        {
            viewModel.StatusText = "Loading package...";
            try
            {
                return await PackageData.LoadPackageAsync(game);
            }
            catch (Exception e)
            {
                await DialogWindow.Show($"{e.GetType()}\n{e.Message}\n\n{e.StackTrace}", "Failed to load game package");
                return null;
            }
        }

        private async Task<bool> TryLoadDump(GameSettings game)
        {
            viewModel.StatusText = "Reading .jmap...";
            try
            {
                await Task.Run(game.LoadParamDumpings);
                return true;
            }
            catch (Exception e)
            {
                await DialogWindow.Show($"{e.GetType()}\n{e.Message}\n\n{e.StackTrace}", "Failed to parse object dump");
                return false;
            }
        }

        private async Task<bool> CheckConfigPathsValid(GameSettings game)
        {
            if (!Directory.Exists(game.PaksFolder) ||
                string.IsNullOrWhiteSpace(game.ObjectDump) ||
                game.ObjectDump.EndsWith(".txt") ||
                !File.Exists(game.ObjectDump))
            {
                await DialogWindow.Show("Some paths specified in game profile are invalid", "Failed to load config");
                OpenSettings();
                return false;
            }
            return true;
        }

        private async void OpenSettings()
        {
            if (await SettingsWindow.ShowWindow(this))
                LoadConfig();
        }

        public async void StartComparing()
        {
            ComparisonResult? result = null;

            bool loadingCache = AssetsComparer.AssetsCacheExists();
            if (loadingCache)
            {
                result = AssetsComparer.LoadAssetsCache();
            }
            else
            {
                var dialog = new ProgressWindow("Comparing", "Comparing assets:");
                dialog.Open(this);
                if (await LoadDumpAndPackage())
                {
                    result = await AssetsComparer.CompareAssets(Settings.Instance.CompareGame1!,
                        Settings.Instance.CompareGame2!,
                        PackageCompare1!,
                        PackageCompare2!,
                        dialog.Update);

                    AssetsComparer.SaveAssetsCache(result);
                }

                dialog.Close();
            }

            if (result != null)
                viewModel.PopulateTree(result.AllAssets, result.NewAssets, result.RemovedAssets, result.ModifiedAssets);

            if (loadingCache)
                await LoadDumpAndPackage();
            

            async Task<bool> LoadDumpAndPackage()
            {
                if (!await TryLoadDump(Settings.Instance.CompareGame1!) ||
                    !await TryLoadDump(Settings.Instance.CompareGame2!) ||
                    await TryLoadPackage(Settings.Instance.CompareGame1!) is not { } p1 ||
                    await TryLoadPackage(Settings.Instance.CompareGame2!) is not { } p2)
                    return false;
                
                PackageCompare1 = p1;
                PackageCompare2 = p2;
                return true;
            }
        }

        private async void AssetsTree_DoubleTapped(object sender, TappedEventArgs e)
        {
            if (sender is Visual { DataContext: AssetFile item })
                await LoadAsset(item);
        }

        public async Task LoadAsset(AssetFile item)
        {
            if (AssetsTabs.Items.FirstOrDefault(o => o is TabItem t && t.Tag?.ToString() == item.FullPath) is { } foundTab)
            {
                AssetsTabs.SelectedItem = foundTab;
                return;
            }
            AssetViewModel asset;
            try
            {
                string name = item.Name[..item.Name.LastIndexOf('.')];
                if (Settings.Instance.IsInCompareMode)
                {
                    if (item.ChangeStatus == ChangeStatus.Added)
                    {
                        asset = new(null, await PackageCompare2!.LoadAssetAndCheck(item.FullPath, name));
                    }
                    else if (item.ChangeStatus == ChangeStatus.Removed)
                    {
                        asset = new(await PackageCompare1!.LoadAssetAndCheck(item.FullPath, name), null);
                    }
                    else
                    {
                        var task1 = PackageCompare1!.LoadAssetAndCheck(item.FullPath, name);
                        var task2 = PackageCompare2!.LoadAssetAndCheck(item.FullPath, name);
                        var result = await Task.WhenAll(task1, task2);
                        asset = new(result[0], result[1]);
                    }
                }
                else
                {
                    asset = new(await Package.LoadAssetAndCheck(item.FullPath, name));
                }
                
                var viewer = new BPGraphViewer(asset) { Margin = new Thickness(0, 5, 0, 0) };
                
                AddTab(new()
                {
                    Header = name,
                    Tag = item.FullPath,
                    Classes = { "Closeable" },
                    Content = viewer
                });
            }
            catch (Exception ex)
            {
                await DialogWindow.Show(ex.Message, "Failed to open asset");
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            OpenSettings();
        }

        private async void CompareButton_Click(object sender, RoutedEventArgs e)
        {
            if (await CompareSettingsWindow.ShowWindow(this))
                LoadConfig();
        }
        
        private void MacrosButton_Click(object sender, RoutedEventArgs e)
        {
            new MacroViewerWindow().ShowDialog(this);
        }

        private async void OpenAsset_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Visual { DataContext: AssetFile asset })
                await LoadAsset(asset);
        }

        private async void FindReferences_OnClick(object? sender, RoutedEventArgs e)
        {
            if (Settings.Instance.IsInCompareMode)
            {
                await DialogWindow.Show("Finding assets references is not available in compare mode", "Search");
                return;
            }
            
            if (sender is Visual { DataContext: AssetFile asset })
            {
                var dialog = new ProgressWindow("Search", "Finding references:");
                dialog.Open(this);
                var result = await ReferencesSearcher.FindAssetReference(Package, Package.Assets.First(o => o.Path == asset.FullPath), dialog.Update);
                AddTab(new()
                {
                    Header = $"References of {asset.Name}",
                    Classes = { "Closeable" },
                    Content = new AssetReferencesResultView($"Found {result.Length} references of {asset.Name}",
                        result.Select(o => new ReferenceResult()
                        {
                            File = new(o.Name, o.Path), 
                            Function = ""
                        }).ToList())
                });
                
                dialog.Close();
            }
        }

        private void AssetDirectory_OnDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (sender is Visual { DataContext: AssetDirectory item })
                viewModel.CurrentDir = item;
        }

        private void TabClose_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Visual v)
                CloseTab(v);
        }

        private void Tab_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.Properties.IsMiddleButtonPressed && sender is Visual v)
                CloseTab(v);
        }

        private void CloseTab(Visual child)
        {
            AssetsTabs.Items.Remove(child.GetVisualAncestors().OfType<TabItem>().FirstOrDefault());
        }

        private void CloseAllTabs_OnClick(object? sender, RoutedEventArgs e)
        {
            var toClose = AssetsTabs.Items.OfType<TabItem>()
                .Where(t => t.Classes.Contains("Closeable"))
                .ToList();
            foreach (var tab in toClose)
                AssetsTabs.Items.Remove(tab);
        }

        public void AddTab(TabItem newTab)
        {
            AssetsTabs.Items.Add(newTab);
            AssetsTabs.SelectedItem = newTab;
        }

        private async void FindUnreferenced_OnClick(object? sender, RoutedEventArgs e)
        {
            var dialog = new ProgressWindow("Search", "Finding references:");
            dialog.Open(this);
            var result = await ReferencesSearcher.FindUnreferencedAssets(Package, dialog.Update);
            AddTab(new()
            {
                Header = $"Unreferenced assets",
                Classes = { "Closeable" },
                Content = new AssetReferencesResultView(
                    $"Found {result.Length} unreferenced assets with total size of {(result.Sum(o => o.Size) / 1024d / 1024d):N2} MB",
                    result.Select(o => new ReferenceResult()
                    {
                        File = new(o.Name, o.Path),
                        Function = ""
                    }).ToList())
            });
                
            dialog.Close();
        }
    }
}