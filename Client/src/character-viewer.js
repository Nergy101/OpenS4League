import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { loadScnAssets } from './ScnAssets.js';
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
  scene.add(new THREE.HemisphereLight(0xe1edff, 0x666778, 1.8));
  const key = new THREE.DirectionalLight(0xfff1de, 2.2);
  key.position.set(-250, 450, 400); key.castShadow = true;
  key.shadow.mapSize.set(2048, 2048);
  Object.assign(key.shadow.camera, { left: -220, right: 220, top: 350, bottom: -150, near: 1, far: 1200 });
  key.shadow.bias = -0.0002;
  scene.add(key);
  const rim = new THREE.DirectionalLight(0xa8caff, 1.4);
  rim.position.set(300, 280, -250); scene.add(rim);
  const basicOnly = new URLSearchParams(location.search).has('basic');
  const library = basicOnly ? null : await WardrobeLibrary.load('./Models/Characters/Wardrobe/index.json', { maxTextureSize: renderer.capabilities.maxTextureSize });
  const assets = library
    ? { manifest: library.runtimeManifest, textures: library.textures, base: library.base }
    : await loadScnAssets('./Models/Characters/BasicFemale/character.json', 's4-character-threejs');
  const character = library ? await library.createModel() : new CharacterModel(assets.manifest, assets.buffer, assets.textures);
  const TEXTURE_QUALITY_KEY = 'opens4l.texture-quality.v1';
  const textureQuality = document.querySelector('#texture-quality');
  const textureQualityStatus = document.querySelector('#texture-quality-status');
  const availableTextureQualities = new Set(['1x']);
  for (const descriptor of Object.values(assets.manifest.textures ?? {}))
    for (const quality of Object.keys(descriptor.variants ?? {})) availableTextureQualities.add(quality);
  for (const option of textureQuality.options) option.disabled = !availableTextureQualities.has(option.value);
  const savedTextureQuality = localStorage.getItem(TEXTURE_QUALITY_KEY);
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
      return true;
    });
  }
  scene.add(character.root);
  const floor = new THREE.Mesh(new THREE.PlaneGeometry(1100, 1100), new THREE.ShadowMaterial({ opacity: 0.23 }));
  floor.rotation.x = -Math.PI / 2; floor.receiveShadow = true; scene.add(floor);
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
  function updateStatus() {
    let meshes = 0, vertices = 0;
    character.root.traverse(node => { if (node.isMesh) { meshes++; vertices += node.geometry.attributes.position.count; } });
    status.classList.remove('error');
    status.textContent = `${character.body.label} · ${character.equipment.size} equipment slots · ${meshes} meshes · ${vertices.toLocaleString('en-US')} vertices`;
    const unavailable = library?.index.inventory.filter(item => item.status === 'unavailable') ?? [];
    document.querySelector('#wardrobe-count').textContent = library
      ? `${character.body.items.length} imported items · ${unavailable.length} unavailable · ${library.textures.size} textures loaded`
      : 'Basic outfit only · open the full wardrobe for more equipment';
  }
  function populateEquipment() {
    const container = document.querySelector('#equipment'); container.replaceChildren();
    const query = document.querySelector('#equipment-search').value.trim().toLowerCase();
    const matches = item => `${item.id} ${item.label}`.toLowerCase().includes(query);
    const slots = ['hair', 'face', 'shirt', 'pants', 'gloves', 'shoes', 'accessory'].filter(slot => character.body.items.some(item => item.slot === slot));
    for (const slot of slots) {
      const row = document.createElement('div'); row.className = 'slot';
      const selection = character.equipment.get(slot);
      const items = character.body.items.filter(item => item.slot === slot && (matches(item) || item.id === selection?.item.id))
        .sort((a, b) => a.label.localeCompare(b.label, 'en') || a.id.localeCompare(b.id));
      const label = document.createElement('label'); label.htmlFor = `slot-${slot}`;
      label.textContent = `${slot.charAt(0).toUpperCase() + slot.slice(1)} (${items.filter(matches).length})`;
      const itemSelect = document.createElement('select'); itemSelect.id = `slot-${slot}`; itemSelect.dataset.slot = slot;
      if (!(slot in character.body.defaults)) itemSelect.add(new Option('None', ''));
      for (const item of items) itemSelect.add(new Option(`${item.label} · ${item.id}${matches(item) ? '' : ' (selected)'}`, item.id));
      itemSelect.value = selection?.item.id ?? '';
      itemSelect.onchange = () => selectEquipment(slot, itemSelect.value);
      const variantRow = document.createElement('div'); variantRow.className = 'variant'; variantRow.hidden = !selection;
      const variantLabel = document.createElement('label'); variantLabel.htmlFor = `variant-${slot}`; variantLabel.textContent = 'Skin';
      const variantSelect = document.createElement('select'); variantSelect.id = `variant-${slot}`; variantSelect.dataset.variant = slot;
      if (selection) {
        for (const variant of selection.item.variants) variantSelect.add(new Option(variant.label, variant.id));
        variantSelect.value = selection.variant.id; variantSelect.disabled = selection.item.variants.length === 1;
      }
      variantSelect.onchange = () => selectEquipment(slot, itemSelect.value, variantSelect.value);
      variantRow.append(variantLabel, variantSelect); row.append(label, itemSelect, variantRow); container.append(row);
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
  if (!basicOnly) {
    const lastOutfitId = loadLastOutfitId(localStorage);
    if (lastOutfitId) await runUi(() => applySavedOutfitById(lastOutfitId, true));
  }
  const timer = new THREE.Timer();
  renderer.setAnimationLoop(() => {
    timer.update(); const delta = Math.min(timer.getDelta(), 0.1);
    character.update(delta); controls.update(delta); syncAnimationControls(); renderer.render(scene, camera);
    window.characterReady = renderer.info.render.triangles > 0;
  });
  window.characterViewer = { character, assets, library, renderer, scene, camera, controls, frame, bounds, selectEquipment, selectAnimation, selectTextureQuality, textureQualityStatus,
    get animationDefinitions() { return [...animationDefinitions.values()]; }, animationReady: false,
    whenIdle: () => Promise.all([...pendingUiActions]) };
  if (!new URLSearchParams(location.search).has('noAnimations')) {
    CharacterAnimations.load('./Models/Characters/Animations/Female/index.json').then(async pack => {
      animationPack = pack; populateAnimations(); window.characterViewer.animationReady = true;
      document.querySelector('#animation-note').textContent = `${pack.clips.length} source animations. Standing idle is the default.`;
      if (pack.clips.some(clip => clip.id === 'idle')) await selectAnimation('idle');
    }).catch(error => { document.querySelector('#animation-note').textContent = error.message; });
  } else document.querySelector('#animation-note').textContent = 'Animation pack disabled for this reference view.';
} catch (error) {
  status.textContent = error.message; status.classList.add('error'); console.error(error);
}
