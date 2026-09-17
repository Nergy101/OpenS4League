import * as THREE from 'three';
import { scnGeometry, scnMaterials, scnMatrix, disposeScnObject } from './ScnAssets.js';
import { enableExtendedSkinning } from './ExtendedSkinning.js';
import { validateOutfit } from './SavedOutfits.js';

/** A body rig with replaceable equipment. No female-specific assembly rules. */
export class CharacterModel {
  constructor(manifest, buffer, textures, sceneBuffers = new Map()) {
    if (manifest.format !== 's4-character-threejs' || manifest.version !== 1)
      throw new Error('Unsupported character bundle');
    this.manifest = manifest;
    this.buffer = buffer;
    this.sceneBuffers = sceneBuffers;
    this.textures = textures;
    this.scenes = new Map(manifest.scenes.map(scene => [scene.source.toLowerCase(), scene]));
    this.root = new THREE.Group();
    this.root.name = 'S4 character';
    this.root.scale.z = -1;
    this.bones = new Map();
    this.equipment = new Map();
    this.animations = new Map();
    this.animationSpeed = 1; this.activeAction = null;
    this.mixer = new THREE.AnimationMixer(this.root);
    this.setBody(manifest.catalog.defaultBody);
  }

  setBody(id) {
    const body = this.manifest.catalog.bodies.find(body => body.id === id);
    if (!body) throw new Error(`Body type is not imported: ${id}`);
    const source = this.scenes.get(body.skeleton.toLowerCase());
    if (!source) throw new Error(`Missing skeleton: ${body.skeleton}`);
    this.mixer.stopAllAction();
    this.mixer.uncacheRoot(this.root);
    disposeScnObject(this.root);
    this.root.clear(); this.bones.clear(); this.equipment.clear(); this.animations.clear();
    this.motionRoot = new THREE.Group(); this.motionRoot.name = "Animation root offset"; this.root.add(this.motionRoot);
    this.body = body; this.bodyId = id;
    const rig = new THREE.Group();
    rig.name = `${id}:skeleton`;
    rig.matrix.copy(scnMatrix(source.matrix)); rig.matrixAutoUpdate = false;
    this.motionRoot.add(rig);
    const sourceBones = new Map(source.nodes.filter(n => n.type === 'Bone').map(n => [n.name, n]));
    for (const [name, data] of sourceBones) {
      const bone = new THREE.Bone(); bone.name = name;
      const parent = sourceBones.get(data.parent);
      // Unlike clothing nodes, the main rig stores WORLD bind matrices.
      bone.matrix.copy(parent ? scnMatrix(parent.matrix).invert().multiply(scnMatrix(data.matrix)) : scnMatrix(data.matrix));
      this.rememberRest(bone);
      bone.userData.source = data;
      this.bones.set(name, bone);
    }
    for (const [name, data] of sourceBones) (this.bones.get(data.parent) ?? rig).add(this.bones.get(name));
    // The rig's cartridge meshes and hitboxes are not the character's skin.
    rig.userData.helpers = source.nodes.filter(n => n.type !== 'Bone');
    for (const [slot, itemId] of Object.entries(body.defaults)) this.setItem(slot, itemId);
    this.resetPose();
  }

  applyOutfit(outfit) {
    validateOutfit(outfit, this.manifest.catalog);
    const manifest = { ...this.manifest, catalog: { ...this.manifest.catalog, defaultBody: outfit.bodyId } };
    let staged;
    try {
      staged = new CharacterModel(manifest, this.buffer, this.textures, this.sceneBuffers);
      for (const [slot, selection] of Object.entries(outfit.equipment)) staged.setItem(slot, selection.itemId, selection.variantId);
    } catch (error) { staged?.dispose(); throw error; }
    this.mixer.stopAllAction(); this.mixer.uncacheRoot(this.root);
    disposeScnObject(this.root); this.root.clear();
    this.root.add(...staged.root.children.slice());
    this.motionRoot = staged.motionRoot;
    this.body = staged.body; this.bodyId = staged.bodyId;
    this.bones = staged.bones; this.equipment = staged.equipment; this.animations.clear();
    staged.mixer.uncacheRoot(staged.root);
    this.resetPose();
  }

  rememberRest(node) {
    node.matrix.decompose(node.position, node.quaternion, node.scale);
    node.userData.restMatrix = node.matrix.toArray();
    node.matrixAutoUpdate = false;
  }

