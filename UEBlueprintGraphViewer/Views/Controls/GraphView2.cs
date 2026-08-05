using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Newtonsoft.Json;
using SkiaSharp;
using UEBlueprintGraphViewer.Comparing;
using UEBlueprintGraphViewer.Engine;
using UEBlueprintGraphViewer.Nodes;
using UEBlueprintGraphViewer.ViewModels;
using UEBlueprintGraphViewer.Views;
using UEBlueprintGraphViewer.Views.Controls;
using UEBlueprintGraphViewer.Views.Renderers;

namespace UEBlueprintGraphViewer;

public class GraphView2 : ContentControl
{
    public GraphView2()
    {
        customDrawOperation = new(this);
        ClipToBounds = true;
        Focusable = true;
        Autopanner = new(this);
    }

    private EditorViewModel? editor;

    public EditorViewModel? Editor
    {
        get => editor;
        set
        {
            editor?.PropertyChanged -= EditorOnPropertyChanged;
            editor = value;
            editor?.SelectedNodes.Clear();
            mouseOverNode = null;
            InvalidateVisual();
            GraphViewSearch search = new() { DataContext = editor };
            Content = search;
            editor?.PropertyChanged += EditorOnPropertyChanged;
        }
    }
    
    private void EditorOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorViewModel.SearchResult))
        {
            InvalidateVisual();
        }
        else if (e.PropertyName == nameof(EditorViewModel.SearchResultIndex))
        {
            var node = Editor?.SearchResult.ElementAtOrDefault(Editor.SearchResultIndex);
            if (node != null)
                Autopanner.PanToCentered(new(node.X, node.Y));
        }
    }
    
    public delegate void SelectionChanged();
    public delegate void Panned(Vector delta);

    public Panned OnPanned;

    public GraphViewerAutopanner Autopanner;
    
    public SelectionChanged OnSelectionChanged;
    
    readonly CustomDrawOperation customDrawOperation;

    public double CustomDrawOperationTime;
    private double lastTime;
    private double deltaTimeMs;
    
    public Vector Translation { get; private set; } = new(0, 0);
    

    public float Scaling = 1;
    
    public Point MousePosition => lastMousePosition;


    private bool isDragging;
    private bool isDraggingNode;
    private bool isSelecting;
    private Point lastMousePosition;
    private Point mousePosOnGraph;
    private Point mousePosOnGraphOnPress;
    
    private List<BPNode> selectedNodesBeforeSelection = [];
    private BPNode? mouseOverNode;

    public bool DisableMoving;

    private int gridSnap = 8;
    
    private object updateLock = new();
    
    public override void Render(DrawingContext context)
    {
        double elapsed = (double)Stopwatch.GetTimestamp() / (Stopwatch.Frequency / 1000f);
        deltaTimeMs = elapsed - lastTime;
        lastTime = elapsed;
        
        if (IsEffectivelyVisible)
        {
            customDrawOperation.Bounds = Bounds;
            context.Custom(customDrawOperation);
        }
    }

    public void SetTranslation(Point translation)
    {
        Translation = translation;
        InvalidateVisual();
    }
    
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.F)
        {
            Editor?.IsSearchVisible = !Editor.IsSearchVisible;
        }
    }
    
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;
        mousePosOnGraphOnPress = mousePosOnGraph;
        if (props.IsLeftButtonPressed)
        {
            UpdateNodesSelection(e);
            isDraggingNode = mouseOverNode != null;
            isSelecting = !isDraggingNode;
            if (isSelecting)
            {
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                {
                    selectedNodesBeforeSelection = [..Editor?.SelectedNodes ?? []];
                }
                else
                {
                    selectedNodesBeforeSelection = [];
                }
            }
        }
        if (props.IsRightButtonPressed || props.IsMiddleButtonPressed)
        {
            isDragging = !DisableMoving;
        }
        
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Right)
        {
            if (mousePosOnGraphOnPress == mousePosOnGraph)
            {
                UpdateNodesSelection(e);
                var flyout = new Flyout() {FlyoutPresenterClasses = { "ContextMenu" }};
                flyout.Content = mouseOverNode != null ? new NodeContextMenu(this, flyout) : new AddNodeMenu();
                flyout.ShowAt(this, true);
            }
        }
        
        if (isSelecting)
            OnSelectionChanged?.Invoke();
        
        isDragging = false;
        isDraggingNode = false;
        isSelecting = false;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        var newPos = e.GetPosition(this);
        var newGraphPos = (newPos + Translation) / Scaling;

        mouseOverNode = Editor?.Nodes.FirstOrDefault(o => 
            mousePosOnGraph.X > o.X && mousePosOnGraph.Y > o.Y &&
            mousePosOnGraph.X < o.Right && mousePosOnGraph.Y < o.Bottom);
        
        if (mouseOverNode != null)
        {
            if (mousePosOnGraph.Y > mouseOverNode.Y + (mouseOverNode.HeaderHidden ? 0 : 30))
            {
                List<GraphPin> pins = mousePosOnGraph.X < mouseOverNode.X + mouseOverNode.NodeWidth / 2f
                    ? mouseOverNode.Input
                    : mouseOverNode.Output;
                Editor?.MouseOverPin = pins.FirstOrDefault(o => !o.IsHidden && mousePosOnGraph.Y < o.Y + 16);
            }
        }
        
        if (isDragging)
        {
            var delta = lastMousePosition - newPos;
            Translation += delta;
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                OnPanned?.Invoke(delta);
        }
        
        if (isDraggingNode)
        {
            float offsetX = (float)(Math.Floor(newGraphPos.X / gridSnap) * gridSnap - Math.Floor(mousePosOnGraph.X / gridSnap) * gridSnap);
            float offsetY = (float)(Math.Floor(newGraphPos.Y / gridSnap) * gridSnap - Math.Floor(mousePosOnGraph.Y / gridSnap) * gridSnap);
            
            foreach (var node in Editor?.SelectedNodes ?? [])
            {
                node.SetPosition(node.X + offsetX, node.Y + offsetY);
            }
        }
        
        if (isSelecting && Editor != null)
        {
            var minX = Math.Min(mousePosOnGraphOnPress.X, mousePosOnGraph.X);
            var minY = Math.Min(mousePosOnGraphOnPress.Y, mousePosOnGraph.Y);
            var maxX = Math.Max(mousePosOnGraphOnPress.X, mousePosOnGraph.X);
            var maxY = Math.Max(mousePosOnGraphOnPress.Y, mousePosOnGraph.Y);

            var selected = Editor.Nodes.Where(o =>
                o.X < maxX && minX < o.Right &&
                o.Y < maxY && minY < o.Bottom).ToList();
            var toAdd = selected.Where(o => !selectedNodesBeforeSelection.Contains(o));
            var toRemove = selected.Where(o => selectedNodesBeforeSelection.Contains(o));
            
            lock (updateLock)
            {
                Editor.SelectedNodes.Clear();
                Editor.SelectedNodes.AddRange(selectedNodesBeforeSelection);
                Editor.SelectedNodes.AddRange(toAdd);
                Editor.SelectedNodes.RemoveAll(o => toRemove.Contains(o));
            }
        }
        
        lastMousePosition = newPos;
        mousePosOnGraph = newGraphPos;
        
        InvalidateVisual();
    }

    private void UpdateNodesSelection(PointerEventArgs e)
    {
        if (Editor == null) return;
        Editor.SelectedPin = Editor.MouseOverPin;
        if (!Editor.SelectedNodes.Contains(mouseOverNode))
        {
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                Editor.SelectedNodes.Clear();
            }
            
            if (mouseOverNode != null)
            {
                Editor.SelectedNodes.Add(mouseOverNode);
            }
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            Editor.SelectedNodes.Remove(mouseOverNode);
        }
        OnSelectionChanged?.Invoke();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        var origTranslation = Translation;
        Zoom(e.Delta.Y, lastMousePosition);
        OnPanned?.Invoke(Translation - origTranslation);
    }

    public void Zoom(double delta, Point origin)
    {
        Translation += origin;

        float newScale = Scaling;
        if (delta > 0)
        {
            newScale = Scaling * 1.25f;
        }
        else if (delta < 0)
        {
            newScale = Scaling / 1.25f;
        }
        newScale = (float)Math.Round(newScale, 4);
        newScale = (float)Math.Clamp(newScale, 0.01, 3);
        if (newScale is > 0.9f and < 1.1f)
            newScale = 1;

        var mul = newScale / Scaling;
        Scaling = newScale;
        Translation *= mul;
        Translation -= origin;
        Translation = new Vector(Math.Round(Translation.X), Math.Round(Translation.Y));
        InvalidateVisual();
    }

    private class CustomDrawOperation : ICustomDrawOperation
    {
        public Rect Bounds { get; set; }

        private GraphView2 view;
        
        public CustomDrawOperation(GraphView2 editor)
        {
            view = editor;
        }

        public void Dispose() { }

        public bool Equals(ICustomDrawOperation? other) => false;

        public bool HitTest(Point p) => Bounds.Contains(p);

        static SKColor backgroundColor = new SKColor(30,30,30);
        static SKPaint darkerBackgroundPaint = SkiaUtils.MakePaint(21, 21, 21);
        static SKPaint gridPaint = SkiaUtils.MakeStroke(1, 51,51,51, 128);
        static SKPaint gridPaint2 = SkiaUtils.MakeStroke(2, 51,51,51);
        static SKPaint nodeBackg = SkiaUtils.MakePaint(40,40,40);
        static SKPaint nodeAddedBackg = SkiaUtils.MakePaint(40,88,40);
        static SKPaint nodeChangedBackg = SkiaUtils.MakePaint(136,136,40);
        static SKPaint nodeRemovedBackg = SkiaUtils.MakePaint(136,40,40);
        static SKPaint nodeBorder = SkiaUtils.MakeStroke(SKColors.Black, 1);
        static SKPaint nodeSelectedBorder = SkiaUtils.MakeStroke(SKColors.Orange, 1);
        static SKPaint nodeSearchResultBorder = SkiaUtils.MakeStroke(SKColors.White, 3);
        static SKPaint nodeValue = SkiaUtils.MakePaint(34,34,34);
        private static SKPaint nodeValueBorder = SkiaUtils.MakeStroke(1, 153, 153, 153);
        static SKPaint selection = SkiaUtils.MakePaint(30,50,80, 70);
        static SKPaint selectionBorder = SkiaUtils.MakeStroke(1, 30,80,150);
        private static SKPaint textPaint = SkiaUtils.MakePaint(255, 255, 255, 255);
        private static SKPaint textPaint2 = SkiaUtils.MakePaint(255, 255, 255, 128);

        private static SKFont textFont = SkiaUtils.MakeFont(12.5f);
        static SKFont textFontBody = SkiaUtils.MakeFont( 28);
        
        
        public void Render(ImmediateDrawingContext context)
        {
            try
            {
                var stopWatch = Stopwatch.StartNew();
                
                if (SkiaUtils.GetLeaseCanvas(context) is not {} canvas)
                    return;
                
                canvas.Save();
                
                // Fill background of entire control
                canvas.Clear(backgroundColor);
                
                RenderGrid(canvas);
                
                // Transform
                canvas.Translate((float)-view.Translation.X, (float)-view.Translation.Y);
                canvas.Scale(view.Scaling);

                if (view.Editor is not null)
                {
                    foreach (var connection in view.Editor.Connections)
                    {
                        var pin = connection.Source;
                        var pin2 = connection.Target;
                        
                        if (IsLineInView(pin.X, pin.Y, pin2.X, pin2.Y))
                        {
                            if (view.Scaling > 0.1)
                            {
                                using SKPath path = new();
                                path.MoveTo(new SKPoint(pin.X, pin.Y));
                                path.CubicTo(new SKPoint(pin.X + 70, pin.Y), new SKPoint(pin2.X - 70, pin2.Y),
                                    new SKPoint(pin2.X, pin2.Y));
                                canvas.DrawPath(path, ViewData.GetPinStrokeSK(pin.PinType.PinCategory));
                            }
                            else
                            {
                                canvas.DrawLine(pin.X, pin.Y, pin2.X, pin2.Y,
                                    ViewData.GetPinStrokeSK(pin.PinType.PinCategory));
                            }
                        }
                    }
                    
                    foreach (var node in view.Editor.Nodes)
                    {
                        if (IsNodeInView(node))
                            RenderNode(canvas, node);
                    }
                    
                    lock (view.updateLock)
                    {
                        foreach (var node in view.Editor.SelectedNodes)
                        {
                            canvas.DrawRoundRect(node.X, node.Y,
                                node.NodeWidth, node.NodeHeight, 5, 5, nodeSelectedBorder);
                        }
                        
                        foreach (var node in view.Editor.SearchResult)
                        {
                            canvas.DrawRoundRect(node.X, node.Y,
                                node.NodeWidth, node.NodeHeight, 5, 5, nodeSearchResultBorder);
                        }
                    }
                }

                if (view.mouseOverNode is not null && view.Editor?.MouseOverPin is {} mouseOverPin)
                    RenderPinSelection(mouseOverPin, canvas);
                
                canvas.Restore();

                if (view.isSelecting)
                {
                    var start = view.mousePosOnGraphOnPress * view.Scaling - view.Translation;
                    var end = view.mousePosOnGraph * view.Scaling - view.Translation;
                    
                    SKRect rect = new((float)start.X, (float)start.Y, (float)end.X, (float)end.Y);
                    canvas.DrawRect(rect, selection);
                    canvas.DrawRect(rect, selectionBorder);
                }

                canvas.DrawRect( new SKRect(0, (float)Bounds.Height - 25, (float)Bounds.Width, (float)Bounds.Height), darkerBackgroundPaint);
                float textY = (float)Bounds.Height - 8;
                canvas.DrawText($"Zoom: {view.Scaling:N2}", 5, textY, SKTextAlign.Left, textFont, textPaint);
                canvas.DrawText($"FPS: {Math.Round(1000f / view.deltaTimeMs)}", 120, textY, SKTextAlign.Left, textFont, textPaint);
                
                stopWatch.Stop();
                view.CustomDrawOperationTime = stopWatch.Elapsed.TotalMilliseconds;
            }
            catch (Exception)
            {
                Debugger.Break();
                throw;
            }
        }

        private void RenderPinSelection(GraphPin pin, SKCanvas canvas)
        {
            float width = 35;
            if (!pin.IsNameHidden)
            {
                width += textFont.MeasureText(pin.PinFriendlyName);
                if (pin.IsInput && !pin.IsConnected)
                    width += Math.Clamp(textFont.MeasureText(pin.Value), 30, 400) + 10;
            }

            if (pin.IsOutput)
                width *= -1;
            
            SKRect rect = new(pin.X, pin.Y - 16, pin.X + width, pin.Y + 16);

            SKColor pinColor = ViewData.GetPinColorSK(pin.PinType.PinCategory).Color;
            SKColor pinColorTransparent = pinColor.WithAlpha(0);
                            
            using SKPaint paint = new();
            paint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(rect.Left, rect.Top),
                new SKPoint(rect.Right, rect.Top),
                [pinColorTransparent, pinColor.WithAlpha(100), pinColorTransparent],
                [0, 0.7f, 1],
                SKShaderTileMode.Repeat);
                            
            canvas.DrawRect(rect, paint);

            if (pin.Value.Length > 55)
            {
                SKRect popupRect = new SKRect((float)view.mousePosOnGraph.X, (float)view.mousePosOnGraph.Y + 15,
                    (float)view.mousePosOnGraph.X + 400, (float)view.mousePosOnGraph.Y + 200);
                string formatted = JsonConvert.SerializeObject(pin.Value).Trim('"');
                SkiaUtils.DrawTextWithWrapping(canvas, formatted, popupRect, textFont, textPaint, height =>
                {
                    SKRect popupRect2 = new SKRect(popupRect.Left - 5, popupRect.Top - 5, popupRect.Right + 5, popupRect.Top + 5 + height);
                    canvas.DrawRoundRect(popupRect2, 5, 5, nodeValue);
                    canvas.DrawRoundRect(popupRect2, 5, 5, nodeValueBorder);
                });
            }
        }

        private bool IsNodeInView(BPNode node)
        {
            return node.Right >= view.Translation.X / view.Scaling &&
                   node.Bottom >= view.Translation.Y / view.Scaling &&
                   node.X <= (view.Translation.X + Bounds.Width) / view.Scaling &&
                   node.Y <= (view.Translation.Y + Bounds.Height) / view.Scaling;
        }
        
        private bool IsLineInView(float x1, float y1, float x2, float y2)
        {
            var minX = view.Translation.X / view.Scaling;
            if (x1 < minX && x2 < minX)
                return false;
            var maxX = (view.Translation.X + Bounds.Width) / view.Scaling;
            if (x1 > maxX && x2 > maxX)
                return false;
            var minY = view.Translation.Y / view.Scaling;
            if (y1 < minY && y2 < minY)
                return false;
            var maxY = (view.Translation.Y + Bounds.Height) / view.Scaling;
            if (y1 > maxY && y2 > maxY)
                return false;
            return true;
        }

        private void RenderNode(SKCanvas canvas, BPNode node)
        {
            canvas.Save();
            canvas.Translate(node.X, node.Y);
            
            SKRect nodeRect = new SKRect(0, 0, node.NodeWidth, node.NodeHeight);

            SKPaint backg = node.ChangeStatus switch
            {
                ChangeStatus.None => nodeBackg,
                ChangeStatus.Added => nodeAddedBackg,
                ChangeStatus.Removed => nodeRemovedBackg,
                ChangeStatus.Changed => nodeChangedBackg,
                _ => nodeBackg,
            };
            
            if (view.Scaling > 0.2)
                canvas.DrawRoundRect(nodeRect, 5,5, backg);
            else
                canvas.DrawRect(nodeRect, backg);
            
            if (!node.HeaderHidden)
            {
                SKRect nodeHeaderRect = new SKRect(0, 0, node.NodeWidth, 25);
                SKPaint headerPaint = ViewData.GetNodeColorSK(node.NodeType[7..]);
                if (view.Scaling > 0.2)
                {
                    using SKRoundRect a = new(nodeHeaderRect, 0);
                    a.SetNinePatch(nodeHeaderRect, 5,5,5,0);
                    canvas.DrawRoundRect(a, headerPaint);
                    canvas.DrawText(node.Name, node.HeaderCenter ? node.NodeWidth / 2f : 7, 17, node.HeaderCenter ? SKTextAlign.Center : SKTextAlign.Left, textFont, textPaint);
                }
                else
                {
                    canvas.DrawRect(nodeHeaderRect, headerPaint);
                }
            }

            if (view.Scaling > 0.2)
            {
                if (node.ShowNameAsBody)
                    canvas.DrawText(node.Name, node.NodeWidth / 2f, node.NodeHeight / 2f + 10, SKTextAlign.Center, textFontBody, textPaint2);

                canvas.DrawRoundRect(nodeRect, 5,5, nodeBorder);

                foreach (var pin in node.Input)
                    RenderPin(node, pin, canvas);
                foreach (var pin in node.Output)
                    RenderPin(node, pin, canvas);
            }
            

            canvas.Restore();
        }

        private static void RenderPin(BPNode node, GraphPin pin, SKCanvas canvas)
        {
            if (pin.IsHidden) return;
            
            var connectorX = pin.IsOutput ? node.NodeWidth - 20 : 20;
            float connectorY = pin.Y - node.Y;
            PinConnectorRenderer.Render(connectorX, connectorY, pin.IsConnected, pin.PinType, canvas);
            
            float textY = connectorY + 4f;
            float nameWidth = 35;
            if (!pin.IsNameHidden)
            {
                canvas.DrawText(pin.PinFriendlyName, pin.IsOutput ? node.NodeWidth - 35 : 35, textY, pin.IsOutput ? SKTextAlign.Right : SKTextAlign.Left, textFont, textPaint);
                nameWidth = textFont.MeasureText(pin.PinFriendlyName) + 35;
            }
            
            if (pin is { IsInput: true, IsConnected: false, PinType.PinCategory: not EngineBPData.PinType.exec })
            {
                var valueFormatted = JsonConvert.SerializeObject(pin.Value).Trim('"');
                var valueWidth = Math.Clamp(textFont.MeasureText(valueFormatted), 30, 400) + 10;
                var rect = SKRect.Create(nameWidth + 5, connectorY - 10, valueWidth, 20);
                canvas.DrawRoundRect(rect, 5,5, nodeValue);
                canvas.DrawRoundRect(rect, 5,5, nodeValueBorder);
                    
                string value = valueFormatted.Length > 55 ? $"{valueFormatted[..55]}..." : valueFormatted;
                canvas.DrawText(value, nameWidth + 5 + 5, textY, SKTextAlign.Left, textFont, textPaint);
            }
        }
        
        private void RenderGrid(SKCanvas canvas)
        {
            if (view.Scaling > 0.4)
                RenderLines(16, gridPaint);

            if (view.Scaling > 0.3)
                RenderLines(128, gridPaint2);

            if (view.Scaling is <= 0.3f and > 0.05f)
            {
                RenderLines(128, gridPaint);
                RenderLines(1024, gridPaint2);
            }
            
            return;

            void RenderLines(int cellWidth, SKPaint paint)
            {
                var cellWidthScaled = cellWidth * view.Scaling;
                var startX = -view.Translation.X % cellWidthScaled;
                var startY = -view.Translation.Y % cellWidthScaled;
                for (var x = startX; x < startX + Bounds.Width + cellWidthScaled; x += cellWidthScaled)
                {
                    canvas.DrawLine((float)x, 0, (float)x, (float)Bounds.Height, paint);
                }
                for (var y = startY; y < startY + Bounds.Height + cellWidthScaled; y += cellWidthScaled)
                {
                    canvas.DrawLine(0, (float)y, (float)Bounds.Width, (float)y, paint);
                }
            }
        }
    }
}