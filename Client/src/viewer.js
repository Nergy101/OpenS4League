import * as THREE from 'three';
import { PointerLockControls } from 'three/addons/controls/PointerLockControls.js';
import { loadStation2 } from './Station2Loader.js';

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
  const map = await loadStation2();
  scene.add(map.root);
  const configResponse = await fetch('./Models/Maps/Station-2/map-config.json');
  if (!configResponse.ok) throw new Error(`Map configuration: HTTP ${configResponse.status}`);
  const config = await configResponse.json();
  const r = config.RENDERER;
  const fogColor = new THREE.Color().setRGB(Number(r.FogColorR), Number(r.FogColorG), Number(r.FogColorB), THREE.SRGBColorSpace);
  const fog = new THREE.Fog(fogColor, Number(r.FogMinDist), Number(r.FogMaxDist));
  scene.fog = fog;
  scene.background = fogColor;
  function pose(position, target) {
    fly.unlock();
    camera.position.set(position[0], position[1], -position[2]);
    camera.lookAt(target[0], target[1], -target[2]);
  }
  function overview() {
    pose([12500, 11500, -15500], [0, 1700, 0]);
    document.querySelector('#fog').checked = false;
    scene.fog = null;
  }
  function ingame() {
    const e = config.ENVIRONMENT;
    pose(['X', 'Y', 'Z'].map(a => Number(e['InGameCameraPos' + a])), ['X', 'Y', 'Z'].map(a => Number(e['InGameCameraLookAt' + a])));
    document.querySelector('#fog').checked = true;
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
  document.querySelector('#wireframe').onchange = e => map.root.traverse(node => {
    if (node.isMesh) for (const material of Array.isArray(node.material) ? node.material : [node.material]) material.wireframe = e.target.checked;
  });
  const keys = new Set();
  window.addEventListener('keydown', e => { if (fly.isLocked) { keys.add(e.code); e.preventDefault(); } });
  window.addEventListener('keyup', e => keys.delete(e.code));
  window.addEventListener('blur', () => keys.clear());
  fly.addEventListener('lock', () => { document.querySelector('#crosshair').style.display = 'block'; document.activeElement?.blur(); });
  fly.addEventListener('unlock', () => { keys.clear(); document.querySelector('#crosshair').style.display = 'none'; });
  window.addEventListener('resize', () => {
    camera.aspect = innerWidth / innerHeight; camera.updateProjectionMatrix(); renderer.setSize(innerWidth, innerHeight);
  });
  cameraSelect.value = 'BROADCASTINGCAMERA_06';
  cameraSelect.onchange();
  const total = map.manifest.totals;
  status.textContent = `${total.scenes} SCNs · ${total.models} meshes · ${total.triangles.toLocaleString('en-US')} triangles · ${Object.keys(map.manifest.textures).length} textures`;
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
    window.station2Ready = renderer.info.render.triangles > 0;
  });
  // Inspectable integration surface for smoke tests and importing the actual model.
  window.station2 = { ...map, scene, camera, renderer, fly, overview, ingame };
} catch (error) {
  status.textContent = error.message;
  status.classList.add('error');
  console.error(error);
}