  setItem(slot, itemId, variantId = 'default') {
    if (!itemId && !(slot in this.body.defaults)) {
      const previous = this.equipment.get(slot);
      if (previous) for (const part of previous.parts) { part.group.removeFromParent(); part.extraGroups?.forEach(group => { group.removeFromParent(); disposeScnObject(group); }); disposeScnObject(part.group); }
      this.equipment.delete(slot); this.applyHiding(); this.root.updateMatrixWorld(true);
      return;
    }
    const item = this.body.items.find(item => item.id === itemId && item.slot === slot);
    if (!item) throw new Error(`Item ${itemId} is not available for ${this.bodyId}/${slot}`);
    const variant = item.variants.find(variant => variant.id === variantId);
    if (!variant) throw new Error(`Unknown texture variant: ${variantId}`);
    // Build before replacing the old selection: invalid assets must not erase it.
    const parts = [];
    try {
      for (const part of item.parts) parts.push(this.buildPart(part, variant.maps, item));
    } catch (error) {
      for (const part of parts) disposeScnObject(part.group);
      throw error;
    }
    const previous = this.equipment.get(slot);
    if (previous) for (const part of previous.parts) { part.group.removeFromParent(); part.extraGroups?.forEach(group => { group.removeFromParent(); disposeScnObject(group); }); disposeScnObject(part.group); }
    for (const part of parts) part.parent.add(part.group);
    this.equipment.set(slot, { item, variant, parts });
    this.applyHiding();
    this.root.updateMatrixWorld(true);
  }

  buildPart(part, replacements, item) {
    const source = this.scenes.get(part.scene.toLowerCase());
    if (!source) throw new Error(`Missing equipment scene: ${part.scene}`);
    const attachmentBone = part.attachmentBone ? this.bones.get(part.attachmentBone) : null;
    if (part.attachmentBone && !attachmentBone) throw new Error(`Missing attachment bone: ${part.attachmentBone}`);
    const parent = attachmentBone ?? this.motionRoot;
    const group = new THREE.Group();
    group.name = part.scene;
    group.matrix.copy(scnMatrix(source.matrix)); group.matrixAutoUpdate = false;
    const localNodes = new Map(), privateBones = new Map(), skin = [];
    const objects = source.nodes.map(data => {
      let object;
      if (data.geometry) {
        const geometry = scnGeometry(data.geometry, this.sceneBuffers.get(source.source.toLowerCase()) ?? this.buffer);
        const materials = scnMaterials(data, this.textures, replacements, true);
        if (data.details.bones.length) {
          object = new THREE.SkinnedMesh(geometry, materials);
          skin.push({ object, data });
        } else object = new THREE.Mesh(geometry, materials);
        object.castShadow = true;
      } else if (data.type === 'Bone' && (part.attachmentBone || !this.bones.has(data.name))) {
        object = new THREE.Bone();
        privateBones.set(data.name, object);
      } else object = new THREE.Group();
      object.name = data.name;
      object.userData.source = data;
      object.matrix.copy(scnMatrix(data.matrix));
      // Attached assets can store scene-space bind matrices, but their authored
      // base animation TRS is parent-local for both bones and rigid meshes.
      // Use that rest pose rather than applying a parent's bind twice. Older
      // assets without a base channel retain their original local matrices.
      const base = attachmentBone && data.details?.animations?.find(animation => animation.Name.toLowerCase() === 'base');
      const key = (base?.TransformKeyData ?? base?.TransformKeyData2)?.TransformKey;
      if (key) object.matrix.compose(
        new THREE.Vector3(key.Translation.X, key.Translation.Y, key.Translation.Z),
        new THREE.Quaternion(key.Rotation.X, key.Rotation.Y, key.Rotation.Z, key.Rotation.W),
        new THREE.Vector3(key.Scale.X, key.Scale.Y, key.Scale.Z));
      this.rememberRest(object);
      // Names are asset-scoped: face and hair each own a Hair_Bone_Dummy.
      if (!localNodes.has(data.name)) localNodes.set(data.name, object);
      return object;
    });
    objects.forEach((object, i) => {
      const name = source.nodes[i].parent;
      const localParent = !name || name.toLowerCase() === source.header.toLowerCase() ? group : localNodes.get(name);
      if (!localParent) throw new Error(`Missing local parent ${name} in ${part.scene}`);
      localParent.add(object);
    });
    for (const { object, data } of skin) {
      const weights = data.details.bones;
      const count = object.geometry.attributes.position.count;
      const indices = new Uint16Array(count * 4), values = new Float32Array(count * 4), influenceCount = new Uint8Array(count);
      const indices2 = new Uint16Array(count * 4), values2 = new Float32Array(count * 4);
      let extended = false;
      weights.forEach((bone, boneIndex) => {
        for (const value of bone.Weight) {
          if (!Number.isInteger(value.Vertex) || value.Vertex < 0 || value.Vertex >= count || !Number.isFinite(value.Weight) || value.Weight < 0)
            throw new Error(`Invalid skin weight in ${data.name}`);
          if (influenceCount[value.Vertex] >= 8) throw new Error(`More than eight skin influences in ${data.name}; refusing to discard weights`);
          const influence = influenceCount[value.Vertex]++;
          const index = value.Vertex * 4 + influence % 4;
          if (influence < 4) { indices[index] = boneIndex; values[index] = value.Weight; }
          else { indices2[index] = boneIndex; values2[index] = value.Weight; extended = true; }
        }
      });
      if (influenceCount.some(count => count === 0)) throw new Error(`Unweighted vertex in ${data.name}`);
      object.geometry.setAttribute('skinIndex', new THREE.Uint16BufferAttribute(indices, 4));
      object.geometry.setAttribute('skinWeight', new THREE.Float32BufferAttribute(values, 4));
      if (extended) {
        object.geometry.setAttribute('skinIndex2', new THREE.Uint16BufferAttribute(indices2, 4));
        object.geometry.setAttribute('skinWeight2', new THREE.Float32BufferAttribute(values2, 4));
        enableExtendedSkinning(object);
      }
      const bones = weights.map(weight => {
        const bone = privateBones.get(weight.Name) ?? this.bones.get(weight.Name);
        if (!bone) throw new Error(`Unresolved skin bone ${weight.Name} in ${data.name}`);
        return bone;
      });
      // Each garment retains its OWN inverse bind matrices. Recomputing them
      // from the common rig would erase authored fit differences.
      const skeleton = new THREE.Skeleton(bones, weights.map(weight => scnMatrix(weight.Matrix)));
      object.bindMode = 'attached';
      object.bind(skeleton, new THREE.Matrix4());
      // Attached mode cancels meshWorld: never bake the SCN model matrix into skin vertices.
      object.frustumCulled = false;
    }
    const extraGroups = [];
    let renderParent = parent;
    if (attachmentBone && skin.length) {
      // Keep static source geometry and private hair bones below the attachment
      // bone, but render skinned vertices at character-root level. The bones
      // already carry the head world transform; keeping the SkinnedMesh below
      // the head would apply that transform twice.
      parent.add(group);
      this.root.updateMatrixWorld(true);
      const inverseGroup = group.matrixWorld.clone().invert();
      const renderGroup = new THREE.Group();
      renderGroup.name = `${part.scene}:render`;
      renderGroup.matrixAutoUpdate = false;
      for (const { object } of skin) {
        const sourceLocal = inverseGroup.clone().multiply(object.matrixWorld);
        object.removeFromParent();
        object.matrix.copy(sourceLocal);
        object.matrixAutoUpdate = false;
        renderGroup.add(object);
      }
      renderParent = this.motionRoot;
      extraGroups.push(group);
      return { group: renderGroup, parent: renderParent, extraGroups };
    }
    return { group, parent: renderParent, extraGroups };
  }

