using System.Text;
using S4League.Resource;
using S4League.Resource.Internal;


// List entries: `SelfTest list <resource.s4hd|dir> <substr>`
if (args.Length >= 3 && args[0] == "list")
{
    var zp = Directory.Exists(args[1]) ? Path.Combine(args[1], "resource.s4hd") : args[1];
    foreach (var e in S4Zip.OpenZip(zp).Values
                 .Where(e => e.FullName.Contains(args[2], StringComparison.OrdinalIgnoreCase))
                 .OrderBy(e => e.FullName).Take(40))
        Console.WriteLine($"  {e.FullName}  (len={e.Length})");
    return 0;
}

// Server-data extraction: `SelfTest extract <resource.s4hd|dir> <outDir>`
// Writes the decoded XML/x7 files a NetspherePirates server expects under <outDir>.
if (args.Length >= 3 && args[0] == "extract")
{
    var zpath = Directory.Exists(args[1]) ? Path.Combine(args[1], "resource.s4hd") : args[1];
    var outDir = args[2];
    var zip = S4Zip.OpenZip(zpath);

    string[] wanted =
    {
        "xml/constant_info.x7", "xml/action.x7", "xml/_eu_weapon.x7", "xml/effect_list.x7",
        "xml/effect_match_list.x7", "xml/enchant_data.x7", "xml/equip_limit.x7",
        "xml/monster_status.x7", "xml/monster_wave/monster_map_middle.x7", "xml/experience.x7",
        "xml/map.x7", "xml/item.x7", "xml/default_item.x7",
        "language/xml/gameinfo_string_table.x7", "language/xml/item_effect_string_table.x7",
        "language/xml/iteminfo_string_table.x7",
    };

    int ok = 0, miss = 0;
    foreach (var rel in wanted)
    {
        var entry = zip.Values.FirstOrDefault(e =>
            e.FullName.EndsWith(rel, StringComparison.OrdinalIgnoreCase));
        if (entry is null) { Console.WriteLine($"  MISSING: {rel}"); miss++; continue; }
        var dest = Path.Combine(outDir, rel.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        File.WriteAllBytes(dest, entry.GetData());
        Console.WriteLine($"  {rel}  <-  {entry.FullName}  ({new FileInfo(dest).Length} bytes)");
        ok++;
    }
    Console.WriteLine($"extracted {ok}, missing {miss}");
    return miss == 0 ? 0 : 2;
}
// Diagnostic mode: `SelfTest <folder-or-.s4hd>` opens a real archive and reads real files.
if (args.Length > 0)
{
    var target = args[0];
    var zipPath = Directory.Exists(target) ? Path.Combine(target, "resource.s4hd") : target;
    var zip = S4Zip.OpenZip(zipPath);
    Console.WriteLine($"Opened '{zipPath}': {zip.Count} entries");

    string[] wanted = { ".dds", ".x7", ".png", ".tga", ".xml", ".lua" };
    foreach (var ext in wanted)
    {
        var e = zip.Values.FirstOrDefault(x => x.Length > 0 &&
            Path.GetExtension(x.Name).Equals(ext, StringComparison.OrdinalIgnoreCase));
        if (e is null) { Console.WriteLine($"  ({ext}: none)"); continue; }
        try
        {
            var data = e.GetData();
            var head = Convert.ToHexString(data.AsSpan(0, Math.Min(8, data.Length)));
            var ascii = System.Text.Encoding.ASCII.GetString(data, 0, Math.Min(16, data.Length)).Replace("\n", " ");
            Console.WriteLine($"  {e.FullName}  len={data.Length} (expected {e.Length})  head={head} '{ascii}'  {(data.Length == e.Length ? "OK" : "LEN-MISMATCH")}");
        }
        catch (Exception ex) { Console.WriteLine($"  {e.FullName}: READ FAILED {ex.Message}"); }
    }
    return 0;
}

int failures = 0;

void Check(string name, bool ok)
{
    Console.WriteLine($"[{(ok ? "PASS" : "FAIL")}] {name}");
    if (!ok) failures++;
}

static byte[] Rand(int n, int seed)
{
    var r = new Random(seed);
    var b = new byte[n];
    r.NextBytes(b);
    return b;
}

// 1. CRC32 known vector: "123456789" -> 0xCBF43926
Check("CRC32 vector", Crc32.Compute(Encoding.ASCII.GetBytes("123456789")) == 0xCBF43926);

// 2. LZO round-trip (random + highly compressible)
foreach (var (label, data) in new (string, byte[])[]
{
    ("lzo random", Rand(200_000, 1)),
    ("lzo repetitive", Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("The quick brown fox 12345. ", 5000)))),
})
{
    var comp = MiniLzo.Compress(data);
    var back = MiniLzo.Decompress(comp, data.Length);
    Check($"{label} ({data.Length}->{comp.Length})", back.AsSpan().SequenceEqual(data));
}

