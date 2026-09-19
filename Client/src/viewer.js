import * as THREE from 'three';
import { PointerLockControls } from 'three/addons/controls/PointerLockControls.js';
import { loadMap } from './MapLoader.js';
import { loadTextureSet } from './ScnAssets.js';
import { loadMapIndex, mapAssetUrl, mapLabel, requestedMapId, resolveMapEntry } from './MapRegistry.js';

const status = document.querySelector('#status');
try {
  const renderer = new THREE.WebGLRenderer({ antialias: true });
  renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
  renderer.setSize(innerWidth, innerHeight);
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  renderer.toneMapping = THREE.NoToneMapping;
  document.body.prepend(renderer.domElement);
  const scene = new THREE.Scene();
  // The authored sky dome is ~860,000 units in radius; don't clip it at map distance.
  const camera = new THREE.PerspectiveCamera(60, innerWidth / innerHeight, 5, 2000000);
  const fly = new PointerLockControls(camera, renderer.domElement);
  const index = await loadMapIndex();
  const entry = resolveMapEntry(index, requestedMapId());
  // The remembered texture level is applied to the *load*, not just to the select; otherwise a map
  // opens at its original level while the menu claims the enhanced one.
  const TEXTURE_QUALITY_KEY = 'opens4l.texture-quality.v1';
  const maxTextureSize = renderer.capabilities.maxTextureSize;
  const rememberedQuality = localStorage.getItem(TEXTURE_QUALITY_KEY) === '4x' ? '4x' : '1x';
  const map = await loadMap(mapAssetUrl(entry, entry.manifest), entry.bundleFormat,
    { quality: rememberedQuality, maxTextureSize });
  const mapName = map.manifest.name ?? entry.id;
  document.title = `${mapName} · OpenS4L`;
  document.querySelector('#map-name').textContent = mapName;
  // The map menu is the same registry the viewer just resolved, so it needs no extra request.
  // It scrolls and filters: the roster is long enough that a plain list is unusable.
  const menuButton = document.querySelector('#maps');
  const menu = document.querySelector('#map-list');
  const items = document.querySelector('#map-items');
  const search = document.querySelector('#map-search');
  const empty = document.querySelector('#map-empty');
  for (const map of index.maps) {
    const link = document.createElement('a');
    link.href = `?map=${map.id}`;
    link.textContent = mapLabel(map);
    link.dataset.id = map.id;
    if (map.id === entry.id) link.setAttribute('aria-current', 'true');
    items.append(link);
  }
  const filterMaps = () => {
    const query = search.value.trim().toLowerCase();
    let shown = 0;
    for (const link of items.children) {
      const matches = !query || link.textContent.toLowerCase().includes(query) || link.dataset.id.includes(query);
      link.hidden = !matches;
      if (matches) shown++;
    }
    empty.hidden = shown > 0;
  };
  search.oninput = filterMaps;
  const setMenu = open => {
    menu.classList.toggle('open', open);
    menuButton.setAttribute('aria-expanded', String(open));
    // Focus the filter so a keyboard user can type immediately; the current map starts visible.
    if (open) { search.focus(); search.select(); }
  };
  menuButton.onclick = () => setMenu(!menu.classList.contains('open'));
  document.addEventListener('click', event => { if (!event.target.closest('.menu')) setMenu(false); });
  window.addEventListener('keydown', event => { if (event.key === 'Escape') setMenu(false); });
  filterMaps();
  scene.add(map.root);
  const configResponse = await fetch(mapAssetUrl(entry, entry.config));
  if (!configResponse.ok) throw new Error(`Map configuration: HTTP ${configResponse.status}`);
  const config = await configResponse.json();
  // The map's own configuration is the source for fog and cameras, and some maps omit either,
  // so every read here has a defined fallback instead of producing NaN.
  const render = config.RENDERER ?? {};
  const fogValues = ['FogColorR', 'FogColorG', 'FogColorB', 'FogMinDist', 'FogMaxDist'].map(key => Number(render[key]));
  const fog = fogValues.every(Number.isFinite)
    ? new THREE.Fog(new THREE.Color().setRGB(fogValues[0], fogValues[1], fogValues[2], THREE.SRGBColorSpace), fogValues[3], fogValues[4])
    : null;
  scene.fog = fog;
  // A map without a fog colour keeps its own sky dome; the viewer's own backdrop fills the rest.
  scene.background = fog ? fog.color : new THREE.Color(0x161d2a);
  function pose(position, target) {
    fly.unlock();
    camera.position.set(position[0], position[1], -position[2]);
    camera.lookAt(target[0], target[1], -target[2]);
  }
  // The overview is fitted to the map's own geometry: the authored sky dome is hundreds of
  // thousands of units across, so it is excluded and every map gets a usable aerial view.
  const bounds = new THREE.Box3();
  const skyNodes = new Set();
  for (const sky of map.skies) sky.traverse(node => skyNodes.add(node));
  map.root.updateMatrixWorld(true);
  map.root.traverse(node => {
    if (!node.isMesh || !node.visible || skyNodes.has(node)) return;
    node.geometry.computeBoundingBox();
    bounds.union(node.geometry.boundingBox.clone().applyMatrix4(node.matrixWorld));
  });
  const centre = bounds.getCenter(new THREE.Vector3());
  const extent = bounds.getSize(new THREE.Vector3()).length();
  function overview() {
    fly.unlock();
    camera.position.copy(centre).addScaledVector(new THREE.Vector3(1, 1.4, -1.6).normalize(), extent);
    camera.lookAt(centre);
    document.querySelector('#fog').checked = false;
    scene.fog = null;
  }
  function ingame() {
    const environment = config.ENVIRONMENT ?? {};
    const position = ['X', 'Y', 'Z'].map(axis => Number(environment['InGameCameraPos' + axis]));
    const target = ['X', 'Y', 'Z'].map(axis => Number(environment['InGameCameraLookAt' + axis]));
    // Maps without an in-game camera (and without broadcasting cameras) fall back to the overview.
    if (![...position, ...target].every(Number.isFinite)) { overview(); return; }
    pose(position, target);
    document.querySelector('#fog').checked = Boolean(fog);
    scene.fog = fog;
  }
  const cameraSelect = document.querySelector('#camera');
  for (const [key, value] of Object.entries(config).filter(([key]) => key.startsWith('BROADCASTINGCAMERA_'))) {
    const option = document.createElement('option');
    option.value = key; option.textContent = `Camera ${key.split('_').at(-1)}`;
    cameraSelect.append(option);
  }
  cameraSelect.onchange = () => {
    const c = config[cameraSelect.value];
    if (!c) return;
    const p = ['X', 'Y', 'Z'].map(a => Number(c['Pos' + a]));
    pose(p, p.map((n, i) => n + Number(c['Dir' + ['X', 'Y', 'Z'][i]]) * 1500));
  };
  document.querySelector('#overview').onclick = overview;
  document.querySelector('#ingame').onclick = ingame;
  document.querySelector('#fly').onclick = () => fly.lock();
  renderer.domElement.addEventListener('click', () => { if (!fly.isLocked) fly.lock(); });
  document.querySelector('#fog').onchange = e => { scene.fog = e.target.checked ? fog : null; };
  const wireframe = document.querySelector('#wireframe');
  const applyWireframe = () => map.root.traverse(node => {
    if (node.isMesh) for (const material of Array.isArray(node.material) ? node.material : [node.material]) material.wireframe = wireframe.checked;
  });
  wireframe.onchange = applyWireframe;
  // Texture level: every map carries the select, but only a bundle that records generated variants
  // can offer 4×, so an unavailable option is disabled and the status line reports what each
  // texture actually resolved to — lightmaps and normal maps keep their decoded original because
  // generated pixels are never substituted for semantic data.
  const textureQuality = document.querySelector('#texture-quality');
  const textureQualityStatus = document.querySelector('#texture-quality-status');
  const availableLevels = new Set(['1x']);
  for (const descriptor of Object.values(map.manifest.textures ?? {}))
    for (const level of Object.keys(descriptor.variants ?? (descriptor.file ? { '1x': descriptor } : {}))) availableLevels.add(level);
  for (const option of textureQuality.options) option.disabled = !availableLevels.has(option.value);
  textureQuality.value = availableLevels.has(rememberedQuality) ? rememberedQuality : '1x';
  let visibleTextures = map.textures;
  let textureUrls = map.urls ?? new Map();
  const label = level => level.replace('x', '×');
  function updateTextureQualityStatus(requested) {
    const counts = new Map();
    for (const level of map.levels.values()) counts.set(level, (counts.get(level) ?? 0) + 1);
    const total = map.levels.size;
    const fallback = [...counts].filter(([level]) => level !== requested).sort()
      .map(([level, count]) => `${count} at ${label(level)}`).join(', ');
    textureQualityStatus.textContent = fallback
      ? `Requested: ${label(requested)} · Loaded: ${label(requested)} on ${counts.get(requested) ?? 0} of ${total} textures · ${fallback} kept their original level`
      : `Requested: ${label(requested)} · Loaded: ${label(requested)} on all ${total} textures`;
  }
  async function selectTextureQuality(quality) {
    textureQuality.value = quality;
    textureQualityStatus.textContent = `Requested: ${label(quality)} · Loading…`;
    try {
      const next = await loadTextureSet(map.manifest, map.base, quality, { maxTextureSize });
      const previous = visibleTextures;
      map.levels = next.levels;
      visibleTextures = next.textures;
      textureUrls = next.urls;
      map.rebuildMaterials(next.textures);
      applyWireframe();
      for (const texture of previous.values()) texture.dispose();
      localStorage.setItem(TEXTURE_QUALITY_KEY, quality);
      updateTextureQualityStatus(quality);
      updateStatus();
      return true;
    } catch (error) {
      status.textContent = error.message;
      status.classList.add('error');
      updateTextureQualityStatus(textureQuality.value);
      return false;
    }
  }
  textureQuality.onchange = () => selectTextureQuality(textureQuality.value);
  if (!availableLevels.has('4x')) textureQualityStatus.textContent = 'Requested: 1× · Loaded: 1× (this map has no generated 4× levels: run `make threejs-map-upscale MAP=' + entry.id + '`)';
  else updateTextureQualityStatus(textureQuality.value);
  const keys = new Set();
  window.addEventListener('keydown', e => { if (fly.isLocked) { keys.add(e.code); e.preventDefault(); } });
  window.addEventListener('keyup', e => keys.delete(e.code));
  window.addEventListener('blur', () => keys.clear());
  fly.addEventListener('lock', () => { document.querySelector('#crosshair').style.display = 'block'; document.activeElement?.blur(); });
  fly.addEventListener('unlock', () => { keys.clear(); document.querySelector('#crosshair').style.display = 'none'; });
  window.addEventListener('resize', () => {
    camera.aspect = innerWidth / innerHeight; camera.updateProjectionMatrix(); renderer.setSize(innerWidth, innerHeight);
  });
  // The viewer opens on the fitted overview of the map's own geometry, which shows the whole
  // layout; the URL may still request one authored camera via ?camera=<name>.
  const requestedCamera = new URLSearchParams(location.search).get('camera') ?? '';
  cameraSelect.value = requestedCamera && config[requestedCamera] ? requestedCamera : '';
  if (cameraSelect.value) cameraSelect.onchange(); else overview();
  const total = map.manifest.totals;
  // What the viewer actually loaded: the manifest, the geometry buffer and the current texture set.
  // Resource timings give the real byte counts, so this stays honest across encodings and cache
  // states instead of reporting a size the manifest happens to record.
  const formatSize = bytes => (bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`);
  function loadedBytes() {
    const sizes = new Map(performance.getEntriesByType('resource')
      .map(entry => [entry.name, entry.transferSize || entry.encodedBodySize || 0]));
    const urls = [map.manifestUrl, map.bufferUrl, ...textureUrls.values()];
    return urls.reduce((sum, url) => sum + (sizes.get(url) ?? sizes.get(new URL(url).href) ?? 0), 0);
  }
  function updateStatus() {
    status.classList.remove('error');
    status.textContent = `${total.scenes} SCNs · ${total.models} meshes · ${total.triangles.toLocaleString('en-US')} triangles · `
      + `${Object.keys(map.manifest.textures).length} textures · ${formatSize(loadedBytes())} loaded`;
  }
  updateStatus();
  const flightDirection = new THREE.Vector3();
  const timer = new THREE.Timer();
  let elapsed = 0;
  renderer.setAnimationLoop(() => {
    timer.update();
    const dt = Math.min(timer.getDelta(), 0.05);
    if (document.querySelector('#animation').checked) { elapsed += dt; map.update(elapsed); }
    if (fly.isLocked) {
      const speed = (keys.has('ShiftLeft') || keys.has('ShiftRight') ? 6000 : 1800) * dt;
      camera.getWorldDirection(flightDirection);
      camera.position.addScaledVector(flightDirection, ((keys.has('KeyW') ? 1 : 0) - (keys.has('KeyS') ? 1 : 0)) * speed);
      fly.moveRight(((keys.has('KeyD') ? 1 : 0) - (keys.has('KeyA') ? 1 : 0)) * speed);
      camera.position.y += ((keys.has('KeyE') ? 1 : 0) - (keys.has('KeyQ') ? 1 : 0)) * speed;
    }
    renderer.render(scene, camera);
    window.mapReady = renderer.info.render.triangles > 0;
  });
  // Inspectable integration surface for smoke tests and importing the actual model.
  window.s4map = { ...map, entry, scene, camera, renderer, fly, overview, ingame,
    selectTextureQuality, textureQualityStatus, textures: () => visibleTextures, levels: () => map.levels };
} catch (error) {
  status.textContent = error.message;
  status.classList.add('error');
  console.error(error);
}
