/**
 * The converted-map index is the single place that names a map's bundle files.
 * The preview server has no directory listing, so the browser cannot discover
 * them; the converter writes this file next to the maps it converts.
 */
export const MAP_INDEX_URL = './Models/Maps/index.json';
export const MAP_INDEX_FORMAT = 's4-maps-index';

export async function loadMapIndex(url = MAP_INDEX_URL) {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`Map index: HTTP ${response.status}. Convert a map first (make threejs convert-assets).`);
  const index = await response.json();
  if (index.format !== MAP_INDEX_FORMAT || index.version !== 1) throw new Error('Unsupported map index');
  return index;
}

/** The map the viewer opens when the URL requests none: Station-2, the preview's reference map. */
export const DEFAULT_MAP_ID = 'station-2';

/** Resolves a map id (or directory name, case-insensitively). No id means the default map. */
export function resolveMapEntry(index, id) {
  const maps = index?.maps ?? [];
  if (!maps.length) throw new Error('No converted maps. Run: make threejs convert-assets');
  if (!id) return maps.find(map => map.id.toLowerCase() === DEFAULT_MAP_ID) ?? maps[0];
  const wanted = id.toLowerCase();
  const entry = maps.find(map => map.id.toLowerCase() === wanted || map.directory?.toLowerCase() === wanted);
  if (!entry) throw new Error(`Unknown map '${id}'. Available: ${maps.map(map => map.id).join(', ')}`);
  return entry;
}

export function mapAssetUrl(entry, file) {
  return `./Models/Maps/${entry.directory}/${file}`;
}

/** Display label for a map: the registry's name, falling back to its id. */
export function mapLabel(entry) {
  return entry.name || entry.id;
}

/** The map requested in the page URL, e.g. /?map=station-2. */
export function requestedMapId(search = location.search) {
  return new URLSearchParams(search).get('map') ?? '';
}
