using UEBlueprintGraphViewer.Nodes;

namespace UEBlueprintGraphViewer.ViewModels
{
    public class ConnectionViewModel
    {
        public ConnectionViewModel(GraphPin source, GraphPin target)
        {
            Source = source;
            Target = target;
        }
        public GraphPin Source { get; set; }
        public GraphPin Target { get; set; }
    }
}
