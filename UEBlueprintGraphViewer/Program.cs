using System;
using Avalonia;
using Avalonia.Media;

namespace UEBlueprintGraphViewer
{
    internal class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args) => BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .With(new SkiaOptions
                {
                    MaxGpuResourceSizeBytes = 1 * 1024 * 1024 * 1024
                })
                .With(new FontManagerOptions
                {
                    DefaultFamilyName = "avares://UEBlueprintGraphViewer/Resources#Roboto"
                })
                .LogToTrace();
    }
}
