import * as THREE from 'three';
import { CharacterModel } from './CharacterModel.js';
import { validateOutfit } from './SavedOutfits.js';
import { TextureVariantStore } from './TextureVariantStore.js';

async function response(url) {
  const result = await fetch(url);
  if (!result.ok) throw new Error(`Asset HTTP ${result.status}: ${url.pathname}`);
  return result;
}

/** An indexed wardrobe: only the current rig, equipment, and skins stay resident. */
export class WardrobeLibrary {
  static async load(url, options = {}) {
    const base = new URL(url, window.location.href);
    return new WardrobeLibrary(await (await response(base)).json(), base, options);
  }

  constructor(index, base, adapters = {}) {
    if (index.format !== 's4-wardrobe' || index.version !== 1) throw new Error('Unsupported wardrobe index');
    this.index = index;
    this.base = base;
    const loader = new THREE.TextureLoader();
    this.adapters = {
      json: adapters.json ?? (async url => (await response(url)).json()),
      binary: adapters.binary ?? (async url => (await response(url)).arrayBuffer()),
      texture: adapters.texture ?? (url => loader.loadAsync(url.href)),
    };
    this.textureStore = new TextureVariantStore(index.textures, base, { loadAsync: url => this.adapters.texture(new URL(url)) }, { maxTextureSize: adapters.maxTextureSize });
    this.quality = '1x';
    this.runtimeManifest = { format: 's4-character-threejs', version: 1, catalog: index.catalog, scenes: [], textures: index.textures };
    this.sceneCache = new Map(); this.sceneBuffers = new Map(); this.textures = new Map(); this.textureEntries = new Map();
    this.pendingScenes = new Map(); this.pendingTextures = new Map();
    this.slotRequests = new Map(); this.bodyRequest = 0; this.activeLoads = 0;
  }

  async loadScene(path) {
    const key = path.toLowerCase();
    if (this.sceneCache.has(key)) return this.sceneCache.get(key);
    if (this.pendingScenes.has(key)) return this.pendingScenes.get(key);
    const descriptor = this.index.scenes[key];
    if (!descriptor) throw new Error(`Scene is not imported: ${path}`);
    const pending = Promise.all([
      this.adapters.json(new URL(descriptor.json, this.base)),
      this.adapters.binary(new URL(descriptor.bin, this.base)),
    ]).then(([scene, buffer]) => {
      if (scene.source.toLowerCase() !== key) throw new Error(`Scene index mismatch: ${path}`);
      this.sceneCache.set(key, scene); this.sceneBuffers.set(key, buffer);
      this.runtimeManifest.scenes = [...this.sceneCache.values()];
      this.model?.scenes.set(key, scene);
      return scene;
    }).finally(() => this.pendingScenes.delete(key));
    this.pendingScenes.set(key, pending);
    return pending;
  }

  async loadTexture(path) {
    if (this.textures.has(path)) return this.textures.get(path);
    if (this.pendingTextures.has(path)) return this.pendingTextures.get(path);
    const pending = this.textureStore.acquire(path, this.quality).then(async loaded => {
      // A load that started before a quality change must not leave the old level resident:
      // re-acquire the level the current request actually resolves to.
      let result = loaded;
      const expected = this.textureStore.resolve(path, this.quality);
      if (expected.quality !== result.quality) {
        this.textureStore.release(path, result.quality);
        result = await this.textureStore.acquire(path, this.quality);
      }
      this.textureEntries.set(path, result);
      this.textures.set(path, result.texture);
      return result.texture;
    }).finally(() => this.pendingTextures.delete(path));
    this.pendingTextures.set(path, pending);
    return pending;
  }

  async setQuality(quality) {
    this.textureStore.setQuality(quality);
    this.quality = quality;
    if (!this.model) return;
    const active = [...this.model.equipment.values()].flatMap(selection => [...this.texturePaths(selection.item, selection.variant)]);
    for (const path of new Set(active)) {
      if (this.textures.has(path)) {
        const old = this.textureEntries.get(path);
        this.textureStore.release(path, old?.requestedQuality ?? this.quality);
        this.textures.delete(path); this.textureEntries.delete(path);
      }
      await this.loadTexture(path);
    }
    for (const [slot, selection] of this.model.equipment) this.model.setItem(slot, selection.item.id, selection.variant.id);
    return this.reconcileQuality();
  }

  /**
   * Re-acquire every resident texture whose loaded level is not the one the current quality
   * resolves to, then rebind the equipment. A resident 1× texture while 4×/8× is requested means
   * the level was loaded before the request (a reload, an on-demand load, or a slow switch);
   * retrying is bounded so a genuinely unavailable higher level cannot loop forever.
   */
  async reconcileQuality(attempts = 2) {
    if (!this.model) return this.textureStatus();
    for (let attempt = 0; attempt < attempts; attempt++) {
      const mismatched = [];
      for (const [path, entry] of this.textureEntries) {
        let expected;
        try { expected = this.textureStore.resolve(path, this.quality); } catch { continue; }
        if (expected.quality !== entry.quality) mismatched.push(path);
      }
      if (!mismatched.length) break;
      for (const path of mismatched) {
        this.textureStore.release(path, this.textureEntries.get(path)?.quality ?? this.quality);
        this.textures.delete(path); this.textureEntries.delete(path);
        await this.loadTexture(path);
      }
      for (const [slot, selection] of this.model.equipment) this.model.setItem(slot, selection.item.id, selection.variant.id);
    }
    return this.textureStatus();
  }

