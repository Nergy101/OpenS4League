using Avalonia.Headless.XUnit;
using S4LResourceTool.App.Services;
using S4LResourceTool.App.ViewModels;
using S4LResourceTool.App.Views;
using Xunit;

namespace S4LResourceTool.App.HeadlessTests;

public class SmokeTests
{
    private sealed class StubUi(string clientDir) : IUiServices
    {
        public string? SavePath { get; set; }

        public Task<string?> PickFolderAsync(string title) => Task.FromResult<string?>(clientDir);
        public Task<IReadOnlyList<string>> PickFilesAsync(string t, string? n = null, string? e = null)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task<string?> PickSaveFileAsync(string s, string? e = null) => Task.FromResult(SavePath);
        public Task<string?> PickSaveImageAsync(string s) => Task.FromResult(SavePath);
        public Task<string?> PickFromListAsync(string t, IReadOnlyList<(string Label, string Value)> o)
            => Task.FromResult<string?>(o.Count > 0 ? o[0].Value : null);
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(true);
    }

    private static FolderNode? Find(FolderNode node, string fullPath)
    {
        if (node.FullPath == fullPath) return node;
        foreach (var c in node.Children)
        {
            var hit = Find(c, fullPath);
            if (hit is not null) return hit;
        }
        return null;
    }

    private static async Task<MainWindowViewModel> OpenAsync(ArchiveFixture fx)
    {
        var vm = new MainWindowViewModel { Ui = new StubUi(fx.ClientDir) };
        await vm.SelectDirectoryCommand.ExecuteAsync(null);
        return vm;
    }

    [AvaloniaFact]
    public async Task Opening_archive_builds_folder_tree_and_lists_files()
    {
        using var fx = new ArchiveFixture();
        var vm = await OpenAsync(fx);

        Assert.Single(vm.Folders);
        var root = vm.Folders[0];
        Assert.NotNull(Find(root, "gui/hud"));
        Assert.NotNull(Find(root, "gui/texture"));
        Assert.NotNull(Find(root, "sound/effects"));

        // Root-level files.
        vm.SelectedFolder = root;
        Assert.Contains(vm.Files, r => r.Name == "readme.txt");
    }

    [AvaloniaFact]
    public async Task Text_file_produces_text_preview()
    {
        using var fx = new ArchiveFixture();
        var vm = await OpenAsync(fx);

        vm.SelectedFolder = Find(vm.Folders[0], "gui/hud");
        var ini = Assert.Single(vm.Files, r => r.Name == "config.ini");
        vm.SelectedFile = ini;
        await vm.PreviewTask!;

        Assert.True(vm.ShowText);
        Assert.False(vm.ShowImage);
        Assert.Contains("scale=1.0", vm.PreviewText);
    }

    [AvaloniaFact]
    public async Task Dds_file_decodes_to_image_preview()
    {
        using var fx = new ArchiveFixture();
        var vm = await OpenAsync(fx);

        vm.SelectedFolder = Find(vm.Folders[0], "gui/texture");
        var dds = Assert.Single(vm.Files, r => r.Name == "logo.dds");
        vm.SelectedFile = dds;
        await vm.PreviewTask!;

        Assert.True(vm.ShowImage);
        Assert.NotNull(vm.PreviewImage);
        Assert.Equal(4, vm.PreviewImage!.PixelSize.Width);
        Assert.Equal(4, vm.PreviewImage.PixelSize.Height);
        Assert.True(vm.ShowScalePicker, "scale picker should appear for a decodable DDS");
    }

