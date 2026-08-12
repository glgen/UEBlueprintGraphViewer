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
        _customDrawOperation = new(this);
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
            _mouseOverNode = null;
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
    
    private readonly CustomDrawOperation _customDrawOperation;

    public double CustomDrawOperationTime;
    private double _lastTime;
    private double _deltaTimeMs;
    
    public Vector Translation { get; private set; } = new(0, 0);
    

    public float Scaling = 1;
    
    public Point MousePosition => _lastMousePosition;


    private bool _isDragging;
    private bool _isDraggingNode;
    private bool _isSelecting;
    private Point _lastMousePosition;
    private Point _mousePosOnGraph;
    private Point _mousePosOnGraphOnPress;
    
    private List<BPNode> _selectedNodesBeforeSelection = [];
    private BPNode? _mouseOverNode;

    public bool DisableMoving;

    private int _gridSnap = 8;
    
    private object _updateLock = new();
    
    public override void Render(DrawingContext context)
    {
        double elapsed = (double)Stopwatch.GetTimestamp() / (Stopwatch.Frequency / 1000f);
        _deltaTimeMs = elapsed - _lastTime;
        _lastTime = elapsed;
        
        if (IsEffectivelyVisible)
        {
            _customDrawOperation.Bounds = Bounds;
            context.Custom(_customDrawOperation);
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
        _mousePosOnGraphOnPress = _mousePosOnGraph;
        if (props.IsLeftButtonPressed)
        {
            UpdateNodesSelection(e);
            _isDraggingNode = _mouseOverNode != null;
            _isSelecting = !_isDraggingNode;
            if (_isSelecting)
            {
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                {
                    _selectedNodesBeforeSelection = [..Editor?.SelectedNodes ?? []];
                }
                else
                {
                    _selectedNodesBeforeSelection = [];
                }
            }
        }
        if (props.IsRightButtonPressed || props.IsMiddleButtonPressed)
        {
            _isDragging = !DisableMoving;
        }
        
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Right)
        {
            if (_mousePosOnGraphOnPress == _mousePosOnGraph)
            {
                UpdateNodesSelection(e);
                var flyout = new Flyout() {FlyoutPresenterClasses = { "ContextMenu" }};
                flyout.Content = _mouseOverNode != null ? new NodeContextMenu(this, flyout) : new AddNodeMenu();
                flyout.ShowAt(this, true);
            }
        }
        
        if (_isSelecting)
            OnSelectionChanged?.Invoke();
        
        _isDragging = false;
        _isDraggingNode = false;
        _isSelecting = false;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        var newPos = e.GetPosition(this);
        var newGraphPos = (newPos + Translation) / Scaling;

        _mouseOverNode = Editor?.Nodes.FirstOrDefault(o => 
            _mousePosOnGraph.X > o.X && _mousePosOnGraph.Y > o.Y &&
            _mousePosOnGraph.X < o.Right && _mousePosOnGraph.Y < o.Bottom);
        
        if (_mouseOverNode != null)
        {
            if (_mousePosOnGraph.Y > _mouseOverNode.Y + (_mouseOverNode.HeaderHidden ? 0 : 30))
            {
                List<GraphPin> pins = _mousePosOnGraph.X < _mouseOverNode.X + _mouseOverNode.NodeWidth / 2f
                    ? _mouseOverNode.Input
                    : _mouseOverNode.Output;
                Editor?.MouseOverPin = pins.FirstOrDefault(o => !o.IsHidden && _mousePosOnGraph.Y < o.Y + 16);
            }
        }
        
        if (_isDragging)
        {
            var delta = _lastMousePosition - newPos;
            Translation += delta;
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                OnPanned?.Invoke(delta);
        }
        
        if (_isDraggingNode)
        {
            float offsetX = (float)(Math.Floor(newGraphPos.X / _gridSnap) * _gridSnap - Math.Floor(_mousePosOnGraph.X / _gridSnap) * _gridSnap);
            float offsetY = (float)(Math.Floor(newGraphPos.Y / _gridSnap) * _gridSnap - Math.Floor(_mousePosOnGraph.Y / _gridSnap) * _gridSnap);
            
            foreach (var node in Editor?.SelectedNodes ?? [])
            {
                node.SetPosition(node.X + offsetX, node.Y + offsetY);
            }
        }
        
        if (_isSelecting && Editor != null)
        {
            var minX = Math.Min(_mousePosOnGraphOnPress.X, _mousePosOnGraph.X);
            var minY = Math.Min(_mousePosOnGraphOnPress.Y, _mousePosOnGraph.Y);
            var maxX = Math.Max(_mousePosOnGraphOnPress.X, _mousePosOnGraph.X);
            var maxY = Math.Max(_mousePosOnGraphOnPress.Y, _mousePosOnGraph.Y);

            var selected = Editor.Nodes.Where(o =>
                o.X < maxX && minX < o.Right &&
                o.Y < maxY && minY < o.Bottom).ToList();
            var toAdd = selected.Where(o => !_selectedNodesBeforeSelection.Contains(o));
            var toRemove = selected.Where(o => _selectedNodesBeforeSelection.Contains(o));
            
            lock (_updateLock)
            {
                Editor.SelectedNodes.Clear();
                Editor.SelectedNodes.AddRange(_selectedNodesBeforeSelection);
                Editor.SelectedNodes.AddRange(toAdd);
                Editor.SelectedNodes.RemoveAll(o => toRemove.Contains(o));
            }
        }
        
        _lastMousePosition = newPos;
        _mousePosOnGraph = newGraphPos;
        
        InvalidateVisual();
    }

    private void UpdateNodesSelection(PointerEventArgs e)
    {
        if (Editor == null) return;
        Editor.SelectedPin = Editor.MouseOverPin;
        if (!Editor.SelectedNodes.Contains(_mouseOverNode))
        {
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                Editor.SelectedNodes.Clear();
            }
            
            if (_mouseOverNode != null)
            {
                Editor.SelectedNodes.Add(_mouseOverNode);
            }
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            Editor.SelectedNodes.Remove(_mouseOverNode);
        }
        OnSelectionChanged?.Invoke();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        var origTranslation = Translation;
        Zoom(e.Delta.Y, _lastMousePosition);
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

        private static readonly SKColor BackgroundColor = new SKColor(38,38,38);
        private static readonly SKPaint DarkerBackgroundPaint = SkiaUtils.MakePaint(21, 21, 21);
        private static readonly SKPaint GridPaint = SkiaUtils.MakeStroke(1, 43,43,43, 128);
        private static readonly SKPaint GridPaint2 = SkiaUtils.MakeStroke(2, 27,27,27);
        private static readonly SKPaint NodeBackg = SkiaUtils.MakePaint(15,17,15);
        private static readonly SKPaint NodeAddedBackg = SkiaUtils.MakePaint(40,88,40);
        private static readonly SKPaint NodeChangedBackg = SkiaUtils.MakePaint(136,136,40);
        private static readonly SKPaint NodeRemovedBackg = SkiaUtils.MakePaint(136,40,40);
        private static readonly SKPaint NodeBorder = SkiaUtils.MakeStroke(SKColors.Black, 1);
        private static readonly SKPaint NodeSelectedBorder = SkiaUtils.MakeStroke(SKColors.Orange, 1);
        private static readonly SKPaint NodeSearchResultBorder = SkiaUtils.MakeStroke(SKColors.White, 3);
        private static readonly SKPaint NodeBreakpointBorder = SkiaUtils.MakeStroke(SKColors.Firebrick, 5);
        private static readonly SKPaint NodeHitBreakpointBorder = SkiaUtils.MakeStroke(SKColors.Orange, 5);
        private static readonly SKPaint NodeValue = SkiaUtils.MakePaint(34,34,34);
        private static readonly SKPaint NodeValueBorder = SkiaUtils.MakeStroke(1, 153, 153, 153);
        private static readonly SKPaint Selection = SkiaUtils.MakePaint(30,50,80, 70);
        private static readonly SKPaint SelectionBorder = SkiaUtils.MakeStroke(1, 30,80,150);
        private static readonly SKPaint TextPaint = SkiaUtils.MakePaint(255, 255, 255, 255);
        private static readonly SKPaint TextPaint2 = SkiaUtils.MakePaint(255, 255, 255, 128);
        
        private const byte NodeBodyAlpha = 235;
        private const byte CompactNodeBodyAlpha = 175;

        private static readonly SKFont TextFont = SkiaUtils.MakeFont(12.5f);
        private static readonly SKFont TextFontBody = SkiaUtils.MakeFont( 28);
        
        
        public void Render(ImmediateDrawingContext context)
        {
            try
            {
                var stopWatch = Stopwatch.StartNew();
                
                if (SkiaUtils.GetLeaseCanvas(context) is not {} canvas)
                    return;
                
                canvas.Save();
                
                // Fill background of entire control
                canvas.Clear(BackgroundColor);
                
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
                    
                    lock (view._updateLock)
                    {
                        foreach (var node in view.Editor.SelectedNodes)
                        {
                            canvas.DrawRoundRect(node.X, node.Y,
                                node.NodeWidth, node.NodeHeight, 5, 5, NodeSelectedBorder);
                        }
                        
                        foreach (var node in view.Editor.DebuggerBreakpoints)
                        {
                            canvas.DrawRoundRect(node.X, node.Y,
                                node.NodeWidth, node.NodeHeight, 5, 5, NodeBreakpointBorder);
                        }

                        if (view.Editor.CurrentDebuggerNode is { } debuggerNode)
                        {
                            canvas.DrawRoundRect(debuggerNode.X, debuggerNode.Y,
                                debuggerNode.NodeWidth, debuggerNode.NodeHeight, 5, 5, NodeHitBreakpointBorder);
                        }
                        
                        foreach (var node in view.Editor.SearchResult)
                        {
                            canvas.DrawRoundRect(node.X, node.Y,
                                node.NodeWidth, node.NodeHeight, 5, 5, NodeSearchResultBorder);
                        }
                    }
                }

                if (view._mouseOverNode is not null && view.Editor?.MouseOverPin is {} mouseOverPin)
                    RenderPinSelection(mouseOverPin, canvas);
                
                canvas.Restore();

                if (view._isSelecting)
                {
                    var start = view._mousePosOnGraphOnPress * view.Scaling - view.Translation;
                    var end = view._mousePosOnGraph * view.Scaling - view.Translation;
                    
                    SKRect rect = new((float)start.X, (float)start.Y, (float)end.X, (float)end.Y);
                    canvas.DrawRect(rect, Selection);
                    canvas.DrawRect(rect, SelectionBorder);
                }

                canvas.DrawRect( new SKRect(0, (float)Bounds.Height - 25, (float)Bounds.Width, (float)Bounds.Height), DarkerBackgroundPaint);
                float textY = (float)Bounds.Height - 8;
                canvas.DrawText2($"Zoom: {view.Scaling:N2}", 5, textY, SKTextAlign.Left, TextFont, TextPaint);
                canvas.DrawText2($"FPS: {Math.Round(1000f / view._deltaTimeMs)}", 120, textY, SKTextAlign.Left, TextFont, TextPaint);
                
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
                width += TextFont.MeasureText(pin.PinFriendlyName);
                if (pin.IsInput && !pin.IsConnected)
                    width += Math.Clamp(TextFont.MeasureText(pin.Value), 30, 400) + 10;
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

            SKRect popupRect = new SKRect((float)view._mousePosOnGraph.X, (float)view._mousePosOnGraph.Y + 15,
                (float)view._mousePosOnGraph.X + 400, (float)view._mousePosOnGraph.Y + 200);
            if (Settings.DebuggerMode && pin is { IsConnected: true, Property: not null } && view.Editor != null)
            {
                string text = $"{pin.Property.Name} -> {view.Editor.DebuggerLocals.FirstOrDefault(o => o.Name == pin.Property.Name)?.DefaultValue}";
                
                SkiaUtils.DrawTextWithWrapping(canvas, text, popupRect, TextFont, TextPaint, height =>
                {
                    SKRect popupRect2 = new SKRect(popupRect.Left - 5, popupRect.Top - 5, popupRect.Right + 5, popupRect.Top + 5 + height);
                    canvas.DrawRoundRect(popupRect2, 5, 5, NodeValue);
                    canvas.DrawRoundRect(popupRect2, 5, 5, NodeValueBorder);
                });
            }
            else if (pin.Value.Length > 55)
            {
                string formatted = JsonConvert.SerializeObject(pin.Value).Trim('"');
                SkiaUtils.DrawTextWithWrapping(canvas, formatted, popupRect, TextFont, TextPaint, height =>
                {
                    SKRect popupRect2 = new SKRect(popupRect.Left - 5, popupRect.Top - 5, popupRect.Right + 5, popupRect.Top + 5 + height);
                    canvas.DrawRoundRect(popupRect2, 5, 5, NodeValue);
                    canvas.DrawRoundRect(popupRect2, 5, 5, NodeValueBorder);
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
                ChangeStatus.None => NodeBackg,
                ChangeStatus.Added => NodeAddedBackg,
                ChangeStatus.Removed => NodeRemovedBackg,
                ChangeStatus.Changed => NodeChangedBackg,
                _ => NodeBackg,
            };
            
            byte bodyAlpha = node.NodeType is "K2Node_VariableGet" or "K2Node_VariableSet" or "K2Node_PromotableOperator"
                ? CompactNodeBodyAlpha
                : NodeBodyAlpha;
            backg.Color = backg.Color.WithAlpha(bodyAlpha);

            if (view.Scaling > 0.2)
                canvas.DrawRoundRect(nodeRect, 5,5, backg);
            else
                canvas.DrawRect(nodeRect, backg);
            
            if (node is { ChangeStatus: ChangeStatus.None, TintPin: not null } && view.Scaling > 0.2)
            {
                SKColor edge = ViewData.NodeBaseColor;
                SKColor mid = ViewData.GetNodeTintColor(node.TintPin.PinType.PinCategory);
                using SKPaint tintPaint = new()
                {
                    IsAntialias = true,
                    Shader = SKShader.CreateLinearGradient(
                        new SKPoint(0, 0), new SKPoint(node.NodeWidth, 0),
                        [edge, mid, edge], [0f, 0.5f, 1f], SKShaderTileMode.Clamp),
                    Color = SKColors.Black.WithAlpha(bodyAlpha),
                };

                if (node.TintHeaderOnly)
                {
                    SKRect headerRect = new(0, 0, node.NodeWidth, 34);
                    using SKRoundRect rr = new(headerRect, 0);
                    rr.SetNinePatch(headerRect, 5, 5, 5, 0);
                    canvas.DrawRoundRect(rr, tintPaint);
                }
                else
                {
                    canvas.DrawRoundRect(nodeRect, 5, 5, tintPaint);
                }
            }
            
            if (!node.HeaderHidden)
            {
                SKRect nodeHeaderRect = new SKRect(0, 0, node.NodeWidth, 25);
                SKPaint headerPaint = ViewData.GetNodeHeaderColorSK(node.NodeType[7..], node.Pure);
                headerPaint.Color = headerPaint.Color.WithAlpha(NodeBodyAlpha);
                if (view.Scaling > 0.2)
                {
                    using SKRoundRect a = new(nodeHeaderRect, 0);
                    a.SetNinePatch(nodeHeaderRect, 5,5,5,0);
                    canvas.DrawRoundRect(a, headerPaint);
                    canvas.DrawText2(node.Name, node.HeaderCenter ? node.NodeWidth / 2f : 7, 17, node.HeaderCenter ? SKTextAlign.Center : SKTextAlign.Left, TextFont, TextPaint);
                }
                else
                {
                    canvas.DrawRect(nodeHeaderRect, headerPaint);
                }
            }

            if (view.Scaling > 0.2)
            {
                if (node.ShowNameAsBody)
                    canvas.DrawText2(node.Name, node.NodeWidth / 2f, node.NodeHeight / 2f + 10, SKTextAlign.Center, TextFontBody, TextPaint2);
                
                if (node.CompactTitle)
                    canvas.DrawText2(node.Name, node.NodeWidth / 2f, 22, SKTextAlign.Center, TextFont, TextPaint);

                canvas.DrawRoundRect(nodeRect, 5,5, NodeBorder);

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
                canvas.DrawText2(pin.PinFriendlyName, pin.IsOutput ? node.NodeWidth - 35 : 35, textY, pin.IsOutput ? SKTextAlign.Right : SKTextAlign.Left, TextFont, TextPaint);
                nameWidth = TextFont.MeasureText(pin.PinFriendlyName) + 35;
            }
            
            if (pin is { IsInput: true, IsConnected: false, PinType.PinCategory: not EngineBPData.PinType.exec })
            {
                var valueFormatted = JsonConvert.SerializeObject(pin.Value).Trim('"');
                var valueWidth = Math.Clamp(TextFont.MeasureText(valueFormatted), 30, 400) + 10;
                var rect = SKRect.Create(nameWidth + 5, connectorY - 10, valueWidth, 20);
                canvas.DrawRoundRect(rect, 5,5, NodeValue);
                canvas.DrawRoundRect(rect, 5,5, NodeValueBorder);
                    
                string value = valueFormatted.Length > 55 ? $"{valueFormatted[..55]}..." : valueFormatted;
                canvas.DrawText2(value, nameWidth + 5 + 5, textY, SKTextAlign.Left, TextFont, TextPaint);
            }
        }
        
        private void RenderGrid(SKCanvas canvas)
        {
            if (view.Scaling > 0.4)
                RenderLines(16, GridPaint);

            if (view.Scaling > 0.3)
                RenderLines(128, GridPaint2);

            if (view.Scaling is <= 0.3f and > 0.05f)
            {
                RenderLines(128, GridPaint);
                RenderLines(1024, GridPaint2);
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