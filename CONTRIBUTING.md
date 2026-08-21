# OpenS4L — Credits & Attribution

Everything in this repository owes a debt to the people and projects below. If you use,
fork, or redistribute any part of OpenS4L, please keep these credits intact. The MIT
license (see `LICENSE`) already requires preserving the copyright notice — this file is
the full accounting of *who* is owed credit and *why*.

> **Please read the "Attribution & legal notes" section** — a few vendored pieces carry
> their own license terms (GPLv2, or upstream code whose source was never published) that
> affect what you may do with the binaries you build.

---

## Upstream project heritage

OpenS4L is a **modern rebuild** of the abandoned **NetspherePirates** S4 League server
emulator, and a large part of it is derived from, or a direct port of, that project's code.

| Project | Author / maintainer | What we took | Where it lives | Links |
|---|---|---|---|---|
| **NetspherePirates** | wtfblub | The original S4 League server emulator this whole project is rebuilt from. Namespaces & project names were renamed `Netsphere.*` → `OpenS4L.*`. | Throughout `Server/opens4l/` | https://github.com/wtfblub/NetspherePirates |
| **BlubLib** (+ `BlubLib.Serialization`, `BlubLib.DotNetty`) | wtfblub | A full port of the BlubLib utility/serialization/transport libraries. Because BlubLib's **source was never published**, it was recovered by **decompiling** the published DLLs and re-homed into an in-repo project. | `Server/opens4l/src/OpenS4L.Blub/` | https://gitlab.com/wtfblub/BlubLib |
| **ProudNet** | Nettention | The proprietary networking middleware the S4 League client speaks. `src/ProudNet/` is a **protocol reimplementation** (kept byte-for-byte compatible; version GUID `{beb92241-8333-4117-ab92-9b4af78c688f}`, client `0.8.32.26995`). | `Server/opens4l/src/ProudNet/` | https://www.proudnet.com/ |

## Vendored / reimplemented source

| Piece | Origin | License | Where it lives |
|---|---|---|---|
| **MiniLZO** (C# port) | C# port by **Frank Razenberg** (zzattack) of **LZO** by **Markus F.X.J. Oberhumer** | **GPL v2+** | `Tools/s4l-resource-tool/src/S4League.Resource/MiniLzo.cs` |
| **S4Zip format & crypto** (`S4Zip`, `S4ZipEntry`, `S4Crypt`, `S4CryptoUtilities`, `Crc32`, `BinaryExtensions`) | Re-implemented, **derived from NetspherePirates** (`Netsphere.Resource`) and **BlubLib** | follows upstream (see above) | `Tools/s4l-resource-tool/src/S4League.Resource/` |

## S4 League game content

S4 League and all game assets / client data are the property of **Nexon** (and/or its
licensors). OpenS4L only *emulates* the server protocol; it does **not** redistribute the
game client or its content data, which you must provide yourself.

## NuGet dependencies — server

Runtime packages referenced by `Server/opens4l/`. Licenses are the package authors' own;
the notable ones are listed.

| Package | License |
|---|---|
| Microsoft.EntityFrameworkCore (+Design) | MIT |
| Npgsql.EntityFrameworkCore.PostgreSQL | PostgreSQL License |
| Microsoft.Extensions.* (Configuration, DependencyInjection, Hosting, Options, Logging, …) | MIT |
| Newtonsoft.Json | MIT |
| Serilog (+ Sinks.Console, Sinks.File) | Apache-2.0 |
| Foundatio / Foundatio.JsonNet / Foundatio.Redis | Apache-2.0 |
| Polly | BSD-3-Clause |
| Hjson | MIT |
| IdGen | MIT |
| Stateless | Apache-2.0 |
| HolopaMir.ExpressMapper | MIT |
| Portable.BouncyCastle | MIT-style (Legion of the Bouncy Castle) |
| DotNetty (Buffers, Codecs, Common, Handlers, Transport) | MIT |
| Sigil | MIT |
| SharpLzo | see package |
| Iconic.Zlib.Netstandard | see package |
| System.Numerics.Vectors / System.Runtime.Loader / System.Text.Encoding.CodePages | MIT |
| Microsoft.Composition | MIT |

## NuGet dependencies — resource tool & libraries

Referenced by `Tools/s4l-resource-tool/`. (An older copy of this accounting also lives in
`Tools/s4l-resource-tool/THIRD_PARTY_NOTICES.md`.)

| Package | License |
|---|---|
| Avalonia (+ Desktop, Themes.Fluent, Fonts.Inter, Controls.DataGrid, Diagnostics) | MIT |
| CommunityToolkit.Mvvm | MIT |
| BouncyCastle.Cryptography | MIT-style (Legion of the Bouncy Castle) |
| Pfim | MIT |
| Tmds.DBus.Protocol | MIT |

## NuGet dependencies — plugins

| Package | License |
|---|---|
| EmbedIO | MIT |

---

## Attribution & legal notes

1. **The MIT license applies only to code authored in this repository.** It cannot and does
   not relicense upstream code you don't own.
2. **GPLv2 (MiniLZO).** `MiniLzo.cs` is GPL v2+. Because it is compiled into the resource
   tool, **distributed binaries of `s4l-resource-tool` are subject to the GPLv2.** The
   MIT-licensed OpenS4L code and the GPL LZO component are kept in separate files; if you
   redistribute resource-tool binaries you are responsible for meeting the GPL's terms.
3. **BlubLib (decompiled, never-published source).** `OpenS4L.Blub` is a port of
   copyrighted upstream code that was recovered by decompilation. Its author never released
   the source or a license. Treat it as upstream's copyrighted material, used for this
   self-hosted fork.
4. **NetspherePirates / ProudNet.** The upstream emulator and the ProudNet protocol are not
   explicitly licensed; OpenS4L's reimplementation is provided for the self-hosted
   community. If you are unsure about redistributing it publicly, review the upstream
   projects first.
5. **Nexon / S4 League.** This project is an unofficial, non-commercial emulator. It is not
   affiliated with, endorsed by, or connected to Nexon or the S4 League developers.

---

## How to contribute

This file doubles as the project's contribution guide.

- **Bugs & ideas:** open an issue describing the problem, expected behaviour, and steps to
  reproduce.
- **Pull requests:** keep changes focused; match the existing `.NET 10` / Avalonia style;
  update the relevant `README` / checklists when you change behaviour. For the server,
  verify with `make build` (from `Server/`) or `dotnet build` on the solution.
- **New third-party code:** if a PR introduces (or copies) third-party source or a new
  package, add it to the credit tables above and note its license. Code under a
  copy-left license (GPL/LGPL/AGPL) needs a deliberate decision before it's merged.
- **Code of conduct:** be constructive and respectful. This is a hobby/community project.