    [AvaloniaFact]
    public async Task Dds_preview_upscales_4x_without_writing_archive()
    {
        using var fx = new ArchiveFixture();
        var vm = await OpenAsync(fx);

        vm.SelectedFolder = Find(vm.Folders[0], "gui/texture");
        var dds = Assert.Single(vm.Files, r => r.Name == "logo.dds");
        vm.SelectedFile = dds;
        await vm.PreviewTask!;

        // The archive entry must be untouched by upscaling (preview-only).
        var before = dds.Entry.GetData();

        // Switch the preview to 4x and wait for the async upscale to land.
        vm.PreviewScale = "4x";
        await WaitUntilAsync(() => vm.PreviewImage is not null && vm.PreviewImage!.PixelSize.Width == 16);

        Assert.Equal(16, vm.PreviewImage!.PixelSize.Width);
        Assert.Equal(16, vm.PreviewImage.PixelSize.Height);
        Assert.Contains("4x", vm.Status);

        Assert.False(dds.IsModified);
        Assert.Equal(before, dds.Entry.GetData());

        // Back to 1x restores the cached original without re-decoding.
        vm.PreviewScale = "1x";
        Assert.Equal(4, vm.PreviewImage!.PixelSize.Width);
    }

    [AvaloniaFact]
    public async Task Ai_upscale_uses_realesrgan_when_installed()
    {
        var exe = AiTextureUpscaler.FindExecutable(new AppSettings());
        if (exe is null) return; // soft-skip: Real-ESRGAN not installed on this machine

        using var fx = new ArchiveFixture();
        var vm = await OpenAsync(fx);

        vm.SelectedFolder = Find(vm.Folders[0], "gui/texture");
        var big = Assert.Single(vm.Files, r => r.Name == "big.dds"); // 128x128
        vm.SelectedFile = big;
        await vm.PreviewTask!;
        Assert.Equal(128, vm.PreviewImage!.PixelSize.Width);

        vm.PreviewScale = "4x";
        await WaitUntilAsync(() => vm.PreviewImage is not null && vm.PreviewImage!.PixelSize.Width == 512, 60_000);

        Assert.Equal(512, vm.PreviewImage!.PixelSize.Width);
        Assert.Equal(512, vm.PreviewImage.PixelSize.Height);
        Assert.Contains("Real-ESRGAN", vm.Status);
    }

    [AvaloniaFact]
    public async Task Export_upscaled_writes_png()
    {
        using var fx = new ArchiveFixture();
        var outPath = Path.Combine(Path.GetTempPath(), "s4l_export_" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            var stub = new StubUi(fx.ClientDir) { SavePath = outPath };
            var vm = new MainWindowViewModel { Ui = stub };
            await vm.SelectDirectoryCommand.ExecuteAsync(null);

            vm.SelectedFolder = Find(vm.Folders[0], "gui/texture");
            var dds = Assert.Single(vm.Files, r => r.Name == "logo.dds"); // 4x4 -> software 4x
            vm.SelectedFile = dds;
            await vm.PreviewTask!;

            vm.PreviewScale = "4x";
            await WaitUntilAsync(() => vm.PreviewImage is not null && vm.PreviewImage!.PixelSize.Width == 16);

            Assert.True(vm.CanExportUpscaled);
            await vm.ExportUpscaledCommand.ExecuteAsync(null);

            Assert.True(File.Exists(outPath), "exported PNG should exist");
            var dec = PngCodec.Decode(File.ReadAllBytes(outPath));
            Assert.NotNull(dec);
            Assert.Equal(16, dec.Value.Width);
            Assert.Equal(16, dec.Value.Height);
        }
        finally { try { File.Delete(outPath); } catch { } }
    }

    [AvaloniaFact]
    public async Task Export_upscaled_dds_when_texconv_installed()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var texconv = Path.Combine(home, "Downloads", "Texture Upscaler", "texconv.exe");
        if (!File.Exists(texconv)) return; // soft-skip: texconv not installed

