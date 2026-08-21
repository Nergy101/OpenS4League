# Third-party notices

This project bundles or depends on the following components.

## Vendored source

- **MiniLZO (C# port)** — `src/S4League.Resource/MiniLzo.cs`
  - C# port by Frank Razenberg: https://github.com/zzattack/MiniLZO
  - Original LZO by Markus F.X.J. Oberhumer: http://www.oberhumer.com/opensource/lzo/
  - License: **GNU General Public License v2.0 (or later)**.
  - Because this GPL component is compiled into the application, distributed **binaries** of the
    S4 League Resource Tool are subject to the GPLv2.

- **S4Zip format / crypto** — re-implemented in `src/S4League.Resource` (`S4Zip`, `S4ZipEntry`,
  `S4Crypt`, `S4CryptoUtilities`, `Crc32`, `BinaryExtensions`), derived from wtfblub's
  NetspherePirates (`Netsphere.Resource`) and BlubLib.
  - https://github.com/wtfblub/NetspherePirates — https://gitlab.com/wtfblub/BlubLib

## NuGet dependencies

- **Avalonia** and related packages — MIT License — https://github.com/AvaloniaUI/Avalonia
- **CommunityToolkit.Mvvm** — MIT License — https://github.com/CommunityToolkit/dotnet
- **BouncyCastle.Cryptography** — MIT-style License — https://www.bouncycastle.org/
- **Pfim** — MIT License — https://github.com/nickbabcock/Pfim
