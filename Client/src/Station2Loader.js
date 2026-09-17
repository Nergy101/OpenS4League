import * as THREE from 'three';
import { loadScnAssets, scnGeometry, scnMaterials } from './ScnAssets.js';

export async function loadStation2(url = './Models/Maps/Station-2/station2.json') {
  const { manifest, buffer, textures } = await loadScnAssets(url, 's4-station2-threejs');
  return { ...buildStation2(manifest, buffer, textures), manifest };
}

export function buildStation2(manifest, buffer, textures) {
  if (manifest.format !== 's4-station2-threejs' || manifest.version !== 1)
    throw new Error('Unsupported Station-2 bundle');
  const root = new THREE.Group();
  root.name = 'Station-2';
  root.scale.z = -1; // One explicit handedness conversion; original units unchanged.
  const helpers = [], skies = [];

  for (const source of manifest.scenes) {
    const group = new THREE.Group();
    group.name = source.name;
    group.matrix.fromArray(source.matrix);
    group.matrixAutoUpdate = false;
    group.userData.role = source.role;
    root.add(group);
    if (source.role === 'sky') skies.push(group);
    const nodes = source.nodes.map(data => {
      let node;
      if (data.geometry) {
        const g = data.geometry;
        const geometry = scnGeometry(g, buffer);
        node = new THREE.Mesh(geometry, scnMaterials(data, textures));
        // Collision meshes include textured duplicates of real surfaces. Keep
        // their complete geometry available, but never draw it over the visual mesh.
        const helper = /_occlusion|fullscenerendertarget/i.test(data.name)
          || /^oct_/i.test(data.name)
          || !g.groups.some(range => range.map);
        if (helper) { node.visible = false; helpers.push(node); }
        // Preview convention: show the allied sector variant. Ownership requires
        // game state; drawing its red and blue alternatives together makes purple.
        if (/sector\d+_enemy_/i.test(data.name)) node.visible = false;
        if (source.role === 'sky') {
          node.renderOrder = -1000;
          node.frustumCulled = false;
        }
      } else {
        node = new THREE.Group();
      }
      node.name = data.name;
      node.matrix.fromArray(data.matrix);
      node.matrixAutoUpdate = false;
      node.userData = { source: source.name, type: data.type, parentName: data.parent, ...data.details };
      return node;
    });
    const byName = new Map();
    nodes.forEach(node => { if (!byName.has(node.name.toLowerCase())) byName.set(node.name.toLowerCase(), node); });
    nodes.forEach((node, i) => {
      const parentName = source.nodes[i].parent.toLowerCase();
      const parent = parentName && parentName !== source.header.toLowerCase() ? byName.get(parentName) : null;
      if (parentName && parentName !== source.header.toLowerCase() && !parent)
        throw new Error(`Missing parent ${parentName} in ${source.name}`);
      (parent ?? group).add(node);
    });
  }
  const animated = [];
  root.traverse(node => {
    const animation = node.userData.animations?.[0];
    const data = animation?.TransformKeyData2 ?? animation?.TransformKeyData;
    if (!data) return;
    const transform = data.TransformKey;
    if (data.FloatKeys.length || transform?.TKey.length || transform?.RKey.length || transform?.SKey.length)
      animated.push({ node, data });
  });
  const seconds = value => value.split(':').reduce((sum, part) => sum * 60 + Number(part), 0);
  const vector = value => new THREE.Vector3(value.X, value.Y, value.Z);
  const quaternion = value => new THREE.Quaternion(value.X, value.Y, value.Z, value.W);
  function sample(keys, time, property, fallback, convert, interpolate) {
    if (!keys.length) return convert(fallback);
    if (time <= seconds(keys[0].Duration)) return convert(keys[0][property]);
    for (let i = 1; i < keys.length; i++) {
      const end = seconds(keys[i].Duration);
      if (time <= end) {
        const start = seconds(keys[i - 1].Duration);
        return interpolate(convert(keys[i - 1][property]), convert(keys[i][property]), (time - start) / (end - start));
      }
    }
    return convert(keys.at(-1)[property]);
  }
  function update(elapsed) {
    for (const { node, data } of animated) {
      const duration = seconds(data.Duration);
      const time = duration > 0 ? elapsed % duration : 0;
      const key = data.TransformKey;
      if (key && (key.TKey.length || key.RKey.length || key.SKey.length)) {
        node.matrix.compose(
          sample(key.TKey, time, 'Translation', key.Translation, vector, (a, b, t) => a.lerp(b, t)),
          sample(key.RKey, time, 'Rotation', key.Rotation, quaternion, (a, b, t) => a.slerp(b, t)),
          sample(key.SKey, time, 'Scale', key.Scale, vector, (a, b, t) => a.lerp(b, t)),
        );
        node.matrixWorldNeedsUpdate = true;
      }
      if (data.FloatKeys.length && node.isMesh) {
        const alpha = sample(data.FloatKeys, time, 'Alpha', 1, x => x, (a, b, t) => a + (b - a) * t);
        for (const material of Array.isArray(node.material) ? node.material : [node.material]) {
          material.opacity = alpha;
          if (alpha < 1 && !material.transparent) { material.transparent = true; material.needsUpdate = true; }
        }
      }
    }
  }
  update(0);
  root.updateMatrixWorld(true);
  return { root, helpers, skies, update };
}
