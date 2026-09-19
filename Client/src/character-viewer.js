import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { loadScnAssets, disposeScnObject } from './ScnAssets.js';
import { CharacterModel } from './CharacterModel.js';
import { WardrobeLibrary } from './WardrobeLibrary.js';
import { STORAGE_KEY, loadOutfits, saveOutfit, deleteOutfit, loadLastOutfitId, saveLastOutfitId } from './SavedOutfits.js';
import { CharacterAnimations } from './CharacterAnimations.js';

const status = document.querySelector('#status');
const stage = document.querySelector('#stage');
try {
  const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
  renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  renderer.toneMapping = THREE.NoToneMapping;
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;
  stage.prepend(renderer.domElement);
  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(40, 1, 0.5, 4000);
  const controls = new OrbitControls(camera, renderer.domElement);
  controls.enableDamping = true;
  controls.minDistance = 80; controls.maxDistance = 1600; controls.autoRotateSpeed = 1;
  const hemisphere = new THREE.HemisphereLight(0xe1edff, 0x666778, 1.8);
  scene.add(hemisphere);
  const key = new THREE.DirectionalLight(0xfff1de, 2.2);
  key.position.set(-250, 450, 400); key.castShadow = true;
  key.shadow.mapSize.set(2048, 2048);
  Object.assign(key.shadow.camera, { left: -220, right: 220, top: 350, bottom: -150, near: 1, far: 1200 });
  key.shadow.bias = -0.0002;
  scene.add(key);
  const rim = new THREE.DirectionalLight(0xa8caff, 1.4);
  rim.position.set(300, 280, -250); scene.add(rim);
  // Lighting environments: one preset each for the hemisphere/key/rim lights, the stage
  // background and the floor shadow strength. Purely a viewing choice — no asset data changes.
  const LIGHTING_KEY = 'opens4l.lighting.v1';
  const LIGHTING_ENVIRONMENTS = [
    { id: 'studio', label: 'Studio', background: 'radial-gradient(ellipse at 50% 45%, #364354 0%, #222b38 45%, #151b25 85%)',
      hemisphere: { sky: 0xe1edff, ground: 0x666778, intensity: 1.8 },
      key: { color: 0xfff1de, intensity: 2.2, position: [-250, 450, 400] },
      rim: { color: 0xa8caff, intensity: 1.4, position: [300, 280, -250] }, shadow: 0.23 },
    { id: 'daylight', label: 'Daylight', background: 'radial-gradient(ellipse at 50% 40%, #4d5b6b 0%, #2c3542 48%, #191f28 88%)',
      hemisphere: { sky: 0xf4f9ff, ground: 0x8b919d, intensity: 2.3 },
      key: { color: 0xffffff, intensity: 2.7, position: [140, 520, 340] },
      rim: { color: 0xdbe8ff, intensity: 1.1, position: [-300, 250, -260] }, shadow: 0.28 },
    { id: 'sunset', label: 'Sunset', background: 'radial-gradient(ellipse at 50% 55%, #5a3a44 0%, #33232e 50%, #1a141d 88%)',
      hemisphere: { sky: 0xffd7ad, ground: 0x4a3a3a, intensity: 1.7 },
      key: { color: 0xffb066, intensity: 2.8, position: [-420, 320, 260] },
      rim: { color: 0xff8f6b, intensity: 1.7, position: [320, 220, -300] }, shadow: 0.3 },
    { id: 'night', label: 'Night', background: 'radial-gradient(ellipse at 50% 45%, #1f2b3d 0%, #141c27 50%, #0b0e14 90%)',
      hemisphere: { sky: 0x9fc0ff, ground: 0x1b2430, intensity: 1.0 },
      key: { color: 0xcfe0ff, intensity: 1.5, position: [-200, 420, 380] },
      rim: { color: 0x6fa8ff, intensity: 2.2, position: [320, 260, -260] }, shadow: 0.36 },
    { id: 'showroom', label: 'Showroom', background: 'radial-gradient(ellipse at 50% 45%, #e8edf3 0%, #c9d2dc 55%, #a9b4c0 90%)',
      hemisphere: { sky: 0xffffff, ground: 0xd7dde4, intensity: 2.6 },
      key: { color: 0xffffff, intensity: 1.3, position: [0, 520, 240] },
      rim: { color: 0xffffff, intensity: 0.7, position: [0, 280, -320] }, shadow: 0.12 },
  ];
  const basicOnly = new URLSearchParams(location.search).has('basic');
  const animationsDisabled = new URLSearchParams(location.search).has('noAnimations');
  const TEXTURE_QUALITY_KEY = 'opens4l.texture-quality.v1';
  const library = basicOnly ? null : await WardrobeLibrary.load('./Models/Characters/Wardrobe/index.json', { maxTextureSize: renderer.capabilities.maxTextureSize });
  // Apply the remembered level *before* the first texture loads. Otherwise a reload starts at 1×
  // and the status honestly reports "Requested 4× · Loaded 1×" until something re-requests it.
  const savedTextureQuality = localStorage.getItem(TEXTURE_QUALITY_KEY);
  if (library && savedTextureQuality && library.index.textures) {
    const available = new Set(['1x']);
    for (const descriptor of Object.values(library.index.textures))
      for (const quality of Object.keys(descriptor.variants ?? {})) available.add(quality);
    if (available.has(savedTextureQuality)) library.setQuality(savedTextureQuality);
  }
  const assets = library
    ? { manifest: library.runtimeManifest, textures: library.textures, base: library.base }
    : await loadScnAssets('./Models/Characters/BasicFemale/character.json', 's4-character-threejs');
  const character = library ? await library.createModel() : new CharacterModel(assets.manifest, assets.buffer, assets.textures);
  const textureQuality = document.querySelector('#texture-quality');
  const textureQualityStatus = document.querySelector('#texture-quality-status');
  const availableTextureQualities = new Set(['1x']);
  for (const descriptor of Object.values(assets.manifest.textures ?? {}))
    for (const quality of Object.keys(descriptor.variants ?? {})) availableTextureQualities.add(quality);
  for (const option of textureQuality.options) option.disabled = !availableTextureQualities.has(option.value);
  textureQuality.value = savedTextureQuality && availableTextureQualities.has(savedTextureQuality) ? savedTextureQuality : '1x';
  if (!library) textureQuality.disabled = true;
  const pendingUiActions = new Set();
  function updateTextureQualityStatus() {
    const requested = textureQuality.value;
    const loaded = library?.textureEntries?.size
      ? [...library.textureEntries.values()].map(entry => entry.quality).sort()[0]
      : '1x';
    textureQualityStatus.textContent = `Requested: ${requested.replace('x', '×')} · Loaded: ${(loaded ?? '1x').replace('x', '×')}`;
  }
  async function selectTextureQuality(quality) {
    if (!library) return false;
    textureQuality.value = quality;
    return runUi(async () => {
      textureQualityStatus.textContent = `Requested: ${quality.replace('x', '×')} · Loading…`;
      await library.setQuality(quality);
      localStorage.setItem(TEXTURE_QUALITY_KEY, quality);
      populateEquipment(); rebuildSkeleton(); applyDisplay(); updateStatus(); updateTextureQualityStatus();
      return true;
    });
  }
  textureQuality.onchange = () => selectTextureQuality(textureQuality.value);
  function runUi(action) {
    const pending = action().catch(error => {
      status.textContent = error.message; status.classList.add('error');
      populateEquipment();
      return false;
    }).finally(() => pendingUiActions.delete(pending));
    pendingUiActions.add(pending);
    return pending;
  }
  function selectEquipment(slot, id, variant = 'default') {
    return runUi(async () => {
      status.textContent = 'Loading equipment…';
      if (library) { if (!await library.equip(slot, id, variant)) return false; }
      else character.setItem(slot, id, variant);
      populateEquipment(); rebuildSkeleton(); applyDisplay(); updateStatus();
      if (library) { await library.reconcileQuality(); updateTextureQualityStatus(); }
      return true;
    });
  }
  scene.add(character.root);
  const floor = new THREE.Mesh(new THREE.PlaneGeometry(1100, 1100), new THREE.ShadowMaterial({ opacity: 0.23 }));
  floor.rotation.x = -Math.PI / 2; floor.receiveShadow = true; scene.add(floor);
  // Lighting environments are chosen in the page menu; the choice is remembered per browser.
  const lightingSelect = document.querySelector('#lighting');
  for (const environment of LIGHTING_ENVIRONMENTS) lightingSelect.add(new Option(environment.label, environment.id));
  function applyLighting(id) {
    const environment = LIGHTING_ENVIRONMENTS.find(item => item.id === id) ?? LIGHTING_ENVIRONMENTS[0];
    hemisphere.color.setHex(environment.hemisphere.sky);
    hemisphere.groundColor.setHex(environment.hemisphere.ground);
    hemisphere.intensity = environment.hemisphere.intensity;
    key.color.setHex(environment.key.color); key.intensity = environment.key.intensity;
    key.position.fromArray(environment.key.position);
    rim.color.setHex(environment.rim.color); rim.intensity = environment.rim.intensity;
    rim.position.fromArray(environment.rim.position);
    stage.style.background = environment.background;
    floor.material.opacity = environment.shadow;
    lightingSelect.value = environment.id;
    try { localStorage.setItem(LIGHTING_KEY, environment.id); } catch { /* private mode: not persisted */ }
    return environment.id;
  }
  lightingSelect.onchange = () => applyLighting(lightingSelect.value);
  let skeleton;
  function rebuildSkeleton() {
    if (skeleton) { scene.remove(skeleton); skeleton.geometry.dispose(); skeleton.material.dispose(); }
    skeleton = new THREE.SkeletonHelper(character.root);
    skeleton.material.depthTest = false; skeleton.material.transparent = true; skeleton.renderOrder = 10;
    skeleton.visible = document.querySelector('#skeleton').checked;
    scene.add(skeleton);
  }
  function bounds() {
    character.root.updateMatrixWorld(true);
    const box = new THREE.Box3();
    character.root.traverse(node => {
      if (!node.isMesh || !node.visible) return;
      if (node.isSkinnedMesh) node.computeBoundingBox();
      box.expandByObject(node, true);
    });
    if (box.isEmpty()) throw new Error('Character has no visible geometry');
    return box;
  }
  function frame(direction = 'front') {
    const box = bounds(), center = box.getCenter(new THREE.Vector3()), size = box.getSize(new THREE.Vector3());
    floor.position.y = box.min.y - 0.5;
    controls.target.copy(center);
    const distance = Math.max(size.y, size.x / camera.aspect) / (2 * Math.tan(THREE.MathUtils.degToRad(camera.fov / 2))) * 1.25;
    const offset = direction === 'side' ? new THREE.Vector3(distance, 0, 0) : new THREE.Vector3(0, 0, direction === 'back' ? -distance : distance);
    camera.position.copy(center).add(offset);
    camera.lookAt(center); controls.update();
  }
  function applyDisplay() {
    character.root.traverse(node => {
      if (node.isMesh) for (const material of node.material) material.wireframe = document.querySelector('#wireframe').checked;
    });
  }
  // Resource timings give the real byte counts, so this stays honest across encodings and cache
  // states instead of reporting a size the manifest happens to record. What counts is what this
  // page holds now: the index it read, the textures it has equipped, and the scenes it has loaded.
  const formatSize = bytes => (bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`);
  function loadedBytes() {
    const sizes = new Map(performance.getEntriesByType('resource')
      .map(entry => [entry.name, entry.transferSize || entry.encodedBodySize || 0]));
    const urls = new Set();
    if (library) {
      urls.add(library.base.href);
      for (const entry of library.textureEntries.values()) {
        const file = entry?.descriptor?.file;
        if (file) urls.add(new URL(file, library.base).href);
      }
      for (const key of library.sceneCache.keys()) {
        const descriptor = library.index.scenes[key];
        if (!descriptor) continue;
        urls.add(new URL(descriptor.json, library.base).href);
        urls.add(new URL(descriptor.bin, library.base).href);
      }
    } else {
      // The basic outfit has no library to ask, so count everything this page pulled from the assets.
      for (const url of sizes.keys()) if (url.includes('/Models/Characters/')) urls.add(url);
    }
    return [...urls].reduce((sum, url) => sum + (sizes.get(url) ?? 0), 0);
  }
  function updateStatus() {
    let meshes = 0, vertices = 0;
    character.root.traverse(node => { if (node.isMesh) { meshes++; vertices += node.geometry.attributes.position.count; } });
    status.classList.remove('error');
    status.textContent = `${character.body.label} · ${character.equipment.size} equipment slots · ${meshes} meshes · ${vertices.toLocaleString('en-US')} vertices · ${formatSize(loadedBytes())} loaded`;
    const unavailable = library?.index.inventory.filter(item => item.status === 'unavailable') ?? [];
    document.querySelector('#wardrobe-count').textContent = library
      ? `${character.body.items.length} imported items · ${unavailable.length} unavailable · ${library.textures.size} textures loaded`
      : 'Basic outfit only · open the full wardrobe for more equipment';
  }
  // Equipment picker: a custom listbox replaces the plain <select> so the option list can show a
  // live, rotating render of whichever item is highlighted — a native <select>'s options are drawn
  // by the OS and fire no hover events, so a per-option preview needs a DOM-rendered list. The
  // original <select> stays in the DOM (hidden) as the actual source of truth: it keeps the exact
  // same onchange wiring, and existing automation that sets `select.value` and dispatches `change`
  // keeps working unchanged.
  let previewCanvas, previewRenderer, previewCamera;
  function getPreviewCanvas() {
    if (!previewCanvas) {
      previewCanvas = document.createElement('canvas');
      previewCanvas.className = 'equip-preview-canvas';
      previewRenderer = new THREE.WebGLRenderer({ canvas: previewCanvas, antialias: true, alpha: true });
      previewRenderer.setPixelRatio(Math.min(devicePixelRatio, 2));
      previewRenderer.setSize(96, 96, false);
      previewRenderer.outputColorSpace = THREE.SRGBColorSpace;
      previewCamera = new THREE.PerspectiveCamera(32, 1, 1, 4000);
    }
    return previewCanvas;
  }
  let previewParts = null, previewCenter = null, previewDistance = 0, previewAngle = 0, previewRequest = 0;
  function clearPreviewParts() {
    if (!previewParts) return;
    for (const part of previewParts) {
      part.group.removeFromParent(); disposeScnObject(part.group);
      part.extraGroups?.forEach(group => { group.removeFromParent(); disposeScnObject(group); });
    }
    previewParts = null;
  }
  function stopPreview() { previewRequest++; clearPreviewParts(); }
  async function previewItem(slot, itemId) {
    const request = ++previewRequest;
    clearPreviewParts();
    if (!itemId) return;
    try {
      const item = character.body.items.find(candidate => candidate.id === itemId && candidate.slot === slot);
      if (!item) return;
      const variant = item.variants.find(candidate => candidate.id === 'default') ?? item.variants[0];
      if (library) await library.prepareItem(character.body, slot, itemId, variant.id);
      if (request !== previewRequest) return;
      const parts = item.parts.map(part => character.buildPart(part, variant.maps, item));
      for (const part of parts) part.parent.add(part.group);
      const roots = parts.flatMap(part => [part.group, ...(part.extraGroups ?? [])]);
      for (const root of roots) root.visible = false;
      character.root.updateMatrixWorld(true);
      const meshBoxes = [];
      for (const root of roots) root.traverse(node => {
        if (!node.isMesh) return;
        if (node.isSkinnedMesh) node.computeBoundingBox();
        meshBoxes.push(new THREE.Box3().setFromObject(node, true));
      });
      let box = meshBoxes.reduce((acc, meshBox) => acc.union(meshBox), new THREE.Box3());
      if (box.isEmpty()) { for (const part of parts) { part.group.removeFromParent(); disposeScnObject(part.group); part.extraGroups?.forEach(group => { group.removeFromParent(); disposeScnObject(group); }); } return; }
      // Frame by height, not the full bounding box: a T-pose shirt/gloves' sleeves span far wider
      // than the garment itself, and fitting that span would shrink every torso item to a speck.
      let size = box.getSize(new THREE.Vector3());
      // A left/right pair (gloves, shoes, a symmetric accessory) is wider than it is tall for a
      // different reason: it is two separate pieces either side of the body with empty air between
      // them, and a camera aimed at their midpoint frames that gap instead of either piece. Framing
      // just the larger of the two halves keeps a real piece of geometry on screen.
      if (size.x > Math.max(size.y, size.z) * 2 && meshBoxes.length > 1) {
        const midpointX = box.getCenter(new THREE.Vector3()).x;
        const sides = [meshBoxes.filter(b => b.getCenter(new THREE.Vector3()).x < midpointX),
          meshBoxes.filter(b => b.getCenter(new THREE.Vector3()).x >= midpointX)];
        const largerSide = sides[0].reduce((sum, b) => sum + b.getSize(new THREE.Vector3()).length(), 0)
          >= sides[1].reduce((sum, b) => sum + b.getSize(new THREE.Vector3()).length(), 0) ? sides[0] : sides[1];
        if (largerSide.length) {
          box = largerSide.reduce((acc, meshBox) => acc.union(meshBox), new THREE.Box3());
          size = box.getSize(new THREE.Vector3());
        }
      }
      previewCenter = box.getCenter(new THREE.Vector3());
      const heightSpan = Math.max(size.y, size.z, 6);
      previewDistance = heightSpan / (2 * Math.tan(THREE.MathUtils.degToRad(previewCamera.fov / 2))) * 1.3;
      previewParts = parts;
    } catch { /* asset unavailable for preview; leave it blank rather than fail the picker */ }
  }
  let openPicker = null;
  function closeOpenPicker() {
    if (!openPicker) return;
    openPicker.popup.remove();
    openPicker.trigger.setAttribute('aria-expanded', 'false');
    openPicker = null;
    stopPreview();
  }
  document.addEventListener('mousedown', event => {
    if (openPicker && !openPicker.element.contains(event.target)) closeOpenPicker();
  });
  document.addEventListener('keydown', event => { if (event.key === 'Escape' && openPicker) { closeOpenPicker(); } });
  function openEquipmentPicker(element, trigger, slot, itemSelect) {
    if (openPicker?.element === element) { closeOpenPicker(); return; }
    closeOpenPicker();
    trigger.setAttribute('aria-expanded', 'true');
    const popup = document.createElement('div'); popup.className = 'equip-popup';
    const preview = document.createElement('div'); preview.className = 'equip-preview'; preview.append(getPreviewCanvas());
    const list = document.createElement('ul'); list.className = 'equip-options'; list.setAttribute('role', 'listbox'); list.tabIndex = -1;
    const options = [...itemSelect.options];
    let activeIndex = Math.max(0, options.findIndex(option => option.value === itemSelect.value));
    const setActive = index => {
      activeIndex = index;
      optionEls.forEach((el, i) => el.classList.toggle('active', i === index));
      optionEls[index]?.scrollIntoView({ block: 'nearest' });
      previewItem(slot, options[index].value);
    };
    const commit = index => { itemSelect.value = options[index].value; itemSelect.dispatchEvent(new Event('change')); closeOpenPicker(); };
    const optionEls = options.map((option, index) => {
      const li = document.createElement('li'); li.setAttribute('role', 'option'); li.textContent = option.text; li.dataset.value = option.value;
      if (option.value === itemSelect.value) li.setAttribute('aria-selected', 'true');
      li.addEventListener('mouseenter', () => setActive(index));
      li.addEventListener('click', () => commit(index));
      return li;
    });
    list.append(...optionEls);
    list.addEventListener('keydown', event => {
      if (event.key === 'ArrowDown') { event.preventDefault(); setActive(Math.min(activeIndex + 1, options.length - 1)); }
      else if (event.key === 'ArrowUp') { event.preventDefault(); setActive(Math.max(activeIndex - 1, 0)); }
      else if (event.key === 'Home') { event.preventDefault(); setActive(0); }
      else if (event.key === 'End') { event.preventDefault(); setActive(options.length - 1); }
      else if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); commit(activeIndex); }
      else if (event.key === 'Escape') { closeOpenPicker(); trigger.focus(); }
    });
    popup.append(preview, list);
    element.append(popup);
    openPicker = { element, popup, trigger };
    list.focus();
    setActive(activeIndex);
  }
  function buildEquipmentPicker(slot, itemSelect) {
    const picker = document.createElement('div'); picker.className = 'equip-picker';
    const trigger = document.createElement('button'); trigger.type = 'button'; trigger.className = 'equip-trigger'; trigger.id = `slot-${slot}-trigger`;
    trigger.setAttribute('aria-haspopup', 'listbox'); trigger.setAttribute('aria-expanded', 'false');
    trigger.textContent = itemSelect.options[itemSelect.selectedIndex]?.text ?? 'None';
    trigger.onclick = () => openEquipmentPicker(picker, trigger, slot, itemSelect);
    picker.append(trigger, itemSelect);
    return picker;
  }
  function populateEquipment() {
    closeOpenPicker();
    const container = document.querySelector('#equipment'); container.replaceChildren();
    const query = document.querySelector('#equipment-search').value.trim().toLowerCase();
    const matches = item => `${item.id} ${item.label}`.toLowerCase().includes(query);
    const slots = ['hair', 'face', 'shirt', 'pants', 'gloves', 'shoes', 'accessory'].filter(slot => character.body.items.some(item => item.slot === slot));
    for (const slot of slots) {
      const row = document.createElement('div'); row.className = 'slot';
      const selection = character.equipment.get(slot);
      const items = character.body.items.filter(item => item.slot === slot && (matches(item) || item.id === selection?.item.id))
        .sort((a, b) => a.label.localeCompare(b.label, 'en') || a.id.localeCompare(b.id));
      const label = document.createElement('label'); label.htmlFor = `slot-${slot}-trigger`;
      label.textContent = `${slot.charAt(0).toUpperCase() + slot.slice(1)} (${items.filter(matches).length})`;
      const itemSelect = document.createElement('select'); itemSelect.id = `slot-${slot}`; itemSelect.dataset.slot = slot; itemSelect.hidden = true; itemSelect.tabIndex = -1;
      if (!(slot in character.body.defaults)) itemSelect.add(new Option('None', ''));
      for (const item of items) itemSelect.add(new Option(`${item.label} · ${item.id}${matches(item) ? '' : ' (selected)'}`, item.id));
      itemSelect.value = selection?.item.id ?? '';
      itemSelect.onchange = () => selectEquipment(slot, itemSelect.value);
      const picker = buildEquipmentPicker(slot, itemSelect);
      const variantRow = document.createElement('div'); variantRow.className = 'variant'; variantRow.hidden = !selection;
      const variantLabel = document.createElement('label'); variantLabel.htmlFor = `variant-${slot}`; variantLabel.textContent = 'Skin';
      const variantSelect = document.createElement('select'); variantSelect.id = `variant-${slot}`; variantSelect.dataset.variant = slot;
      if (selection) {
        for (const variant of selection.item.variants) variantSelect.add(new Option(variant.label, variant.id));
        variantSelect.value = selection.variant.id; variantSelect.disabled = selection.item.variants.length === 1;
      }
      variantSelect.onchange = () => selectEquipment(slot, itemSelect.value, variantSelect.value);
      variantRow.append(variantLabel, variantSelect); row.append(label, picker, variantRow); container.append(row);
    }
    const unavailable = library?.index.inventory.filter(item => item.status === 'unavailable' && matches(item)) ?? [];
    const list = document.querySelector('#unavailable-items'); list.replaceChildren();
    for (const item of unavailable) {
      const row = document.createElement('li'); row.textContent = `${item.id} — ${item.label}: ${item.reason ?? 'Asset unavailable'}`; list.append(row);
    }
    document.querySelector('#unavailable-summary').textContent = `Unavailable source items (${unavailable.length})`;
    document.querySelector('#unavailable').hidden = !unavailable.length;
  }
  document.querySelector('#equipment-search').addEventListener('input', populateEquipment);
  const bodySelect = document.querySelector('#body');
  for (const body of assets.manifest.catalog.bodies) bodySelect.add(new Option(body.label, body.id));
  bodySelect.value = character.bodyId; bodySelect.disabled = false;
  const animationSelect = document.querySelector('#animation');
  const animationTime = document.querySelector('#animation-time');
  let animationPack = null, animationRequest = 0;
  const animationDefinitions = new Map();
  // The clip pack is per rig and named by the catalog, so a rig whose clips were never converted
  // says so instead of playing another rig's tracks. The lightweight ?basic=1 fixture carries no
  // catalog metadata and keeps the known female pack.
  async function loadAnimationPack() {
    const request = ++animationRequest;
    const note = document.querySelector('#animation-note');
    // The catalog names the pack. Bundles converted before that field existed fall back to the
    // rig-directory convention; the loaded pack's bodyId is checked against the selected rig, so
    // a wrong guess can never display another rig's clips.
    const path = character.body.animationPack || `Animations/${character.body.label}/index.json`;
    animationPack = null; populateAnimations();
    if (animationsDisabled) { note.textContent = 'Animation pack disabled for this reference view.'; return; }
    if (!path) { note.textContent = `No animation clips are converted for the ${character.body.label.toLowerCase()} rig yet.`; return; }
    note.textContent = 'Loading source animations…';
    try {
      const pack = await CharacterAnimations.load('./Models/Characters/' + path);
      if (request !== animationRequest || pack.bodyId !== character.bodyId) return;
      animationPack = pack; populateAnimations(); window.characterViewer.animationReady = true;
      if (pack.clips.some(clip => clip.id === 'idle')) await selectAnimation('idle');
    } catch (error) { if (request === animationRequest) note.textContent = error.message; }
  }
  function populateAnimations() {
    animationDefinitions.clear();
    if (animationPack?.bodyId === character.bodyId)
      for (const descriptor of animationPack.clips) animationDefinitions.set(descriptor.id, { ...descriptor, fromPack: true });
    for (const descriptor of character.body.animations) animationDefinitions.set(descriptor.id, descriptor);
    animationSelect.replaceChildren(new Option('T-pose (default)', 'rest'));
    const groups = new Map();
    for (const descriptor of animationDefinitions.values()) {
      const category = descriptor.category ?? 'Animations';
      if (!groups.has(category)) { const group = document.createElement('optgroup'); group.label = category; groups.set(category, group); animationSelect.append(group); }
      const command = descriptor.mapping?.sourceCommand;
      groups.get(category).append(new Option(command ? `${descriptor.label} (${command})` : descriptor.label, descriptor.id));
    }
    animationSelect.value = animationDefinitions.has(character.animationId) ? character.animationId : 'rest';
    animationSelect.disabled = animationDefinitions.size === 0;
    // Say which rig the clips belong to: only a rig with an imported pack has any.
    if (!animationsDisabled) {
      const label = character.body.label.toLowerCase();
      document.querySelector('#animation-note').textContent = animationDefinitions.size
        ? `${animationDefinitions.size} imported clips for the ${label} rig.`
        : `No animation clips are imported for the ${label} rig yet.`;
    }
    syncAnimationControls();
  }
  function syncAnimationControls() {
    const active = character.animationId !== 'rest';
    for (const id of ['animation-play', 'animation-restart', 'animation-speed', 'animation-time', 'animation-loop', 'animation-in-place'])
      document.getElementById(id).disabled = !active;
    document.querySelector('#animation-play').textContent = character.animationPaused ? 'Play' : 'Pause';
    animationTime.max = Math.max(character.animationDuration, 0.01);
    if (!animationTime.matches(':active')) animationTime.value = character.animationTime;
    document.querySelector('#animation-clock').textContent = `${character.animationTime.toFixed(2)} / ${character.animationDuration.toFixed(2)}s`;
  }
  function selectAnimation(id, keepSettings = false) {
    const request = ++animationRequest;
    const rootBone = character.bones.get('Bip01');
    return runUi(async () => {
      if (id === 'rest') {
        character.resetPose(); animationSelect.value = 'rest'; syncAnimationControls(); updateStatus();
        document.querySelector('#animation-note').textContent = 'Original BASE / T-pose.';
        return true;
      }
      const descriptor = animationDefinitions.get(id);
      if (!descriptor) throw new Error(`Animation is not imported: ${id}`);
      status.textContent = 'Loading source animation…';
      if (!character.animations.has(id)) {
        let clip;
        if (descriptor.fromPack) clip = await animationPack.get(id);
        else {
          const response = await fetch(new URL(descriptor.url, assets.base));
          if (!response.ok) throw new Error(`Animation: HTTP ${response.status}`);
          clip = THREE.AnimationClip.parse(await response.json());
        }
        if (request !== animationRequest || rootBone !== character.bones.get('Bip01')) return false;
        character.registerAnimation(id, clip);
      }
      if (!keepSettings) {
        document.querySelector('#animation-loop').checked = descriptor.loop !== false;
        document.querySelector('#animation-in-place').checked = descriptor.inPlace === true;
      }
      character.setAnimationSpeed(Number(document.querySelector('#animation-speed').value));
      character.playAnimation(id, { ...descriptor,
        loop: document.querySelector('#animation-loop').checked,
        inPlace: document.querySelector('#animation-in-place').checked });
      animationSelect.value = id; syncAnimationControls(); updateStatus();
      document.querySelector('#animation-note').textContent = descriptor.sourceClip ? `Original clip: ${descriptor.sourceClip}` : descriptor.label;
      return true;
    });
  }
  animationSelect.onchange = () => selectAnimation(animationSelect.value);
  document.querySelector('#animation-play').onclick = () => { character.setAnimationPaused(!character.animationPaused); syncAnimationControls(); };
  document.querySelector('#animation-restart').onclick = () => selectAnimation(character.animationId, true);
  document.querySelector('#animation-speed').onchange = event => character.setAnimationSpeed(Number(event.target.value));
  document.querySelector('#animation-loop').onchange = event => character.setAnimationLoop(event.target.checked);
  document.querySelector('#animation-in-place').onchange = event => character.setAnimationInPlace(event.target.checked);
  animationTime.oninput = () => { character.setAnimationPaused(true); character.seekAnimation(Number(animationTime.value)); syncAnimationControls(); };
  bodySelect.onchange = () => runUi(async () => {
    if (library) { if (!await library.setBody(bodySelect.value)) return; }
    else character.setBody(bodySelect.value);
    populateEquipment(); populateAnimations(); rebuildSkeleton(); applyDisplay(); updateStatus(); frame();
    if (library) await library.reconcileQuality();
    await loadAnimationPack();
  });
  document.querySelector('#reset-outfit').disabled = false;
  document.querySelector('#reset-outfit').onclick = () => runUi(async () => {
    if (library) { if (!await library.setBody(character.bodyId)) return; }
    else character.setBody(character.bodyId);
    populateEquipment(); populateAnimations(); rebuildSkeleton(); applyDisplay(); updateStatus();
  });
  const savedSelect = document.querySelector('#saved-outfits');
  const outfitName = document.querySelector('#outfit-name');
  const outfitStatus = document.querySelector('#outfit-status');
  function refreshSavedOutfits(selected = '') {
    try {
      const outfits = loadOutfits(localStorage);
      savedSelect.replaceChildren(new Option('Choose an outfit', ''));
      for (const outfit of outfits) savedSelect.add(new Option(outfit.name, outfit.id));
      savedSelect.value = selected;
      document.querySelector('#apply-outfit').disabled = !savedSelect.value;
      document.querySelector('#delete-outfit').disabled = !savedSelect.value;
    } catch (error) { outfitStatus.textContent = error.message; }
  }
  savedSelect.onchange = () => {
    const outfit = loadOutfits(localStorage).find(outfit => outfit.id === savedSelect.value);
    outfitName.value = outfit?.name ?? '';
    document.querySelector('#apply-outfit').disabled = !outfit;
    document.querySelector('#delete-outfit').disabled = !outfit;
  };
  document.querySelector('#save-outfit').onclick = () => {
    try {
      const outfit = saveOutfit(localStorage, outfitName.value, character);
      saveLastOutfitId(localStorage, outfit.id);
      refreshSavedOutfits(outfit.id); outfitStatus.textContent = `Saved “${outfit.name}” in this browser.`;
    } catch (error) { outfitStatus.textContent = error.message; }
  };
  async function applySavedOutfitById(id, automatic = false) {
    const outfit = loadOutfits(localStorage).find(outfit => outfit.id === id);
    if (!outfit) return false;
    status.textContent = automatic ? 'Restoring last outfit…' : 'Loading saved outfit…';
    if (library) { if (!await library.applyOutfit(outfit)) return false; }
    else character.applyOutfit(outfit);
    bodySelect.value = character.bodyId;
    populateEquipment(); populateAnimations(); rebuildSkeleton(); applyDisplay(); updateStatus();
    if (library) await library.reconcileQuality();
    await loadAnimationPack();
    savedSelect.value = outfit.id; outfitName.value = outfit.name;
    saveLastOutfitId(localStorage, outfit.id);
    outfitStatus.textContent = automatic ? `Restored “${outfit.name}”.` : `Applied “${outfit.name}”.`;
    return true;
  }
  document.querySelector('#apply-outfit').onclick = () => runUi(async () => {
    if (!savedSelect.value) throw new Error('Choose a saved outfit first.');
    await applySavedOutfitById(savedSelect.value);
  });
  document.querySelector('#delete-outfit').onclick = () => {
    try {
      const deletedId = savedSelect.value;
      deleteOutfit(localStorage, deletedId);
      if (loadLastOutfitId(localStorage) === deletedId) saveLastOutfitId(localStorage, '');
      refreshSavedOutfits(); outfitName.value = '';
      outfitStatus.textContent = 'Saved outfit deleted.';
    } catch (error) { outfitStatus.textContent = error.message; }
  };
  outfitName.addEventListener('keydown', event => { if (event.key === 'Enter') document.querySelector('#save-outfit').click(); });
  window.addEventListener('storage', event => { if (event.key === STORAGE_KEY) refreshSavedOutfits(savedSelect.value); });
  refreshSavedOutfits();
  document.querySelector('#front').onclick = () => frame('front');
  document.querySelector('#side').onclick = () => frame('side');
  document.querySelector('#back').onclick = () => frame('back');
  document.querySelector('#frame').onclick = () => frame();
  document.querySelector('#rotate').onchange = event => { controls.autoRotate = event.target.checked; };
  document.querySelector('#skeleton').onchange = event => { skeleton.visible = event.target.checked; };
  document.querySelector('#wireframe').onchange = applyDisplay;
  const resize = () => {
    renderer.setSize(stage.clientWidth, stage.clientHeight);
    camera.aspect = stage.clientWidth / stage.clientHeight; camera.updateProjectionMatrix();
  };
  new ResizeObserver(resize).observe(stage); resize();
  populateEquipment(); populateAnimations(); rebuildSkeleton(); frame(); updateStatus(); updateTextureQualityStatus();
  applyLighting(localStorage.getItem(LIGHTING_KEY) ?? LIGHTING_ENVIRONMENTS[0].id);
  if (!basicOnly) {
    const lastOutfitId = loadLastOutfitId(localStorage);
    if (lastOutfitId) await runUi(() => applySavedOutfitById(lastOutfitId, true));
  }
  // Nothing may stay resident at a level other than the requested one, whatever loaded first.
  if (library) { await library.reconcileQuality(); updateTextureQualityStatus(); }
  const timer = new THREE.Timer();
  renderer.setAnimationLoop(() => {
    timer.update(); const delta = Math.min(timer.getDelta(), 0.1);
    character.update(delta); controls.update(delta); syncAnimationControls(); renderer.render(scene, camera);
    window.characterReady = renderer.info.render.triangles > 0;
    if (previewParts) {
      previewAngle += delta * 0.9;
      const equippedRoots = [...character.equipment.values()]
        .flatMap(item => item.parts.flatMap(part => [part.group, ...(part.extraGroups ?? [])]));
      const previewRoots = previewParts.flatMap(part => [part.group, ...(part.extraGroups ?? [])]);
      const skeletonWasVisible = skeleton.visible, floorWasVisible = floor.visible;
      for (const root of equippedRoots) root.visible = false;
      for (const root of previewRoots) root.visible = true;
      skeleton.visible = false; floor.visible = false;
      previewCamera.position.set(
        previewCenter.x + Math.sin(previewAngle) * previewDistance,
        previewCenter.y + previewDistance * 0.18,
        previewCenter.z + Math.cos(previewAngle) * previewDistance);
      previewCamera.lookAt(previewCenter);
      previewCamera.aspect = 1; previewCamera.updateProjectionMatrix();
      previewRenderer.render(scene, previewCamera);
      for (const root of equippedRoots) root.visible = true;
      for (const root of previewRoots) root.visible = false;
      skeleton.visible = skeletonWasVisible; floor.visible = floorWasVisible;
    }
  });
  window.characterViewer = { character, assets, library, renderer, scene, camera, controls, frame, bounds, selectEquipment, selectAnimation, selectTextureQuality, textureQualityStatus,
    loadedBytes,
    lighting: { environments: LIGHTING_ENVIRONMENTS.map(environment => ({ id: environment.id, label: environment.label })),
      apply: applyLighting, get current() { return lightingSelect.value; } },
    get animationDefinitions() { return [...animationDefinitions.values()]; }, animationReady: false,
    whenIdle: () => Promise.all([...pendingUiActions]) };
  await loadAnimationPack();
} catch (error) {
  status.textContent = error.message; status.classList.add('error'); console.error(error);
}
