import * as THREE from 'three';

/** A phone or tablet (touch first): lighter settings there save battery and heat. */
export const IS_PHONE = typeof matchMedia === 'function' && matchMedia('(pointer: coarse)').matches;

/**
 * Fewer draw calls: every static mesh under `root` (that never moves on its own) is baked into world space
 * and merged with the others that share its material look and shadow settings, so the chairs, tables, glasses
 * and wall decorations draw in a few dozen calls. `root` must not move afterwards.
 */
export function mergeStaticWorld(root: THREE.Object3D) {
  mergeRigid(root, [root]);
  // what is left never moves either: skip its per-frame matrix work
  root.traverse(o => { if (!o.userData.dynamic) { o.updateMatrix(); o.matrixAutoUpdate = false; } });
}

/**
 * The same for something with moving parts (a character): `bones` are the parts that move (root, head, arms,
 * ...). Each mesh is merged into the nearest bone above it, with the others of the same look, so a person is
 * a handful of draw calls (one per bone and material) instead of dozens.
 */
export function mergeRigid(root: THREE.Object3D, bones: THREE.Object3D[]) {
  if ((new THREE.BufferGeometry() as unknown as { isBufferGeometry?: unknown }).isBufferGeometry !== true) return;   // test stub
  root.updateMatrixWorld(true);
  const boneSet = new Set(bones); boneSet.add(root);
  // many parts make their own copy of the same material (every chair its own wood): one per distinct look
  const looks = new Map<string, THREE.Material>();
  const sameLook = (mat: THREE.Material) => {
    const a = mat as THREE.MeshStandardMaterial & { [k: string]: unknown };
    const sig = [a.type, a.color?.getHex(), a.emissive?.getHex(), a.map?.uuid, a.roughness, a.metalness, a.transparent, a.opacity, a.side, a.depthWrite,
      a.vertexColors, a.flatShading, a.normalMap?.uuid, a.roughnessMap?.uuid, a.envMap?.uuid, a.envMapIntensity, a.alphaMap?.uuid, a.emissiveMap?.uuid, a.emissiveIntensity,
      (a as unknown as { clearcoat?: number }).clearcoat, (a as unknown as { sheen?: number }).sheen].join('|');
    let m = looks.get(sig);
    if (!m) looks.set(sig, m = mat);
    return m;
  };
  const groups = new Map<string, { bone: THREE.Object3D; mat: THREE.Material; cast: boolean; recv: boolean; meshes: THREE.Mesh[] }>();
  root.traverse(o => {
    const m = o as THREE.Mesh;
    if (!m.isMesh || boneSet.has(m) || Array.isArray(m.material) || (m as unknown as { isInstancedMesh?: boolean }).isInstancedMesh || m.userData.dynamic || !m.visible || m.renderOrder) return;
    const g = m.geometry, a = g.attributes;
    // only plain geometry: position, normal and uv (others keep their own draw call)
    if (!a.position || !a.normal || !a.uv || Object.keys(a).length !== 3 || g.morphAttributes.position) return;
    // the nearest moving part above; nothing between it and the mesh may be marked as moving
    let bone: THREE.Object3D | null = m.parent;
    while (bone && !boneSet.has(bone)) { if (bone.userData.dynamic) return; bone = bone.parent; }
    if (!bone) return;
    const mat = sameLook(m.material as THREE.Material);
    const key = `${bone.uuid}|${mat.uuid}|${m.castShadow ? 1 : 0}${m.receiveShadow ? 1 : 0}|${g.index ? 'i' : 'n'}`;
    let e = groups.get(key);
    if (!e) groups.set(key, e = { bone, mat, cast: m.castShadow, recv: m.receiveShadow, meshes: [] });
    e.meshes.push(m);
  });
  const mtx = new THREE.Matrix4(), inv = new THREE.Matrix4();
  for (const e of groups.values()) {
    for (const m of e.meshes) m.material = e.mat;   // (built just now, not on the GPU yet: nothing to free)
    if (e.meshes.length < 2) continue;
    inv.copy(e.bone.matrixWorld).invert();
    const parts = e.meshes.map(m => { mtx.multiplyMatrices(inv, m.matrixWorld); return m.geometry.clone().applyMatrix4(mtx); });
    const indexed = !!parts[0].index;
    const total = parts.reduce((n, g) => n + g.attributes.position.count, 0);
    const pos = new Float32Array(total * 3), nor = new Float32Array(total * 3), uv = new Float32Array(total * 2);
    const idx: number[] = [];
    let o = 0;
    for (const g of parts) {
      const n = g.attributes.position.count;
      pos.set(g.attributes.position.array as Float32Array, o * 3);
      nor.set(g.attributes.normal.array as Float32Array, o * 3);
      uv.set(g.attributes.uv.array as Float32Array, o * 2);
      if (indexed) { const ix = g.index!.array; for (let k = 0; k < ix.length; k++) idx.push(ix[k] + o); }
      o += n; g.dispose();
    }
    const geo = new THREE.BufferGeometry();
    geo.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
    geo.setAttribute('normal', new THREE.Float32BufferAttribute(nor, 3));
    geo.setAttribute('uv', new THREE.Float32BufferAttribute(uv, 2));
    if (indexed) geo.setIndex(total > 65535 ? new THREE.Uint32BufferAttribute(idx, 1) : new THREE.Uint16BufferAttribute(idx, 1));
    geo.computeBoundingSphere();
    const merged = new THREE.Mesh(geo, e.mat);
    merged.castShadow = e.cast; merged.receiveShadow = e.recv;
    merged.matrixAutoUpdate = false;
    for (const m of e.meshes) { m.parent?.remove(m); m.geometry.dispose(); }
    e.bone.add(merged);
  }
  // groups left empty by the merge go too (fewer nodes to walk every frame)
  const empty: THREE.Object3D[] = [];
  root.traverse(o => { if (o !== root && !boneSet.has(o) && !o.children.length && !(o as THREE.Mesh).isMesh && o.type === 'Group') empty.push(o); });
  for (const o of empty) o.parent?.remove(o);
}

/** Free the GPU memory of everything under `root` (geometries, materials and their textures). */
export function disposeTree(root: THREE.Object3D, textures = true) {
  root.traverse(o => {
    const m = o as THREE.Mesh;
    if (!m.isMesh) return;
    m.geometry?.dispose();
    for (const mat of Array.isArray(m.material) ? m.material : [m.material]) {
      if (!mat) continue;
      if (textures) for (const v of Object.values(mat)) if ((v as THREE.Texture)?.isTexture) (v as THREE.Texture).dispose();
      mat.dispose();
    }
  });
}

/** Empty a group of one-off meshes (highlights, rings) and free them. */
export function clearDisposing(g: THREE.Object3D) {
  disposeTree(g, false);
  g.clear();
}