  applyHiding() {
    const hides = new Set([...this.equipment.values()].flatMap(selection => selection.item.hides).map(name => name.toLowerCase()));
    for (const selection of this.equipment.values()) for (const part of selection.parts) {
      part.group.traverse(node => {
        if (!node.isMesh) return;
        const name = node.name.toLowerCase();
        const tokens = name.split(/[\s,]+/);
        const slot = selection.item.slot;
        const partNumber = name.match(/(?:^|_)hide_parts?_?([1-4])(?:_|\s|$)/)?.[1];
        const semantic = ((slot === 'hair' || slot === 'face') && hides.has(`${slot}_all`))
          || ((slot === 'gloves' || slot === 'shoes') && partNumber && hides.has(`${slot}_part${partNumber}`));
        node.visible = !semantic && !hides.has(name) && !tokens.some(token => hides.has(token));
      });
    }
  }

  resetPose() {
    this.mixer.stopAllAction();
    this.root.traverse(node => {
      if (!node.userData.restMatrix) return;
      node.matrix.fromArray(node.userData.restMatrix);
      node.matrix.decompose(node.position, node.quaternion, node.scale);
      node.matrixAutoUpdate = false;
    });
    this.motionRoot.position.set(0, 0, 0);
    this.root.updateMatrixWorld(true);
    this.animationId = 'rest';
    this.activeAction = null; this.rootMotionBone = null; this.rootMotionOrigin = null;
  }

