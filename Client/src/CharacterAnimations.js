import * as THREE from 'three';

async function json(url) {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`Animation asset: HTTP ${response.status} (${url.pathname})`);
  return response.json();
}

export class CharacterAnimations {
  static async load(url) {
    const base = new URL(url, window.location.href);
    return new CharacterAnimations(await json(base), base);
  }

  constructor(index, base, fetchJson = json) {
    if (index.format !== 's4-character-animations' || index.version !== 1 || !Array.isArray(index.clips))
      throw new Error('Unsupported animation pack');
    this.bodyId = index.bodyId;
    this.clips = index.clips.map(clip => ({ ...clip, url: new URL(clip.url, base).href }));
    if (new Set(this.clips.map(clip => clip.id)).size !== this.clips.length) throw new Error('Duplicate animation IDs');
    this.index = index; this.base = base; this.fetchJson = fetchJson; this.cache = new Map();
  }

  async get(id) {
    const descriptor = this.clips.find(clip => clip.id === id);
    if (!descriptor) throw new Error(`Animation is not imported: ${id}`);
    if (!this.cache.has(id)) {
      const request = this.fetchJson(new URL(descriptor.url)).then(data => {
        const clip = THREE.AnimationClip.parse(data);
        if (!Number.isFinite(clip.duration) || clip.duration < 0 || !clip.tracks.length)
          throw new Error(`Invalid animation clip: ${id}`);
        for (const track of clip.tracks) if (!track.validate()) throw new Error(`Invalid animation track: ${id}/${track.name}`);
        return clip;
      }).catch(error => { this.cache.delete(id); throw error; });
      this.cache.set(id, request);
    }
    return this.cache.get(id);
  }
}
