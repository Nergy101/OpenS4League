using Avalonia;
using Avalonia.Headless;
using S4LResourceTool.App;
using S4LResourceTool.App.HeadlessTests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace S4LResourceTool.App.HeadlessTests;

public static class TestAppBuilder
{
    // UseHeadlessDrawing = false -> real Skia rendering so image decoding / WriteableBitmap work.
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .WithInterFont()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
