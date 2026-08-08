using Avalonia.Media;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;
using static UEBlueprintGraphViewer.Engine.EngineBPData;

namespace UEBlueprintGraphViewer.Views
{
    internal static class ViewData
    {
        
        public static FrozenDictionary<PinType, SKPaint> SKPinsColors = new Dictionary<PinType, SKPaint>()
        {
            { PinType.exec, FromColor(SKColors.White) },
            { PinType.Bool, FromColor(SKColors.Crimson) },
            { PinType.Byte, FromColor(SKColors.DarkCyan) },
            { PinType.Enum, FromColor(SKColors.DarkCyan) },
            { PinType.Int, FromColor(SKColors.MediumAquamarine) },
            { PinType.Int64, FromColor(SKColors.PaleGreen) },
            { PinType.Float, FromColor(SKColors.Lime) },
            { PinType.Double, FromColor(SKColors.Lime) },
            { PinType.Name, FromColor(SKColors.BlueViolet) },
            { PinType.String, FromColor(SKColors.Magenta) },
            { PinType.Text, FromColor(SKColors.HotPink) },
            { PinType.Vector, FromColor(SKColors.Gold) },
            { PinType.Rotator, FromColor(SKColors.MediumSlateBlue) },
            { PinType.Transform, FromColor(SKColors.Tomato) },
            { PinType.Interface, FromColor(SKColors.Khaki) },
            { PinType.Object, FromColor(SKColors.DeepSkyBlue) },
            { PinType.SoftObject, FromColor(SKColors.PaleTurquoise) },
            { PinType.Class, FromColor(SKColors.Indigo) },
            { PinType.SoftClass, FromColor(SKColors.Violet) },
            { PinType.Delegate, FromColor(SKColors.Crimson) },
            { PinType.Struct, FromColor(SKColors.RoyalBlue) },
            { PinType.Wildcard, FromColor(SKColors.Gray) },
            { PinType.Unknown, FromColor(SKColors.Red) },
        }.ToFrozenDictionary();

        public static FrozenDictionary<PinType, SKPaint> SKPinsStrokes =
            SKPinsColors.Select(o => new KeyValuePair<PinType, SKPaint>(o.Key, FromColorStroke(o.Value.Color))).ToFrozenDictionary();
        
        
        public static FrozenDictionary<string, SKPaint> SKNodesColors = new Dictionary<string, SKPaint>()
        {
            { "AddDelegate", FromColor(new SKColor(84, 129, 159)) },
            { "CallDelegate", FromColor(new SKColor(84, 129, 159)) },
            { "CallFunction", FromColor(new SKColor(84, 129, 159)) },
            { "CallArrayFunction", FromColor(new SKColor(84, 129, 159)) },
            { "ClearDelegate", FromColor(new SKColor(84, 129, 159)) },
            { "RemoveDelegate", FromColor(new SKColor(84, 129, 159)) },
            { "SpawnActorFromClass", FromColor(new SKColor(84, 129, 159)) },
            { "GenericCreateObject", FromColor(new SKColor(84, 129, 159)) },
            { "AssignmentStatement", FromColor(new SKColor(84, 129, 159)) },
            { "CreateWidget", FromColor(new SKColor(84, 129, 159)) },
            { "Tunnel", FromColor(new SKColor(84, 129, 159)) },
            { "Event", FromColor(new SKColor(160, 27, 43)) },
            { "InputAction", FromColor(new SKColor(160, 27, 43)) },
            { "InputKey", FromColor(new SKColor(160, 27, 43)) },
            { "InputAxisKey", FromColor(new SKColor(160, 27, 43)) },
            { "InputAxisEvent", FromColor(new SKColor(160, 27, 43)) },
            { "EnhancedInputAction", FromColor(new SKColor(160, 27, 43)) },
            { "FunctionEntry", FromColor(new SKColor(134, 39, 159)) },
            { "FunctionResult", FromColor(new SKColor(134, 39, 159)) },
            { "IfThenElse", FromColor(new SKColor(130, 130, 130)) },
            { "ExecutionSequence", FromColor(new SKColor(130, 130, 130)) },
            { "MacroInstance", FromColor(new SKColor(130, 130, 130)) },
            { "VariableSet", FromColor(new SKColor(85, 85, 85)) },
            { "DynamicCast", FromColor(new SKColor(19, 114, 118)) },
            { "ClassDynamicCast", FromColor(new SKColor(63, 20, 111)) },
            { "Select", FromColor(new SKColor(102, 136, 97)) },
            { "MakeArray", FromColor(new SKColor(102, 136, 97)) },
            { "MakeMap", FromColor(new SKColor(102, 136, 97)) },
            { "TemporaryVariable", FromColor(new SKColor(102, 136, 97)) },
            { "SwitchInteger", FromColor(new SKColor(149, 149, 16)) },
            { "SwitchString", FromColor(new SKColor(149, 149, 16)) },
            { "SwitchName", FromColor(new SKColor(149, 149, 16)) },
            { "SwitchEnum", FromColor(new SKColor(149, 149, 16)) },
            { "Timeline", FromColor(new SKColor(149, 149, 16)) },
            { "MakeStruct", FromColor(new SKColor(20, 50, 130)) },
        }.ToFrozenDictionary();

        public static SKPaint SKUnknownColor = FromColor(SKColors.Red);
        
        public static readonly SKColor NodeBaseColor = new(40, 40, 40);
        private static readonly FrozenDictionary<PinType, SKColor> NodeTintColors =
            SKPinsColors.Select(o => new KeyValuePair<PinType, SKColor>(
                o.Key, BlendOver(o.Value.Color, NodeBaseColor, 0.18f))).ToFrozenDictionary();

        public static SKColor GetNodeTintColor(PinType? type)
        {
            if (type != null && NodeTintColors.TryGetValue(type.Value, out SKColor color))
                return color;
            return SKUnknownColor.Color;
        }

        private static SKColor BlendOver(SKColor top, SKColor bottom, float alpha)
        {
            byte Mix(byte t, byte b) => (byte)(t * alpha + b * (1 - alpha));
            return new SKColor(Mix(top.Red, bottom.Red), Mix(top.Green, bottom.Green), Mix(top.Blue, bottom.Blue));
        }
        
        public static SKPaint GetPinColorSK(PinType? type)
        {
            if (type != null && SKPinsColors.TryGetValue(type.Value, out SKPaint? color))
                return color;
            return SKUnknownColor;
        }
        
        public static SKPaint GetPinStrokeSK(PinType? type)
        {
            if (type != null && SKPinsStrokes.TryGetValue(type.Value, out SKPaint? color))
                return color;
            return SKUnknownColor;
        }
        
        public static SKPaint GetNodeColorSK(string type)
        {
            if (type != null && SKNodesColors.TryGetValue(type, out SKPaint? color))
            {
                return color;
            }
            return SKUnknownColor;
        }

        private static SKPaint FromColor(SKColor color)
        {
            return new SKPaint { Color = color, IsAntialias = true };
        }
        
        private static SKPaint FromColorStroke(SKColor color)
        {
            return new SKPaint { Color = color, IsAntialias = true, IsStroke = true, StrokeWidth = 2};
        }
    }
}
