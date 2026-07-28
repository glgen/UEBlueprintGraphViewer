using System;
using System.Reflection;
using Avalonia;
using Avalonia.Rendering;
using System.Text.RegularExpressions;

namespace UEBlueprintGraphViewer
{
    public static class SystemUtils
    {
        public static void SetAvaloniaFPS(double fps) => SetAvaloniaFPS(TimeSpan.FromSeconds(1d / fps));

        public static void SetAvaloniaFPS(TimeSpan fps)
        {
            (var intervalProperty, var renderTimer) = GetRenderTimerIntervalProperty();
            intervalProperty?.SetValue(renderTimer, fps);
        }

        private static (FieldInfo?, SleepLoopRenderTimer?) GetRenderTimerIntervalProperty()
        {
            Type locatorType = typeof(AvaloniaLocator);

            object? v = locatorType.GetProperty("Current")?.GetValue(null);
            object? loop = locatorType.GetMethod("GetService")?.Invoke(v, [typeof(IRenderLoop)]);
            var timer = loop?.GetType().GetField("_timer", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(loop);
            if (timer is SleepLoopRenderTimer renderTimer)
            {
                Type timerType = typeof(SleepLoopRenderTimer);
                var intervalProperty = timerType.GetField("_timeBetweenTicks", BindingFlags.Instance | BindingFlags.NonPublic);
                return (intervalProperty, renderTimer);
            }
            return (null, null);
        }

        public static void FixAvaloniaFPSLimit()
        {
            if (OperatingSystem.IsLinux())
            {
                // https://github.com/AvaloniaUI/Avalonia/discussions/18421
                try
                {
                    System.Diagnostics.Process p = new();
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.StartInfo.FileName = "xrandr";
                    p.Start();
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                    p.Dispose();
                    var match = Regex.Match(output, @"\d+x\d+\s{1,10}(\d{1,3}[\,\.]{1}\d{1,2})\*");
                    var fps = double.Parse(match.Groups[1].Value);
                    SetAvaloniaFPS(TimeSpan.FromSeconds(1d / fps));
                }
                catch { }
            }
        }
    }
}
