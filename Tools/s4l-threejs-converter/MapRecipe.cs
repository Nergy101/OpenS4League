using System.Text.Json;

// One S4 map: what it is called, the base name of the bundle files it generates, and
// the map configuration the client ships for it. Everything else (scenes, textures,
// dependencies) is read from that configuration inside the client ZIP.
sealed class MapRecipe
{
    public string name { get; set; } = "";
    public string bundle { get; set; } = "";
    public string config { get; set; } = "";

    public static MapRecipe Load(string recipe)
    {
        var map = JsonSerializer.Deserialize<MapRecipe>(File.ReadAllText(recipe))
            ?? throw new InvalidDataException($"Invalid map recipe: {recipe}");
        if (map.name.Length == 0 || map.bundle.Length == 0 || map.config.Length == 0)
            throw new InvalidDataException($"Map recipe needs name, bundle and config: {recipe}");
        return map;
    }
}
