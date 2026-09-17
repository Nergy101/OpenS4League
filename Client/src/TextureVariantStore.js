import * as THREE from 'three';

const QUALITIES = ['1x', '2x', '4x'];
const rank = quality => Math.max(0, QUALITIES.indexOf(quality));

/** Return the highest available variant no larger than the requested quality. */
export function selectTextureVariant(descriptor, requested = '1x', maxTextureSize = Infinity) {
  const variants = descriptor?.variants ?? (descriptor?.file ? { '1x': descriptor } : {});
  const requestedRank = rank(requested);
  for (let i = requestedRank; i >= 0; i--) {
    const candidate = variants[QUALITIES[i]];
    if (candidate && candidate.width <= maxTextureSize && candidate.height <= maxTextureSize) {
      return { ...candidate, quality: QUALITIES[i], kind: candidate.kind ?? descriptor.kind ?? 'color' };
    }
  }
  throw new Error(`No usable texture variant for ${descriptor?.source ?? 'unknown texture'} at ${requested}`);
}

/** Lazy, reference-counted cache for logical texture paths and quality variants. */
export class TextureVariantStore {
  constructor(manifest, baseUrl, loader = new THREE.TextureLoader(), options = {}) {
    this.manifest = manifest?.textures ?? manifest ?? {};
    this.baseUrl = new URL(baseUrl);
    this.loader = loader;
    this.maxTextureSize = options.maxTextureSize ?? Infinity;
    this.quality = options.quality ?? '1x';
    this.cache = new Map();
    this.pending = new Map();
    this.lastSelected = new Map();
  }

  descriptor(sourcePath) {
    const descriptor = this.manifest[sourcePath] ?? this.manifest[sourcePath.toLowerCase()];
    if (!descriptor) throw new Error(`Texture is not imported: ${sourcePath}`);
    return { ...descriptor, source: sourcePath };
  }

  resolve(sourcePath, requested = this.quality) {
    return selectTextureVariant(this.descriptor(sourcePath), requested, this.maxTextureSize);
  }

  async acquire(sourcePath, requested = this.quality) {
    const variant = this.resolve(sourcePath, requested);
    const key = `${sourcePath}\0${variant.quality}`;
    let entry = this.cache.get(key);
    if (!entry) {
      let promise = this.pending.get(key);
      if (!promise) {
        promise = this.loader.loadAsync(new URL(variant.file, this.baseUrl).href).then(texture => {
          texture.name = sourcePath;
          texture.userData = { ...texture.userData, textureSource: sourcePath, textureQuality: variant.quality, textureKind: variant.kind };
          texture.colorSpace = variant.kind === 'color' || variant.kind === 'alpha' ? THREE.SRGBColorSpace : THREE.NoColorSpace;
          texture.wrapS = texture.wrapT = variant.kind === 'lightmap' ? THREE.ClampToEdgeWrapping : THREE.RepeatWrapping;
          this.cache.set(key, entry = { texture, refs: 0, sourcePath, quality: variant.quality });
          return entry;
        }).finally(() => this.pending.delete(key));
        this.pending.set(key, promise);
      }
      entry = await promise;
    }
    entry.refs++;
    this.lastSelected.set(sourcePath, entry.quality);
    return { texture: entry.texture, sourcePath, requestedQuality: requested, quality: entry.quality, descriptor: variant };
  }

  release(sourcePath, quality = this.quality) {
    let variant;
    try { variant = this.resolve(sourcePath, quality); } catch { return; }
    const key = `${sourcePath}\0${variant.quality}`;
    const entry = this.cache.get(key);
    if (!entry) return;
    entry.refs = Math.max(0, entry.refs - 1);
    if (!entry.refs) { entry.texture.dispose(); this.cache.delete(key); }
  }

  setQuality(quality) {
    if (!QUALITIES.includes(quality)) throw new Error(`Unsupported texture quality: ${quality}`);
    this.quality = quality;
    return quality;
  }

  selectedQuality(sourcePath) { return this.lastSelected.get(sourcePath) ?? null; }
  status(sourcePath, requested = this.quality) { return { requested, loaded: this.selectedQuality(sourcePath) ?? this.resolve(sourcePath, requested).quality }; }

  dispose() {
    for (const entry of this.cache.values()) entry.texture.dispose();
    this.cache.clear(); this.pending.clear(); this.lastSelected.clear();
  }
}
