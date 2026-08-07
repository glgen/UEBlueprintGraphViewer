using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using UEBlueprintGraphViewer.Engine;
using UEBlueprintGraphViewer.Views;
using UEBlueprintGraphViewer.Views.Renderers;

namespace UEBlueprintGraphViewer;

public class PinConnector : Control
{
    public static readonly StyledProperty<EngineBPData.GraphPinType> PinTypeProperty =
        AvaloniaProperty.Register<BytecodeViewer, EngineBPData.GraphPinType>(nameof(PinType));

    public EngineBPData.GraphPinType PinType
    {
        get => GetValue(PinTypeProperty);
        set => SetValue(PinTypeProperty, value);
    }
    
    CustomDrawOperation _customDrawOperation = new();

    public PinConnector()
    {
        AffectsRender<PinConnector>(PinTypeProperty);
    }
    
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (IsEffectivelyVisible)
        {
            _customDrawOperation.PinType = PinType;
            _customDrawOperation.Bounds = Bounds;
            context.Custom(_customDrawOperation);
        }
    }
    
    private class CustomDrawOperation : ICustomDrawOperation
    {
        public Rect Bounds { get; set; }

        public EngineBPData.GraphPinType PinType;

        public void Dispose() { }

        public bool Equals(ICustomDrawOperation? other) => false;

        public bool HitTest(Point p) => Bounds.Contains(p);

        public void Render(ImmediateDrawingContext context)
        {
            if (SkiaUtils.GetLeaseCanvas(context) is not {} canvas)
                return;
            PinConnectorRenderer.Render(9, 9, false, PinType, canvas);
        }
    }
}