  textureStatus() {
    return { requested: this.quality, loaded: [...this.textureEntries.values()].map(entry => entry.quality) };
  }

  texturePaths(item, variant) {
    const paths = new Set();
    for (const part of item.parts) {
      const scene = this.sceneCache.get(part.scene.toLowerCase());
      if (!scene) throw new Error(`Scene not loaded: ${part.scene}`);
      for (const node of scene.nodes) if (node.geometry) for (const group of node.geometry.groups) {
        for (const original of [group.map, group.lightMap]) if (original) paths.add(variant.maps[original] ?? original);
      }
    }
    return paths;
  }

  async prepareItem(body, slot, id, variantId = 'default') {
    if (!id && !(slot in body.defaults)) return;
    const item = body.items.find(item => item.id === id && item.slot === slot);
    if (!item) throw new Error(`Item ${id} is not imported for ${body.id}/${slot}`);
    const variant = item.variants.find(variant => variant.id === variantId);
    if (!variant) throw new Error(`Variant ${variantId} is not imported for ${id}`);
    await Promise.all(item.parts.map(part => this.loadScene(part.scene)));
    await Promise.all([...this.texturePaths(item, variant)].map(path => this.loadTexture(path)));
  }

  async prepareBody(id) {
    const body = this.index.catalog.bodies.find(body => body.id === id);
    if (!body) throw new Error(`Body is not imported: ${id}`);
    await Promise.all([
      this.loadScene(body.skeleton),
      ...Object.entries(body.defaults).map(([slot, item]) => this.prepareItem(body, slot, item)),
    ]);
    return body;
  }

  async createModel() {
    if (this.model) throw new Error('This wardrobe already owns a character model');
    this.activeLoads++;
    try {
      await this.prepareBody(this.index.catalog.defaultBody);
      this.model = new CharacterModel(this.runtimeManifest, null, this.textures, this.sceneBuffers);
      return this.model;
    } finally { this.activeLoads--; this.prune(); }
  }

  async equip(slot, id, variant = 'default') {
    const request = (this.slotRequests.get(slot) ?? 0) + 1;
    this.slotRequests.set(slot, request);
    const bodyRequest = this.bodyRequest;
    this.activeLoads++;
    try {
      await this.prepareItem(this.model.body, slot, id, variant);
      if (request !== this.slotRequests.get(slot) || bodyRequest !== this.bodyRequest) return false;
      this.model.setItem(slot, id, variant);
      return true;
    } finally { this.activeLoads--; this.prune(); }
  }

  async applyOutfit(outfit) {
    const body = validateOutfit(outfit, this.index.catalog);
    const request = ++this.bodyRequest;
    this.slotRequests.clear(); this.activeLoads++;
    try {
      await this.prepareBody(body.id);
      await Promise.all(Object.entries(outfit.equipment).map(([slot, selection]) =>
        this.prepareItem(body, slot, selection.itemId, selection.variantId)));
      if (request !== this.bodyRequest) return false;
      this.model.applyOutfit(outfit);
      return true;
    } finally { this.activeLoads--; this.prune(); }
  }

  async setBody(id) {
    const request = ++this.bodyRequest;
    this.slotRequests.clear(); this.activeLoads++;
    try {
      await this.prepareBody(id);
      if (request !== this.bodyRequest) return false;
      this.model.setBody(id);
      return true;
    } finally { this.activeLoads--; this.prune(); }
  }

  prune() {
    // Keep pending selections protected until all concurrent requests settle.
    if (this.activeLoads || !this.model) return;
    const scenes = new Set([this.model.body.skeleton.toLowerCase()]);
    const textures = new Set();
    for (const selection of this.model.equipment.values()) {
      for (const part of selection.item.parts) scenes.add(part.scene.toLowerCase());
      for (const path of this.texturePaths(selection.item, selection.variant)) textures.add(path);
    }
    for (const key of this.sceneCache.keys()) if (!scenes.has(key)) {
      this.sceneCache.delete(key); this.sceneBuffers.delete(key); this.model.scenes.delete(key);
    }
    for (const [path] of this.textures) if (!textures.has(path)) {
      const entry = this.textureEntries.get(path);
      this.textureStore.release(path, entry?.requestedQuality ?? this.quality);
      this.textureEntries.delete(path); this.textures.delete(path);
    }
    this.runtimeManifest.scenes = [...this.sceneCache.values()];
  }

  dispose() {
    this.model?.dispose();
    this.textureStore.dispose();
    this.textureEntries.clear(); this.textures.clear(); this.sceneCache.clear(); this.sceneBuffers.clear();
  }
}
