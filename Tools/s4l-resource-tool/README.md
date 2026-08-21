# S4 League Resource Tool

A cross-platform rebuild of the S4 League `resource.s4hd` resource manager, targeting
**.NET 10** with an **[Avalonia](https://avaloniaui.net/)** desktop UI that runs natively on
**macOS**, Windows and Linux.

The original tool was Windows-only (WinForms + WPF + Win32 shell/`uxtheme` P/Invoke) and depended
on prebuilt `NeoNetsphere.Resource` / `BlubLib` / `SharpLzo` DLLs. This version re-implements the
full S4 League container format from scratch in portable, managed .NET so it builds and runs
anywhere with the .NET SDK — no Windows, no native DLLs.

## Features

- Browse the resource tree of a S4 League client (`resource.s4hd` + `_resources`).
- Live preview of text (`.txt/.xml/.ini/.x7/.lua/...`) and images (`.dds/.tga/.png/.jpg/.bmp/...`),
  including DXT/BC-compressed DDS decoded via [Pfim](https://github.com/nickbabcock/Pfim).
- **Interactive 3D preview of `.scn` scene files** — the mesh is parsed, textured (DDS/TGA/PNG
  resolved from the archive) and rendered in-engine: drag to orbit, scroll to zoom, and pick which
  texture to overlay on the model from a combo box.
- **Texture preview upscaling** at 2×/4×/8×, in two flavours:
  - In-process, GPU-free Lanczos-3 upscale (alpha-aware, works everywhere).
  - **Real-ESRGAN (ncnn-vulkan) AI upscale** when `realesrgan-ncnn-vulkan.exe` is available —
    colour and alpha are upscaled separately so transparency stays clean.
  - **Export upscaled** previews to PNG or BC7-compressed DDS (via `texconv`).
- **Scan for `resource.s4hd`…** — locate every archive under a chosen folder and pick the one to
  open (shows the full path), rather than auto-selecting the first found archive.
- Search across all resources.
- **Open** a resource in its default application; edits made externally can be re-imported.
- **Open in Unity** — push a `.scn` into a configured Unity project for editing
  (needs `UnityExecutablePath` / `UnityScnProjectPath`).
- **Export** single files or whole selections/folders (with progress).
- **Replace** a resource from a file on disk.
- **Delete** resources; changes are applied to the archive on **Save**.
- **Add** resources by drag & drop (files or whole folders).
- **Find unused** payload files in `_resources` and reclaim disk space.

> Not compatible with Valofe's official S4 League client (same as the original tool).

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or newer.
- macOS, Windows or Linux.
- Real-ESRGAN AI upscaling additionally needs a Vulkan-capable GPU and the bundled
  `realesrgan-ncnn-vulkan.exe` binaries; BC7 DDS export needs `texconv.exe` (DirectXTex).

## Build & run

```bash
# from the repository root
dotnet run --project src/S4LResourceTool.App
# or, if you have make:
make run
```

Then click **Open S4 League folder…** and select the client directory (the one containing
`resource.s4hd`), a parent/subfolder of it, or the `resource.s4hd` file itself. Use **Scan for
resource.s4hd…** to list every archive under a folder and pick from the list. The chosen location
is remembered between launches.

### Supported clients

Reads the standard S4 League container (patch-34 / Season 1 era) **and** the newer
NeoNetsphere-era format used by Season 7+ clients such as **S4Max 4.5.0.x**, whose container
omits the outer block-transpose and uses a non-`1` version magic. The correct variant is
auto-detected on open and preserved on save. Verified against a real S4Max 4.5.0.1 client
(21,905 resources: DDS/TGA/PNG textures, XML, `.x7`, and Lua all decode correctly).

### Publish a self-contained app

```bash
# macOS (Apple Silicon)
dotnet publish src/S4LResourceTool.App -c Release -r osx-arm64 --self-contained
# macOS (Intel):   -r osx-x64
# Windows:         -r win-x64
# Linux:           -r linux-x64
```

## Tests

```bash
# crypto / container round-trip (creates a real resource.s4hd on disk and reads it back)
dotnet run -c Release --project tests/S4League.Resource.SelfTest

# headless Avalonia UI smoke tests (tree, list, text + DDS image preview, search, window binding)
dotnet test tests/S4LResourceTool.App.HeadlessTests
```

## Project layout

| Project | Description |
|---|---|
| `src/S4League.Resource` | Portable class library implementing the S4 League container format: `S4Zip`, `S4ZipEntry`, the S4 block cipher, SEED (CTR) wrapping, X7 obfuscation, CRC-32, and a vendored LZO1X codec. No UI, no platform dependencies. |
| `src/S4League.Scn` | Portable library that parses S4 `.scn` scene files into a `SceneContainer` (boxes, models, shapes, bones, animations, sky) and builds world-space meshes from them. Used by the in-app 3D preview. |
| `src/S4LResourceTool.App` | Avalonia (MVVM) desktop application — browsing, editing, preview (text/image/scene), and texture upscaling/export. |
| `extract-tool/` | Small standalone CLI that lists/extracts entries from a `resource.s4hd` without the GUI. |
| `tests/S4League.Resource.SelfTest` | Console harness that round-trips every crypto/format layer end-to-end. |
| `tests/S4LResourceTool.App.HeadlessTests` | Avalonia headless UI tests. |

`extract-tool` is a separate project (not in the solution). Build/run it directly:

```bash
dotnet run --project extract-tool -- <resource.s4hd> <match> <outfile|->  # "-" lists matches
```

## How the format works (short version)

`resource.s4hd` is a SEED-encrypted index. Each index record (name, checksum, length) is
additionally scrambled with the S4 block cipher. The actual payloads are **not** in the container —
they live as individual files inside the sibling `_resources/` directory, each named after its
hex checksum, and are stored LZO1X-compressed + block-ciphered (with an extra SEED/X7 layer for
`.lua`/`.x7`). See `src/S4League.Resource` for the full, commented implementation.

One decode quirk worth knowing: S4 `.dds` textures are stored **bottom-up**, so the preview and
the `.scn` renderer flip them vertically on decode (TGA/PNG are top-down and left as-is).

## Credits & references

- Format & crypto ported from **wtfblub**'s
  [NetspherePirates `S4Zip.cs`](https://github.com/wtfblub/NetspherePirates/blob/dev/src/Netsphere.Resource/S4Zip.cs)
  and BlubLib helpers.
- LZO1X codec: **zzattack**'s [MiniLZO](https://github.com/zzattack/MiniLZO) C# port of
  Markus Oberhumer's [LZO](http://www.oberhumer.com/opensource/lzo/).
- Original S4LResourceTool by **Dekirai** (Devyre).

## License

See [`LICENSE`](LICENSE) and [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
Note: the bundled MiniLZO port is GPLv2, so binary distributions of this tool are effectively
GPLv2. The newly written code in this repository is otherwise available under the MIT License.
