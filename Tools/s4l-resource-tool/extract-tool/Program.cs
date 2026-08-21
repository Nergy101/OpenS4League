using S4League.Resource;

// usage: extract-tool <resource.s4hd> <match> <outfile>
//   lists entries matching <match>, and if <outfile> given, extracts the first exact-match
//   entry whose FullName equals <match> (or, if <outfile> is "-list", just lists).
var s4hd = args[0];
var match = args[1].ToLowerInvariant();
var outFile = args.Length > 2 ? args[2] : "-list";

var zip = S4Zip.OpenZip(s4hd);
Console.WriteLine($"opened {s4hd}: {zip.Count} entries");

if (outFile == "-list")
{
    foreach (var e in zip.Values)
        if (e.FullName.Contains(match))
            Console.WriteLine("  " + e.FullName + "  (" + e.Length + " bytes)");
    return;
}

var target = match;
S4ZipEntry entry = null;
if (zip.TryGetValue(target, out entry)) { /* exact */ }
else
{
    // fall back to first entry containing match
    foreach (var e in zip.Values)
        if (e.FullName.Contains(match)) { entry = e; break; }
}
if (entry == null) { Console.Error.WriteLine("no entry found for " + match); return; }

var data = entry.GetData();
File.WriteAllBytes(outFile, data);
Console.WriteLine($"extracted {entry.FullName} -> {outFile} ({data.Length} bytes)");
