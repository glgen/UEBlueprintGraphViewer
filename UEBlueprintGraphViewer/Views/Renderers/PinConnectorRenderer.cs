using SkiaSharp;
using UEBlueprintGraphViewer.Engine;
using UEBlueprintGraphViewer.Nodes;

namespace UEBlueprintGraphViewer.Views.Renderers;

public class PinConnectorRenderer
{
    private static SKPath _execConnector = SKPath.ParseSvgPathData("M1,1 L1,13 L4,13 L13,7 L4,1 Z");
    private static SKPath _delegateConnector = SKPath.ParseSvgPathData("M4,2 L2,4 L2,10 L4,12 L10,12 L12,10 L12,4 L10,2 Z");
    private static SKPath _arrayConnector = SKPath.ParseSvgPathData("M0,4H4V0H0Z M0,9H4V5H0Z M0,14H4V10H0Z M5,4H9V0H5Z M5,9H9V5H5Z M5,14H9V10H5Z M10,14H14V10H10Z M10,9H14V5H10Z M10,0V4H14V0Z");
    private static SKPath _mapConnector = SKPath.ParseSvgPathData("M0,4H4V0H0Z M0,9H4V5H0Z M0,14H4V10H0Z");
    private static SKPath _mapConnector2 = SKPath.ParseSvgPathData("M5,4H14V0H5Z M5,9H14V5H5Z M5,14H14V10H5Z");
    private static SKPath _setConnector = SKPath.ParseSvgPathData("M8 1c2.6 0 1 5 3 6-2 1-.4 6-3 6v1c2 0 3 0 3.5-5 0-1 1.5-1 2.5-1V6c-1 0-2.5 0-2.5-1C11 0 10 0 8 0ZM6 1C3.4 1 5 6 3 7c2 1 .4 6 3 6v1c-2 0-3 0-3.5-5C2.5 8 0 8 0 8V6S2.5 6 2.5 5C3 0 4 0 6 0Z");
    
    public static void Render(float x, float y, bool isConnected, EngineBPData.GraphPinType type, SKCanvas canvas)
    {
        bool forceFill = false;
        SKPath? connectorPath1;
        SKPath? connectorPath2 = null;
        if (type.PinCategory == EngineBPData.PinType.exec)
        {
            connectorPath1 = _execConnector;
        }
        else if (type.PinCategory == EngineBPData.PinType.Delegate)
        {
            connectorPath1 = _delegateConnector;
        }
        else
        {
            connectorPath1 = type.ContainerType switch
            {
                EngineEnums.EPinContainerType.None => null,
                EngineEnums.EPinContainerType.Array => _arrayConnector,
                EngineEnums.EPinContainerType.Set => _setConnector,
                EngineEnums.EPinContainerType.Map => _mapConnector,
                _ => null,
            };
            forceFill = connectorPath1 != null;
            connectorPath2 = type.ContainerType == EngineEnums.EPinContainerType.Map ? _mapConnector2 : null;
        }
        
        SKPaint connectorColor = isConnected || forceFill ? ViewData.GetPinColorSK(type.PinCategory) : ViewData.GetPinStrokeSK(type.PinCategory);
        if (connectorPath1 != null)
        {
            SkiaUtils.DrawPathAtLocation(canvas, connectorPath1, x - 6,  y - 7, connectorColor);
            if (connectorPath2 != null)
                SkiaUtils.DrawPathAtLocation(canvas, connectorPath2, x - 6,  y - 7, ViewData.GetPinColorSK(type.PinSubCategory));
        }
        else
        {
            canvas.DrawCircle(x,y, type.IsReference ? 4f : 7.5f, connectorColor);
        }
    }
}