  registerAnimation(id, clip) {
    if (!(clip instanceof THREE.AnimationClip) || id === 'rest') throw new Error('Expected a named Three.js AnimationClip');
    // Mixer actions are keyed by clip.uuid, not our catalog ID. Imported JSON
    // may omit/reuse UUIDs; give each registered clip its own runtime identity.
    const previous = this.animations.get(id);
    if (previous) this.mixer.uncacheClip(previous);
    this.animations.set(id, clip.clone());
  }

  playAnimation(id, options = {}) {
    if (id === 'rest') { this.resetPose(); return; }
    const clip = this.animations.get(id);
    if (!clip) throw new Error(`Animation is not imported: ${id}`);
    this.resetPose();
    this.root.traverse(node => { if (node.isBone) node.matrixAutoUpdate = true; });
    this.activeAction = this.mixer.clipAction(clip).reset();
    this.activeAction.clampWhenFinished = true;
    this.activeAction.setLoop(options.loop === false ? THREE.LoopOnce : THREE.LoopRepeat, options.loop === false ? 1 : Infinity);
    this.activeAction.play(); this.animationId = id;
    this.mixer.timeScale = this.animationSpeed;
    this.mixer.update(0);
    this.rootMotionBone = this.bones.get(options.rootBone ?? 'Bip01');
    this.rootMotionOrigin = this.rootMotionBone?.position.clone();
    this.animationInPlace = options.inPlace === true;
    this.applyRootMotion(); this.root.updateMatrixWorld(true);
  }

  get animationTime() { return this.activeAction?.time ?? 0; }
  get animationDuration() { return this.activeAction?.getClip().duration ?? 0; }
  get animationPaused() { return this.activeAction?.paused ?? true; }

  setAnimationPaused(paused) {
    if (!this.activeAction) return;
    if (!paused && this.activeAction.time >= this.animationDuration) this.activeAction.reset().play();
    this.activeAction.paused = paused;
  }

  setAnimationSpeed(speed) {
    if (!Number.isFinite(speed) || speed <= 0 || speed > 10) throw new Error('Animation speed must be greater than zero and at most 10.');
    this.animationSpeed = speed; this.mixer.timeScale = speed;
  }

  setAnimationLoop(loop) {
    this.activeAction?.setLoop(loop ? THREE.LoopRepeat : THREE.LoopOnce, loop ? Infinity : 1);
  }

  setAnimationInPlace(enabled) {
    this.animationInPlace = enabled;
    this.mixer.update(0); this.applyRootMotion(); this.root.updateMatrixWorld(true);
  }

  seekAnimation(seconds) {
    if (!Number.isFinite(seconds)) throw new Error('Animation time must be finite.');
    if (!this.activeAction) return;
    this.activeAction.enabled = true;
    this.activeAction.time = Math.max(0, Math.min(this.animationDuration, seconds));
    this.mixer.update(0); this.applyRootMotion(); this.root.updateMatrixWorld(true);
  }

  updateAttachments() {
    this.root.updateMatrixWorld(true);
    const inverseMotionRoot = this.motionRoot.matrixWorld.clone().invert();
    for (const selection of this.equipment.values()) for (const part of selection.parts) {
      const attachment = part.attachment;
      if (!attachment) continue;
      const delta = attachment.bone.matrixWorld.clone().multiply(attachment.restWorld.clone().invert());
      attachment.root.matrix.copy(inverseMotionRoot.clone().multiply(delta).multiply(this.motionRoot.matrixWorld));
      attachment.root.matrixAutoUpdate = false;
    }
  }

  applyRootMotion() {
    if (!this.animationInPlace || !this.rootMotionBone || !this.rootMotionOrigin) {
      this.motionRoot.position.set(0, 0, 0); return;
    }
    // Offset the whole actor, not the tracked bone. Mutating animated properties
    // would conflict with AnimationMixer's cached values during pause/scrubbing.
    this.motionRoot.position.set(this.rootMotionOrigin.x - this.rootMotionBone.position.x, 0,
      this.rootMotionOrigin.z - this.rootMotionBone.position.z);
  }

  update(deltaSeconds) {
    this.mixer.update(deltaSeconds); this.applyRootMotion();
    this.updateAttachments();
    this.root.updateMatrixWorld(true);
  }

  dispose() {
    this.mixer.stopAllAction(); this.mixer.uncacheRoot(this.root);
    this.root.removeFromParent(); disposeScnObject(this.root); this.root.clear();
    this.equipment.clear(); this.bones.clear(); this.animations.clear();
  }
}