        using var fx = new ArchiveFixture();
        var outPath = Path.Combine(Path.GetTempPath(), "s4l_export_" + Guid.NewGuid().ToString("N") + ".dds");
        try
        {
            var stub = new StubUi(fx.ClientDir) { SavePath = outPath };
            var vm = new MainWindowViewModel { Ui = stub };
            await vm.SelectDirectoryCommand.ExecuteAsync(null);

            vm.SelectedFolder = Find(vm.Folders[0], "gui/texture");
            var dds = Assert.Single(vm.Files, r => r.Name == "logo.dds");
            vm.SelectedFile = dds;
            await vm.PreviewTask!;

            vm.PreviewScale = "4x";
            await WaitUntilAsync(() => vm.PreviewImage is not null && vm.PreviewImage!.PixelSize.Width == 16);

            await vm.ExportUpscaledCommand.ExecuteAsync(null);

            Assert.True(File.Exists(outPath), "exported DDS should exist");
            var head = File.ReadAllBytes(outPath).Take(4).ToArray();
            Assert.Equal(new byte[] { 0x44, 0x44, 0x53, 0x20 }, head); // "DDS "
        }
        finally { try { File.Delete(outPath); } catch { } }
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!condition())
        {
            if (sw.ElapsedMilliseconds > timeoutMs)
                throw new Xunit.Sdk.XunitException("Timed out waiting for condition.");
            await Task.Delay(20);
        }
    }

    [AvaloniaFact]
    public async Task Binary_file_produces_info_preview()
    {
        using var fx = new ArchiveFixture();
        var vm = await OpenAsync(fx);

        vm.SelectedFolder = Find(vm.Folders[0], "sound/effects");
        var bin = Assert.Single(vm.Files, r => r.Name == "blip.bin");
        vm.SelectedFile = bin;
        await vm.PreviewTask!;

        Assert.True(vm.ShowInfo);
        Assert.False(vm.ShowText);
        Assert.False(vm.ShowImage);
    }

    [AvaloniaFact]
    public async Task Search_filters_across_the_archive()
    {
        using var fx = new ArchiveFixture();
        var vm = await OpenAsync(fx);

        vm.SearchText = "config";
        Assert.Contains(vm.Files, r => r.Name == "config.ini");
        Assert.DoesNotContain(vm.Files, r => r.Name == "readme.txt");
    }

    [AvaloniaFact]
    public async Task Main_window_loads_and_binds_without_error()
    {
        using var fx = new ArchiveFixture();
        var vm = await OpenAsync(fx);

        var window = new MainWindow { DataContext = vm };
        window.Show(); // proves App + MainWindow AXAML load and bindings resolve
        Assert.NotNull(window.DataContext);
        window.Close();
    }

    // Real end-to-end test against an actual S4 League client, if one is available on this machine.
    // Set S4L_TEST_CLIENT to a client folder, or drop one at the well-known path below.
    [AvaloniaFact]
    public async Task Real_client_opens_and_previews_when_present()
    {
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("S4L_TEST_CLIENT"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads", "S4Max Game 4.5.0.1"),
        };
        var clientDir = candidates.FirstOrDefault(p =>
            !string.IsNullOrEmpty(p) && File.Exists(Path.Combine(p!, "resource.s4hd")));
        if (clientDir is null)
            return; // soft-skip: no real client on this machine

        var vm = new MainWindowViewModel { Ui = new StubUi(clientDir) };
        await vm.SelectDirectoryCommand.ExecuteAsync(null);

        Assert.Single(vm.Folders);
        Assert.NotEmpty(vm.Folders[0].Children); // populated folder tree (nested under a few roots)

        // Find and preview a real DDS texture through the full app pipeline.
        vm.SearchText = ".dds";
        Assert.True(vm.Files.Count > 100, "expected many .dds resources from search");
        var dds = vm.Files.FirstOrDefault(r => r.Name.EndsWith(".dds", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(dds);
        vm.SelectedFile = dds;
        await vm.PreviewTask!;
        Assert.True(vm.ShowImage, "DDS texture should decode to an image preview");
        Assert.NotNull(vm.PreviewImage);

        // And a real text/xml resource.
        vm.SearchText = ".xml";
        var xml = vm.Files.FirstOrDefault(r => r.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
        if (xml is not null)
        {
            vm.SelectedFile = xml;
            await vm.PreviewTask!;
            Assert.True(vm.ShowText);
        }
    }
}
