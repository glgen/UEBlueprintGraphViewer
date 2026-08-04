using UEBlueprintGraphViewer.Assets;
using UEBlueprintGraphViewer.Nodes;

namespace UEBlueprintGraphViewer.ReferencesSearch;

public class ReferenceResult
{
    public AssetFile File { get; set; }
    public string? Function { get; set; }
    public int? NodeStatementIndex { get; set; }
}