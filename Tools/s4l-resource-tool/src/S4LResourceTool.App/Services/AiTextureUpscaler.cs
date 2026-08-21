using System.Diagnostics;

namespace S4LResourceTool.App.Services;

/// <summary>
/// Real-ESRGAN (ncnn-vulkan) AI upscaler for the texture preview. Mirrors the approach used by
/// the TextureUpscaler project: the colour is upscaled with alpha forced opaque, the alpha
/// channel is upscaled separately as a grayscale mask, then the two results are recombined —
/// so transparency stays clean. Works only when the external <c>realesrgan-ncnn-vulkan.exe</c>
/// binary and its models are present (needs a Vulkan-capable GPU).
/// </summary>
public static class AiTextureUpscaler
{
    /// <summary>
    /// Locates the Real-ESRGAN executable: first the configured path, then a few well-known
    /// locations. Returns null when it isn't installed anywhere we know about.
    /// </summary>
    public static string? FindExecutable(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.RealesrganPath) && File.Exists(settings.RealesrganPath))
            return settings.RealesrganPath;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] candidates =
        {
            Path.Combine(home, "Downloads", "Texture Upscaler", "Realesrgan", "realesrgan-ncnn-vulkan.exe"),
            Path.Combine(AppContext.BaseDirectory, "Realesrgan", "realesrgan-ncnn-vulkan.exe"),
        };
        foreach (var c in candidates)
            if (File.Exists(c))
                return c;
        return null;
    }

    /// <summary>
    /// AI-upscales a BGRA buffer by <paramref name="factor"/> (2, 4 or 8). Uses temp PNG files
    /// for the Real-ESRGAN round-trip and returns the upscaled BGRA. Runs synchronously (the
    /// external process is awaited); call on a background thread.
    /// </summary>
    public static UpscaledTexture Upscale(byte[] bgra, int w, int h, int factor, string exePath)
    {
        var exeDir = Path.GetDirectoryName(exePath)!;
        var modelsDir = Path.Combine(exeDir, "models");
        var workDir = Path.Combine(Path.GetTempPath(), "S4LResourceTool", "realesrgan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            var colorPath = Path.Combine(workDir, "color.png");
            var alphaPath = Path.Combine(workDir, "alpha.png");

            // Split: opaque colour + separate alpha mask.
            var opaque = new byte[bgra.Length];
            var alpha = new byte[w * h];
            for (int i = 0, a = 0; i < bgra.Length; i += 4, a++)
            {
                opaque[i] = bgra[i];
                opaque[i + 1] = bgra[i + 1];
                opaque[i + 2] = bgra[i + 2];
                opaque[i + 3] = 255;
                alpha[a] = bgra[i + 3];
            }

            File.WriteAllBytes(colorPath, PngCodec.EncodeBgra(opaque, w, h));
            File.WriteAllBytes(alphaPath, PngCodec.EncodeGray(alpha, w, h));

            // Real-ESRGAN supports -s 2/3/4. 2x and 4x are single passes; 8x = a 4x pass
            // followed by a 2x pass (4 * 2 = 8).
            int[] scales = factor switch { 2 => new[] { 2 }, 4 => new[] { 4 }, 8 => new[] { 4, 2 }, _ => throw new ArgumentOutOfRangeException(nameof(factor)) };

            var curColor = colorPath;
            var curAlpha = alphaPath;
            for (int p = 0; p < scales.Length; p++)
            {
                var colorOut = Path.Combine(workDir, $"color_{p}.png");
                var alphaOut = Path.Combine(workDir, $"alpha_{p}.png");
                Run(exePath, modelsDir, curColor, colorOut, scales[p]);
                Run(exePath, modelsDir, curAlpha, alphaOut, scales[p]);
                curColor = colorOut;
                curAlpha = alphaOut;
            }

            var colorDec = PngCodec.Decode(File.ReadAllBytes(curColor));
            var alphaDec = PngCodec.Decode(File.ReadAllBytes(curAlpha));
            if (colorDec is null || alphaDec is null)
                throw new InvalidOperationException("Real-ESRGAN produced an unreadable image.");

            int ow = colorDec.Value.Width, oh = colorDec.Value.Height;
            var cb = colorDec.Value.Bgra;
            var ab = alphaDec.Value.Bgra;
            var result = new byte[ow * oh * 4];
            for (int i = 0; i < ow * oh; i++)
            {
                result[i * 4] = cb[i * 4];
                result[i * 4 + 1] = cb[i * 4 + 1];
                result[i * 4 + 2] = cb[i * 4 + 2];
                result[i * 4 + 3] = ab[i * 4 + 2]; // red channel of the grayscale alpha image
            }
            return new UpscaledTexture { Bgra = result, Width = ow, Height = oh };
        }
        finally
        {
            try { Directory.Delete(workDir, true); } catch { /* best effort cleanup */ }
        }
    }

    private static void Run(string exePath, string modelsDir, string input, string output, int scale)
    {
        var model = scale is 2 or 3 ? "realesr-animevideov3" : "realesrgan-x4plus-anime";
        var psi = new ProcessStartInfo(exePath)
        {
            Arguments = $"-i \"{input}\" -o \"{output}\" -n {model} -s {scale} -m \"{modelsDir}\"",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Could not start Real-ESRGAN.");
        proc.StandardOutput.ReadToEnd();
        proc.StandardError.ReadToEnd();
        proc.WaitForExit();

        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"Real-ESRGAN failed (exit code {proc.ExitCode}).");
        if (!File.Exists(output))
            throw new InvalidOperationException("Real-ESRGAN produced no output file.");
    }
}