// 2b. LZO round-trip across realistic small file sizes (config/ini/xml sized payloads).
{
    bool sweepOk = true;
    foreach (var n in new[] { 21, 24, 29, 32, 40, 48, 64, 100, 128, 200, 512, 1024 })
    {
        Console.Out.Flush();
        var data = Rand(n, 1000 + n);
        var back = MiniLzo.Decompress(MiniLzo.Compress(data), n);
        if (!back.AsSpan().SequenceEqual(data)) { sweepOk = false; Console.WriteLine($"   size {n} failed"); }
    }
    Check("lzo small-size sweep (21..1024)", sweepOk);
}

// 3. S4Crypt round-trips
{
    var d = Rand(300, 2);
    var e = (byte[])d.Clone();
    S4Crypt.Default.Encrypt(e);       // v2, full length
    S4Crypt.Default.Decrypt(e);
    Check("S4Crypt v2 roundtrip", e.AsSpan().SequenceEqual(d));

    e = (byte[])d.Clone();
    S4Crypt.Old40.Encrypt(e);         // v1, full length
    S4Crypt.Old40.Decrypt(e);
    Check("S4Crypt v1 roundtrip", e.AsSpan().SequenceEqual(d));

    // OldCapped32 only transforms first 256 bytes; verify identity + cap boundary
    e = (byte[])d.Clone();
    S4Crypt.OldCapped32.Encrypt(e);
    var tailUntouched = e.AsSpan(256).SequenceEqual(d.AsSpan(256));
    S4Crypt.OldCapped32.Decrypt(e);
    Check("S4Crypt capped roundtrip + boundary", e.AsSpan().SequenceEqual(d) && tailUntouched);
}

// 4. SEED round-trip
{
    var d = Rand(1000, 3);
    var back = d.EncryptSeed().DecryptSeed();
    Check("SEED roundtrip", back.AsSpan().SequenceEqual(d));
}

// 5. X7 round-trip
{
    var d = Rand(5000, 4);
    var back = d.EncryptX7().DecryptX7();
    Check("X7 roundtrip", back.AsSpan().SequenceEqual(d));
}

