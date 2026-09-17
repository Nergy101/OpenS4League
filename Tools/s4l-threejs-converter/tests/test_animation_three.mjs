// Native THREE clips must bind names AND have distinct mixer cache identities.
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { fileURLToPath, pathToFileURL } from 'node:url';
import path from 'node:path';
const repo = fileURLToPath(new URL('../../../', import.meta.url));
const THREE = await import(pathToFileURL(path.join(repo, 'Client/node_modules/three/build/three.module.js')));
const directory = process.argv[2] || path.join(repo, 'Client/Models/Characters/Animations/Female');
const index = JSON.parse(await readFile(path.join(directory, 'index.json'), 'utf8'));
const warnings = [];
const originalWarn = console.warn, originalError = console.error;
console.warn = (...a) => warnings.push(a.join(' '));
console.error = (...a) => warnings.push(a.join(' '));
let channels = 0, samples = 0, maxError = 0;
const actor = new THREE.Group(), bones = new Map();
const mixer = new THREE.AnimationMixer(actor), uuids = new Set(), parsedClips = [];
for (const meta of index.clips) {
  const json = JSON.parse(await readFile(path.join(directory, meta.url), 'utf8'));
  assert.match(json.uuid || '', /^[0-9a-f]{8}-[0-9a-f]{4}-8[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/, 'A deterministic clip UUID is mandatory for AnimationMixer caching');
  assert.ok(!uuids.has(json.uuid), 'Animation clip UUIDs must be unique'); uuids.add(json.uuid);
  const clip = THREE.AnimationClip.parse(json);
  assert.equal(clip.uuid, json.uuid); parsedClips.push(clip);
  assert.ok(clip.validate(), meta.id);
  for (const track of clip.tracks) {
    const parsed = THREE.PropertyBinding.parseTrackName(track.name);
    assert.equal(parsed.nodeName, track.name.slice(0, track.name.lastIndexOf('.')));
    if (!bones.has(parsed.nodeName)) {
      const bone = new THREE.Bone(); bone.name = parsed.nodeName;
      bones.set(bone.name, bone); actor.add(bone);
    }
  }
  assert.equal(bones.size, 82);
  mixer.stopAllAction();
  const action = mixer.clipAction(clip); action.setLoop(THREE.LoopOnce, 1); action.clampWhenFinished = true;
  assert.equal(action.getClip(), clip, 'Shared mixer returned another clip action');
  for (const time of [0, clip.duration * .25, clip.duration * .5, clip.duration * .9, clip.duration]) {
    action.reset().play(); mixer.setTime(time);
    for (const track of clip.tracks) {
      const binding = THREE.PropertyBinding.parseTrackName(track.name);
      const actual = bones.get(binding.nodeName)[binding.propertyName].toArray();
      const expected = track.createInterpolant().evaluate(time);
      for (let i = 0; i < actual.length; i++) {
        const error = Math.abs(actual[i] - expected[i]); maxError = Math.max(error, maxError);
        assert.ok(error < 1e-4, `${meta.id}/${track.name}@${time}: ${actual} != ${[...expected]}`);
      }
      samples++;
    }
  }
  mixer.stopAllAction();
  channels += clip.tracks.length;
}
for (const clip of [parsedClips[0], parsedClips[1], parsedClips[0]]) {
  mixer.stopAllAction(); const action = mixer.clipAction(clip).reset().play();
  mixer.setTime(clip.duration / 2);
  assert.equal(action.getClip(), clip, 'A -> B -> A reused the wrong cached clip');
}
mixer.stopAllAction(); mixer.uncacheRoot(actor);
console.warn = originalWarn; console.error = originalError;
assert.deepEqual(warnings, [], 'PropertyBinding/mixer emitted warnings');
assert.equal(channels, index.verification.tracks);
console.log(JSON.stringify({ verified: true, three: THREE.REVISION, clips: index.clips.length,
  channels, samples, maxError, exactBoneNamesWithSpaces: true, uniqueClipUuids: uuids.size,
  sharedMixerSwitching: true, warnings: warnings.length }));
