import * as THREE from 'three';

// Some original outfits use five positive influences. Retain them exactly with
// a second vec4 rather than pruning or normalizing the source weights.
function extendShader(shader) {
  const declarations = `
#ifdef USE_SKINNING
  attribute vec4 skinIndex2;
  attribute vec4 skinWeight2;
#endif`;
  const matrices = `
  mat4 boneMatX2 = getBoneMatrix( skinIndex2.x );
  mat4 boneMatY2 = getBoneMatrix( skinIndex2.y );
  mat4 boneMatZ2 = getBoneMatrix( skinIndex2.z );
  mat4 boneMatW2 = getBoneMatrix( skinIndex2.w );`;
  const positions = `
  skinned += boneMatX2 * skinVertex * skinWeight2.x;
  skinned += boneMatY2 * skinVertex * skinWeight2.y;
  skinned += boneMatZ2 * skinVertex * skinWeight2.z;
  skinned += boneMatW2 * skinVertex * skinWeight2.w;`;
  const normals = `
  skinMatrix += skinWeight2.x * boneMatX2;
  skinMatrix += skinWeight2.y * boneMatY2;
  skinMatrix += skinWeight2.z * boneMatZ2;
  skinMatrix += skinWeight2.w * boneMatW2;`;
  shader.vertexShader = shader.vertexShader
    .replace('#include <skinning_pars_vertex>', '#include <skinning_pars_vertex>' + declarations)
    .replace('#include <skinbase_vertex>', THREE.ShaderChunk.skinbase_vertex.replace('#endif', matrices + '\n#endif'))
    .replace('#include <skinning_vertex>', THREE.ShaderChunk.skinning_vertex.replace('transformed =', positions + '\n  transformed ='))
    .replace('#include <skinnormal_vertex>', THREE.ShaderChunk.skinnormal_vertex.replace('skinMatrix = bindMatrixInverse', normals + '\n  skinMatrix = bindMatrixInverse'));
}

const base = new THREE.Vector4(), point = new THREE.Vector4(), matrix = new THREE.Matrix4();
function applyEightBoneTransform(index, target) {
  if (target.isVector4) { base.copy(target); target.set(0, 0, 0, 0); }
  else { base.set(target.x, target.y, target.z, 1); target.set(0, 0, 0); }
  base.applyMatrix4(this.bindMatrix);
  for (const suffix of ['', '2']) {
    const indices = this.geometry.attributes['skinIndex' + suffix];
    const weights = this.geometry.attributes['skinWeight' + suffix];
    for (let i = 0; i < 4; i++) {
      const weight = weights.array[index * 4 + i];
      if (!weight) continue;
      const bone = indices.array[index * 4 + i];
      matrix.multiplyMatrices(this.skeleton.bones[bone].matrixWorld, this.skeleton.boneInverses[bone]);
      target.addScaledVector(point.copy(base).applyMatrix4(matrix), weight);
    }
  }
  if (target.isVector4) target.w = base.w;
  return target.applyMatrix4(this.bindMatrixInverse);
}

/** Reapply after ObjectLoader imports a mesh marked userData.skinInfluences=8. */
export function enableExtendedSkinning(mesh) {
  mesh.applyBoneTransform = applyEightBoneTransform;
  mesh.userData.skinInfluences = 8;
  mesh.customDepthMaterial = new THREE.MeshDepthMaterial({ depthPacking: THREE.RGBADepthPacking });
  mesh.customDistanceMaterial = new THREE.MeshDistanceMaterial();
  for (const material of [...(Array.isArray(mesh.material) ? mesh.material : [mesh.material]), mesh.customDepthMaterial, mesh.customDistanceMaterial]) {
    material.onBeforeCompile = extendShader;
    material.customProgramCacheKey = () => 'opens4l-eight-influences-v1';
    material.needsUpdate = true;
  }
}