// 6. Full S4Zip container round-trip (create -> save -> reopen -> read)
{
    var dir = Path.Combine(Path.GetTempPath(), "s4zip_selftest_" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(Path.Combine(dir, "_resources"));
    var zipPath = Path.Combine(dir, "resource.s4hd");

    try
    {
        var payloads = new Dictionary<string, byte[]>
        {
            ["gui/texture/logo.dds"] = Rand(4096, 10),
            ["script/ai/bot.lua"] = Encoding.ASCII.GetBytes("function think() return 1 end\n"),
            ["data/config.x7"] = Encoding.ASCII.GetBytes("<root><value>42</value></root>"),
            ["readme.txt"] = Encoding.UTF8.GetBytes("hello s4 league resource tool"),
            ["big/blob.bin"] = Rand(2_000_000, 11), // above compression threshold
        };

        // Start with an empty valid container, then add entries.
        var zip = CreateEmpty(zipPath);
        foreach (var (name, bytes) in payloads)
            zip.CreateEntry(name, bytes);
        zip.Save();

        // Reopen and verify.
        var reopened = S4Zip.OpenZip(zipPath);
        bool countOk = reopened.Count == payloads.Count;
        Check("S4Zip entry count", countOk);

        bool allMatch = true;
        foreach (var (name, bytes) in payloads)
        {
            var entry = reopened[name.ToLowerInvariant()];
            if (entry is null) { allMatch = false; Console.WriteLine($"   missing: {name}"); continue; }
            var got = entry.GetData();
            if (!got.AsSpan().SequenceEqual(bytes))
            {
                allMatch = false;
                Console.WriteLine($"   payload mismatch: {name} (got {got.Length}, want {bytes.Length})");
            }
        }
        Check("S4Zip payload round-trip (dds/lua/x7/txt/big)", allMatch);
    }
    finally
    {
        try { Directory.Delete(dir, true); } catch { /* best effort */ }
    }
}

// 7. S4Max-style container variant: no outer SwapBlocks + non-1 version magic.
{
    var dir = Path.Combine(Path.GetTempPath(), "s4zip_variant_" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(Path.Combine(dir, "_resources"));
    var zipPath = Path.Combine(dir, "resource.s4hd");
    try
    {
        // Build a canonical archive first (this also writes the payload files).
        var zip = CreateEmpty(zipPath);
        zip.CreateEntry("gui/logo.dds", Rand(2048, 20));
        zip.CreateEntry("cfg/x.ini", Encoding.UTF8.GetBytes("a=1\n"));
        zip.Save();

        // Re-wrap the index the S4Max way: decrypt canonically, stamp a non-1 version magic,
        // and re-encrypt WITHOUT the outer block swap.
        var plain = S4CryptoUtilities.DecryptSeed(File.ReadAllBytes(zipPath), swapOuterBlocks: true);
        BitConverter.GetBytes(0x2A2FABCF).CopyTo(plain, 0);
        File.WriteAllBytes(zipPath, S4CryptoUtilities.EncryptSeed(plain, swapOuterBlocks: false));

        // OpenZip must auto-detect the variant and read payloads correctly.
        var reopened = S4Zip.OpenZip(zipPath);
        var ok = reopened.Count == 2
                 && reopened["gui/logo.dds"]!.GetData().Length == 2048
                 && Encoding.UTF8.GetString(reopened["cfg/x.ini"]!.GetData()) == "a=1\n";
        Check("S4Zip S4Max variant (no-swap + version magic) auto-detect", ok);

        // Saving must preserve the detected variant so the file remains readable.
        reopened.Save();
        var again = S4Zip.OpenZip(zipPath);
        Check("S4Zip variant preserved on save", again.Count == 2 && again["gui/logo.dds"]!.GetData().Length == 2048);
    }
    finally { try { Directory.Delete(dir, true); } catch { } }
}

// 7. SCN scene round-trip: build a synthetic model chunk, serialize, re-read, compare.
{
    var container = new S4League.Scn.SceneContainer { Header = { Name = "TestMap", SubName = "" } };
    var model = new S4League.Scn.ModelChunk(container) { Name = "mesh_01", SubName = "" };
    model.Mesh.Vertices.Add(new System.Numerics.Vector3(0, 0, 0));
    model.Mesh.Vertices.Add(new System.Numerics.Vector3(1, 0, 0));
    model.Mesh.Vertices.Add(new System.Numerics.Vector3(0, 1, 0));
    model.Mesh.Vertices.Add(new System.Numerics.Vector3(0, 0, 1));
    model.Mesh.Faces.Add(new S4League.Scn.Vector3Int(0, 1, 2));
    model.Mesh.Faces.Add(new S4League.Scn.Vector3Int(0, 2, 3));
    model.Mesh.Normals.Add(new System.Numerics.Vector3(0, 0, 1));
    model.Mesh.Normals.Add(new System.Numerics.Vector3(0, 0, 1));
    model.Mesh.Normals.Add(new System.Numerics.Vector3(0, 0, 1));
    model.Mesh.Normals.Add(new System.Numerics.Vector3(0, 0, 1));
    model.Mesh.UV.Add(new System.Numerics.Vector2(0, 0));
    model.Mesh.UV.Add(new System.Numerics.Vector2(1, 0));
    model.Mesh.UV.Add(new System.Numerics.Vector2(1, 1));
    model.TextureData.ExtraUV = 0;
    container.Add(model);

    byte[] bytes;
    using (var ms = new MemoryStream())
    {
        container.Write(ms);
        bytes = ms.ToArray();
    }

    var reread = S4League.Scn.SceneContainer.ReadFrom(bytes);
    bool scnOk = reread.Models.Count == 1
                 && reread.Models[0].Mesh.Vertices.Count == 4
                 && reread.Models[0].Mesh.Faces.Count == 2
                 && reread.Models[0].Name == "mesh_01";
    Check("SCN scene round-trip (header + model chunk)", scnOk);

    var built = S4League.Scn.SceneMeshBuilder.Build(reread);
    Check("SCN mesh extraction", built.Count == 1 && built[0].Vertices.Length == 4 && built[0].Indices.Length == 6);
}

Console.WriteLine();
Console.WriteLine(failures == 0 ? "ALL TESTS PASSED" : $"{failures} TEST(S) FAILED");
return failures == 0 ? 0 : 1;

// Creates a minimal valid, empty S4Zip on disk and opens it.
static S4Zip CreateEmpty(string zipPath)
{
    // Header: int32(1) + int32(0 entries), then SEED-encrypted (matches S4Zip.Save).
    using (var ms = new MemoryStream())
    using (var w = new BinaryWriter(ms))
    {
        w.Write(1);
        w.Write(0);
        File.WriteAllBytes(zipPath, S4CryptoUtilities.EncryptSeed(ms.ToArray()));
    }
    return S4Zip.OpenZip(zipPath);
}
