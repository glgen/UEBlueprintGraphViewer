using System.Collections.Generic;
using System.Linq;
using UEBlueprintGraphViewer.Comparing;

namespace UEBlueprintGraphViewer.Assets
{
    public class AssetTreeItemParent
    {
        public string Name { get; set; }
        public string FullPath { get; set; }

        public ChangeStatus ChangeStatus { get; set; }
        
        public AssetTreeItemParent(string name, string fullPath)
        {
            Name = name;
            FullPath = fullPath;
        }
    }
    public class AssetDirectory : AssetTreeItemParent
    {
        public AssetDirectory? Parent;
        public List<AssetTreeItemParent> Items { get; } = [];
        
        public List<AssetTreeItemParent> ItemsSorted
        {
            get
            {
                List<AssetTreeItemParent> items = [];
                items.AddRange(Items.OfType<AssetDirectory>().OrderBy(o => o.Name));
                items.AddRange(Items.OfType<AssetFile>().OrderBy(o => o.Name));
                return items;
            }
        }

        public AssetDirectory(AssetDirectory? parent, string name, string fullPath) : base(name, fullPath)
        {
            Parent = parent;
        }

        public void SetStatus(ChangeStatus status)
        {
            var dir = this;
            while (dir != null)
            {
                dir.ChangeStatus = status;
                dir = dir.Parent;
            }
        }
    }

    public class AssetFile : AssetTreeItemParent
    {
        public AssetFile(string name, string fullPath) : base(name, fullPath) { }
    }
}
