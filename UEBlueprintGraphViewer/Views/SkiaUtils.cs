using System;
using System.Globalization;
using System.IO;
using System.Linq;
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
    
    // adapted from https://www.mrumpler.at/the-trouble-with-text-rendering-in-skiasharp-and-harfbuzz/
    public static void DrawText2(this SKCanvas canvas, string text,
        float x,
        float y,
        SKTextAlign textAlign,
        SKFont font,
        SKPaint paint)
    {
        if (font.ContainsGlyphs(text))
        {
            canvas.DrawText(text, x, y, textAlign, font, paint);
            return;
        }
        
        int start = 0;
        TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(text);
        bool notAtEnd;

        SKTypeface? fallback = null;
        
        while (enumerator.MoveNext())
        {
            var textElement = enumerator.GetTextElement();
            if (!font.ContainsGlyphs(textElement))
            {
                while ((notAtEnd = enumerator.MoveNext())
                    && !font.ContainsGlyphs(enumerator.GetTextElement()));
                
                var subtext = notAtEnd
                    ? text.Substring(start, enumerator.ElementIndex - start)
                    : text.Substring(start);

                var firstCodepoint = subtext.EnumerateRunes().First().Value;

                fallback = SKFontManager.Default.MatchCharacter(
                    font.Typeface.FamilyName,
                    font.Typeface.FontStyle,
                    null,
                    firstCodepoint);

                if (fallback != null)
                    break;
                
                start = notAtEnd ? enumerator.ElementIndex : text.Length;
            }
        }

        SKFont? fallbackFont = null;
        if (fallback != null)
            fallbackFont = new SKFont(fallback, font.Size);
        
        canvas.DrawText(text, x, y, fallbackFont ?? font, paint);
        fallbackFont?.Dispose();
    }
    
    public static void DrawPathAtLocation(SKCanvas canvas, SKPath p, float x, float y,
        SKPaint connectorColor)
    {
        using SKPath path = new();
        p.Transform(SKMatrix.CreateTranslation(x, y), path);
        canvas.DrawPath(path, connectorColor);
    }
}