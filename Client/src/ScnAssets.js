import * as THREE from 'three';

/** Shared transport/geometry/material layer for SCN-derived maps and characters. */
export async function loadScnAssets(url, expectedFormat) {
  const base = new URL(url, window.location.href);
  const response = await fetch(base);
  if (!response.ok) throw new Error(`Asset manifest: HTTP ${response.status}. Run the converter first.`);
  const manifest = await response.json();
  if (manifest.format !== expectedFormat || manifest.version !== 1) throw new Error('Unsupported SCN asset bundle');
  const binary = await fetch(new URL(manifest.buffer, base));
  if (!binary.ok) throw new Error(`Geometry: HTTP ${binary.status}`);
  const buffer = await binary.arrayBuffer();
  const textures = new Map();
  const loader = new THREE.TextureLoader();
  await Promise.all(Object.entries(manifest.textures).map(async ([path, info]) => {
    const texture = await loader.loadAsync(new URL(info.file, base).href);
    // The parser already flips V. Decoder rows are unchanged and upload flipY=true.
    texture.colorSpace = THREE.SRGBColorSpace;
    texture.wrapS = texture.wrapT = THREE.RepeatWrapping;
    texture.name = path;
    textures.set(path, texture);
  }));
  return { manifest, buffer, textures, base };
}

export function scnGeometry(data, buffer) {
  const geometry = new THREE.BufferGeometry();
  const attribute = info => new THREE.BufferAttribute(
    info.type === 'Uint32Array'
      ? new Uint32Array(buffer, info.byteOffset, info.count * info.itemSize)
      : new Float32Array(buffer, info.byteOffset, info.count * info.itemSize), info.itemSize);
  geometry.setAttribute('position', attribute(data.positions));
  if (data.normals.count) geometry.setAttribute('normal', attribute(data.normals));
  if (data.uv.count) geometry.setAttribute('uv', attribute(data.uv));
  if (data.uv1.count) geometry.setAttribute('uv1', attribute(data.uv1));
  geometry.setIndex(attribute(data.indices));
  data.groups.forEach((range, i) => geometry.addGroup(range.start, range.count, i));
  return geometry;
}

export function scnMaterials(node, textures, replacements = {}, shaded = false) {
  const flags = node.details.flags;
  const result = node.geometry.groups.map((range, i) => {
    if (!range.map) return new THREE.MeshBasicMaterial({ visible: false });
    const map = textures.get(replacements[range.map] ?? range.map)?.clone() ?? null;
    const sideMap = textures.get(replacements[range.lightMap] ?? range.lightMap)?.clone() ?? null;
    if (map) {
      map.generateMipmaps = !(flags & 2048);
      if (flags & 2048) map.minFilter = THREE.LinearFilter;
      map.needsUpdate = true;
    }
    const normalMap = node.details.extraUV === 2 ? sideMap : null;
    const lightMap = normalMap ? null : sideMap;
    if (lightMap) {
      lightMap.channel = 1;
      lightMap.wrapS = lightMap.wrapT = THREE.ClampToEdgeWrapping;
      lightMap.needsUpdate = true;
    }
    const properties = {
      name: `${node.name}:${i}`, map, lightMap, lightMapIntensity: Math.PI,
      side: flags & 8 ? THREE.DoubleSide : THREE.FrontSide,
      transparent: !!(flags & (2 | 32)),
      blending: flags & 32 ? THREE.AdditiveBlending : THREE.NormalBlending,
      alphaTest: flags & 4 ? 0.5 : 0,
      depthWrite: !(flags & 64), depthTest: !/nodepthtest/i.test(node.name),
      fog: !(flags & 512), toneMapped: false,
    };
    if (shaded && !(flags & 1)) {
      if (normalMap) { normalMap.colorSpace = THREE.NoColorSpace; normalMap.needsUpdate = true; }
      return new THREE.MeshPhongMaterial({ ...properties, normalMap, shininess: 8, specular: 0x111111 });
    }
    return new THREE.MeshBasicMaterial(properties);
  });
  return result.length ? result : [new THREE.MeshBasicMaterial({ visible: false })];
}

/** Source Matrix4x4 fields use row-vector notation; arrays fit Three's column-major storage. */
export function scnMatrix(value) {
  if (Array.isArray(value)) return new THREE.Matrix4().fromArray(value);
  return new THREE.Matrix4().fromArray([
    value.M11, value.M12, value.M13, value.M14, value.M21, value.M22, value.M23, value.M24,
    value.M31, value.M32, value.M33, value.M34, value.M41, value.M42, value.M43, value.M44,
  ]);
}

export function disposeScnObject(root) {
  root.traverse(node => {
    if (!node.isMesh) return;
    node.geometry.dispose();
    for (const material of Array.isArray(node.material) ? node.material : [node.material]) {
      for (const name of ['map', 'lightMap', 'normalMap']) material[name]?.dispose();
      material.dispose();
    }
    node.skeleton?.dispose();
    node.customDepthMaterial?.dispose();
    node.customDistanceMaterial?.dispose();
  });
}
