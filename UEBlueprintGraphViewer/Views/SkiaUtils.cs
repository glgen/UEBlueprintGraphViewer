using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Skia;
using SkiaSharp;

namespace UEBlueprintGraphViewer.Views;

public static class SkiaUtils
{
    public static SKPaint MakePaint(byte red, byte green, byte blue, byte alpha = 255) 
        => new() { Color = new SKColor(red, green, blue, alpha), IsAntialias = true };
    public static SKPaint MakeStroke(float width, byte red, byte green, byte blue, byte alpha = 255) 
        => new() { Color = new SKColor(red, green, blue, alpha), IsAntialias = true, IsStroke = true, StrokeWidth = width };
    public static SKPaint MakePaint(SKColor color) => new() { Color = color, IsAntialias = true };
    public static SKPaint MakeStroke(SKColor color, float width) => new() { Color = color, IsAntialias = true, IsStroke = true, StrokeWidth = width };

        
    public static SKFont MakeFont(float size)
    {
        var uri = new Uri("avares://UEBlueprintGraphViewer/Resources/Roboto-Regular.ttf");
        using Stream fontStream = AssetLoader.Open(uri);
        return new SKFont(SKTypeface.FromStream(fontStream), size);
    }
    
    public static SKCanvas? GetLeaseCanvas(ImmediateDrawingContext context)
    {
        var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        using var lease = leaseFeature?.Lease();
        return lease?.SkCanvas;
    }
    
    public static void DrawTextWithWrapping(SKCanvas canvas, string text, SKRect rect, SKFont font, SKPaint paint, Action<float> beforeRendering)
    {
        float spaceWidth = font.MeasureText(" ");
        float wordX = rect.Left;
        float wordY = rect.Top + font.Size;

        var words = text.Split(' ');
        SKPoint[] coords = new SKPoint[words.Length];

        for (int i = 0; i < words.Length; i++)
        {
            var word = words[i];
            float wordWidth = font.MeasureText(word);
            if (wordWidth > rect.Right - wordX)
            {
                wordY += font.Spacing;
                wordX = rect.Left;
            }
            
            coords[i] = new SKPoint(wordX, wordY);
            wordX += wordWidth + spaceWidth;
        }

        beforeRendering?.Invoke(wordY - rect.Top);
        
        for (int i = 0; i < words.Length; i++)
        {
            canvas.DrawText(words[i], coords[i], SKTextAlign.Left, font, paint);
        }
    }
    
    public static void DrawPathAtLocation(SKCanvas canvas, SKPath p, float x, float y,
        SKPaint connectorColor)
    {
        using SKPath path = new();
        p.Transform(SKMatrix.CreateTranslation(x, y), path);
        canvas.DrawPath(path, connectorColor);
    }
}