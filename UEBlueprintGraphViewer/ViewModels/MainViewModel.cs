using CommunityToolkit.Mvvm.ComponentModel;
using CUE4Parse.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia.Threading;
using UEBlueprintGraphViewer.Assets;
using UEBlueprintGraphViewer.Comparing;

namespace UEBlueprintGraphViewer.ViewModels
{
    internal partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private AssetDirectory? _currentDir;

        [ObservableProperty]
        private AssetDirectory? _root;

        [ObservableProperty]
        private List<AssetFile>? _flatFilesFiltered;

        private List<AssetFile>? _flatFiles;
        
        [ObservableProperty]
        private bool _isInCompareMode;

        private string? _filterText;
        public string? FilterText
        {
            get => _filterText;
            set
            {
                _filterText = value;
                ApplyFilter();
            }
        }

        private bool _showOnlyChanged;
        public bool ShowOnlyChanged
        {
            get => _showOnlyChanged;
            set
            {
                _showOnlyChanged = value;
                ApplyFilter();
            }
        }


        [ObservableProperty]
        private string _statusText;
        
        public void PopulateTree(List<string> assets, List<string>? added = null, List<string>? removed = null, List<string>? modified = null)
        {
            Stopwatch sw = Stopwatch.StartNew();

            HashSet<string>? newHash = added?.ToHashSet();
            HashSet<string>? removedHash = removed?.ToHashSet();
            HashSet<string>? modifiedHash = modified?.ToHashSet();

            AssetDirectory root = new AssetDirectory(null, "", "/");
            List<AssetFile> flat = [];

            foreach (string entry in assets.OrderBy(o => o.SubstringBeforeLast('.')))
            {
                string extenstion = entry.SubstringAfterLast('.');
                if (extenstion != "uasset" && extenstion != "umap")
                    continue;

                AssetDirectory current = root;

                string[] path = entry.Split('/', StringSplitOptions.RemoveEmptyEntries);
                foreach (string folder in path[..^1])
                {
                    AssetDirectory? dir = current.Items.Find(f => f.Name == folder) as AssetDirectory;
                    if (dir == null)
                    {
                        dir = new(current, folder, $"{current.FullPath}{folder}/");
                        current.Items.Add(dir);
                    }
                    current = dir;
                }

                AssetFile file = new(path.Last(), entry);
                current.Items.Add(file);
                flat.Add(file);

                if (newHash?.Contains(entry) == true)
                {
                    current.SetStatus(ChangeStatus.Changed);
                    file.ChangeStatus = ChangeStatus.Added;
                }
                else if (removedHash?.Contains(entry) == true)
                {
                    current.SetStatus(ChangeStatus.Changed);
                    file.ChangeStatus = ChangeStatus.Removed;
                }
                else if (modifiedHash?.Contains(entry) == true)
                {
                    current.SetStatus(ChangeStatus.Changed);
                    file.ChangeStatus = ChangeStatus.Changed;
                }
            }
            
            Root = root;
            CurrentDir = root;
            
            _flatFiles = flat;
            ApplyFilter();
            sw.Stop();
            Trace.WriteLine($"Populate tree: {sw.ElapsedMilliseconds}ms");
        }

        private void ApplyFilter()
        {
            IEnumerable<AssetFile> list = _flatFiles ?? [];
            if (ShowOnlyChanged)
                list = _flatFiles?.Where(o => o.ChangeStatus != ChangeStatus.None) ?? [];

            if (string.IsNullOrEmpty(FilterText))
                FlatFilesFiltered = [.. list];
            else
                FlatFilesFiltered = [.. list.Where(o => o.FullPath.Contains(FilterText, StringComparison.OrdinalIgnoreCase))];
        }

        public void Clear()
        {
            Root = null;
            CurrentDir = null;
            _flatFiles = null;
            FlatFilesFiltered = null;
        }

        public void NavigateBack()
        {
            CurrentDir = CurrentDir?.Parent ?? Root;
        }
    }
}
