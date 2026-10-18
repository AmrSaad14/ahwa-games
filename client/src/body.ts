import * as THREE from 'three';

/*
 * One smooth, skinned body per character instead of tubes and balls stuck together.
 *
 * The body is described as a few dozen soft shapes (tapered capsules and ellipsoids for the hips, waist, chest,
 * shoulders, neck, arms and legs) blended into each other like clay (a smooth signed-distance union), so the
 * shoulders melt into the chest, the elbows and knees are rounded and there are no seams. The surface is
 * extracted with "surface nets" and bound to the character's joints (hips, shoulders, elbows, knees) with
 * soft skin weights, so it bends like a body when the arms and legs move.
 *
 * Clothes are shells of the same shapes, a little bigger and cut where the garment ends (short or long
 * sleeves, the jacket open over the shirt), so every garment fits the body it is worn on.
 */

type V = THREE.Vector3;
export interface Prim { a: V; b: V; ra: number; rb: number; bone: number; zs?: number; cut?: (p: V) => number }

/** Precomputed numbers for a primitive, so the distance needs no allocations. */
interface Fast { ax: number; ay: number; az: number; bx: number; by: number; bz: number; ra: number; dr: number; il2: number; izs: number; zk: number; cx: number; cy: number; cz: number; br: number; cut?: (p: V) => number }
const fastCache = new WeakMap<Prim, Fast>();
function fast(P: Prim): Fast {
  let F = fastCache.get(P);
  if (!F) {
    const bx = P.b.x - P.a.x, by = P.b.y - P.a.y, bz = P.b.z - P.a.z, L2 = bx * bx + by * by + bz * bz, zs = P.zs ?? 1;
    F = { ax: P.a.x, ay: P.a.y, az: P.a.z, bx, by, bz, ra: P.ra, dr: P.rb - P.ra, il2: L2 > 1e-9 ? 1 / L2 : 0, izs: 1 / zs, zk: Math.min(1, zs),
      cx: P.a.x + bx / 2, cy: P.a.y + by / 2, cz: P.a.z + bz / 2, br: Math.sqrt(L2) / 2 + Math.max(P.ra, P.rb) / Math.min(1, zs), cut: P.cut };
    fastCache.set(P, F);
  }
  return F;
}
/** Signed distance to a tapered capsule (z squashed by zs around the segment, for flat chests and hips). */
function primDist(P: Prim, p: V, useCut = true) {
  const F = fast(P);
  const px = p.x - F.ax, py = p.y - F.ay, pz = p.z - F.az;
  let t = (px * F.bx + py * F.by + pz * F.bz) * F.il2; t = t < 0 ? 0 : t > 1 ? 1 : t;
  const dx = px - F.bx * t, dy = py - F.by * t, dz = (pz - F.bz * t) * F.izs;
  let d = (Math.sqrt(dx * dx + dy * dy + dz * dz) - (F.ra + F.dr * t)) * F.zk;
  if (useCut && F.cut) d = Math.max(d, F.cut(p));
  return d;
}
/** Smooth union: shapes flow into each other within k. */
function smin(a: number, b: number, k: number) { const h = Math.max(k - Math.abs(a - b), 0) / k; return Math.min(a, b) - h * h * k * 0.25; }
function field(prims: Prim[], p: V, k: number, grow: number) {
  let d = 1e9;
  for (const P of prims) {
    const F = fast(P), ex = p.x - F.cx, ey = p.y - F.cy, ez = p.z - F.cz;
    // too far to change anything (bounding sphere): skip
    if (Math.sqrt(ex * ex + ey * ey + ez * ez) - F.br > d + k) continue;
    d = smin(d, primDist(P, p), k);
  }
  return d - grow;
}

export interface Shell { prims: Prim[]; k: number; grow: number; material: THREE.Material; clip?: (p: V) => number }

/** Surface nets over a grid: a smooth, closed surface with roughly even triangles. */
function extract(sh: Shell, h: number) {
  const f = (p: V) => { const d = field(sh.prims, p, sh.k, sh.grow); return sh.clip ? Math.max(d, sh.clip(p)) : d; };
  // bounds of everything that can be inside
  const lo = new THREE.Vector3(1e9, 1e9, 1e9), hi = new THREE.Vector3(-1e9, -1e9, -1e9);
  for (const P of sh.prims) {
    const r = Math.max(P.ra, P.rb) / Math.min(1, P.zs ?? 1) + sh.grow + sh.k + h * 2;
    for (const e of [P.a, P.b]) { lo.min(_q.set(e.x - r, e.y - r, e.z - r)); hi.max(_q.set(e.x + r, e.y + r, e.z + r)); }
  }
  const nx = Math.ceil((hi.x - lo.x) / h) + 1, ny = Math.ceil((hi.y - lo.y) / h) + 1, nz = Math.ceil((hi.z - lo.z) / h) + 1;
  const val = new Float32Array(nx * ny * nz);
  const idx = (i: number, j: number, k: number) => i + nx * (j + ny * k);
  const p = new THREE.Vector3();
  // coarse pass first: points far from the surface are not evaluated exactly
  const C = 4, cx = Math.ceil(nx / C) + 1, cy = Math.ceil(ny / C) + 1, cz = Math.ceil(nz / C) + 1;
  const coarse = new Float32Array(cx * cy * cz);
  for (let k = 0; k < cz; k++) for (let j = 0; j < cy; j++) for (let i = 0; i < cx; i++) coarse[i + cx * (j + cy * k)] = f(p.set(lo.x + i * C * h, lo.y + j * C * h, lo.z + k * C * h));
  const far = C * h * 0.95;   // (a coarse point is at most 0.87·C·h from any fine point it stands for)
  for (let k = 0; k < nz; k++) for (let j = 0; j < ny; j++) for (let i = 0; i < nx; i++) {
    const c = coarse[Math.round(i / C) + cx * (Math.round(j / C) + cy * Math.round(k / C))];
    val[idx(i, j, k)] = Math.abs(c) > far ? c : f(p.set(lo.x + i * h, lo.y + j * h, lo.z + k * h));
  }
  // one vertex per cell the surface passes through, at the average of its edge crossings
  const vid = new Int32Array((nx - 1) * (ny - 1) * (nz - 1)).fill(-1);
  const cell = (i: number, j: number, k: number) => i + (nx - 1) * (j + (ny - 1) * k);
  const pos: number[] = [];
  const corners = [[0, 0, 0], [1, 0, 0], [0, 1, 0], [1, 1, 0], [0, 0, 1], [1, 0, 1], [0, 1, 1], [1, 1, 1]];
  const edges = [[0, 1], [2, 3], [4, 5], [6, 7], [0, 2], [1, 3], [4, 6], [5, 7], [0, 4], [1, 5], [2, 6], [3, 7]];
  const cv = new Float32Array(8);
  for (let k = 0; k < nz - 1; k++) for (let j = 0; j < ny - 1; j++) for (let i = 0; i < nx - 1; i++) {
    let mask = 0;
    for (let c = 0; c < 8; c++) { const [a, b, d] = corners[c]; cv[c] = val[idx(i + a, j + b, k + d)]; if (cv[c] < 0) mask |= 1 << c; }
    if (mask === 0 || mask === 255) continue;
    let sx = 0, sy = 0, sz = 0, n = 0;
    for (const [e0, e1] of edges) {
      const v0 = cv[e0], v1 = cv[e1];
      if ((v0 < 0) === (v1 < 0)) continue;
      const t = v0 / (v0 - v1), c0 = corners[e0], c1 = corners[e1];
      sx += c0[0] + (c1[0] - c0[0]) * t; sy += c0[1] + (c1[1] - c0[1]) * t; sz += c0[2] + (c1[2] - c0[2]) * t; n++;
    }
    vid[cell(i, j, k)] = pos.length / 3;
    pos.push(lo.x + (i + sx / n) * h, lo.y + (j + sy / n) * h, lo.z + (k + sz / n) * h);
  }
  // a quad across every grid edge the surface crosses
  const ind: number[] = [];
  const quad = (a: number, b: number, c: number, d: number, flip: boolean) => {
    if (a < 0 || b < 0 || c < 0 || d < 0) return;
    if (flip) ind.push(a, b, c, a, c, d); else ind.push(a, c, b, a, d, c);
  };
  for (let k = 1; k < nz - 1; k++) for (let j = 1; j < ny - 1; j++) for (let i = 0; i < nx - 1; i++) {      // x edges
    const a = val[idx(i, j, k)], b = val[idx(i + 1, j, k)]; if ((a < 0) === (b < 0)) continue;
    quad(vid[cell(i, j - 1, k - 1)], vid[cell(i, j, k - 1)], vid[cell(i, j, k)], vid[cell(i, j - 1, k)], a < 0);
  }
  for (let k = 1; k < nz - 1; k++) for (let j = 0; j < ny - 1; j++) for (let i = 1; i < nx - 1; i++) {      // y edges
    const a = val[idx(i, j, k)], b = val[idx(i, j + 1, k)]; if ((a < 0) === (b < 0)) continue;
    quad(vid[cell(i - 1, j, k - 1)], vid[cell(i - 1, j, k)], vid[cell(i, j, k)], vid[cell(i, j, k - 1)], a < 0);
  }
  for (let k = 0; k < nz - 1; k++) for (let j = 1; j < ny - 1; j++) for (let i = 1; i < nx - 1; i++) {      // z edges
    const a = val[idx(i, j, k)], b = val[idx(i, j, k + 1)]; if ((a < 0) === (b < 0)) continue;
    quad(vid[cell(i - 1, j - 1, k)], vid[cell(i, j - 1, k)], vid[cell(i, j, k)], vid[cell(i - 1, j, k)], a < 0);
  }
  // smooth normals from the shape itself; pull each vertex onto the true surface once
  const nor = new Float32Array(pos.length), e = h * 0.5;
  for (let v = 0; v < pos.length; v += 3) {
    p.set(pos[v], pos[v + 1], pos[v + 2]);
    const gx = f(_g.set(p.x + e, p.y, p.z)) - f(_g.set(p.x - e, p.y, p.z));
    const gy = f(_g.set(p.x, p.y + e, p.z)) - f(_g.set(p.x, p.y - e, p.z));
    const gz = f(_g.set(p.x, p.y, p.z + e)) - f(_g.set(p.x, p.y, p.z - e));
    const L = Math.hypot(gx, gy, gz) || 1;
    const d = f(p);
    pos[v] -= gx / L * d; pos[v + 1] -= gy / L * d; pos[v + 2] -= gz / L * d;
    nor[v] = gx / L; nor[v + 1] = gy / L; nor[v + 2] = gz / L;
  }
  return { pos: new Float32Array(pos), nor, ind };
}
const _g = new THREE.Vector3(), _q = new THREE.Vector3();

const cache = new Map<string, THREE.BufferGeometry>();

/**
 * Builds the body shells (skin, garments) for one character, bound to `bones` (prim.bone indexes into it).
 * Geometry is shared between characters with the same `key` (same person, same pose at build time).
 */
export function buildBody(key: string, root: THREE.Object3D, bones: THREE.Object3D[], shells: Shell[], h = 0.072): THREE.SkinnedMesh[] {
  if ((new THREE.BufferGeometry() as unknown as { isBufferGeometry?: unknown }).isBufferGeometry !== true) return [];   // test stub
  root.updateMatrixWorld(true);
  const skeleton = new THREE.Skeleton(bones as THREE.Bone[]);
  return shells.map((sh, si) => {
    const ck = key + '#' + si;
    let geo = cache.get(ck);
    if (!geo) {
      const { pos, nor, ind } = extract(sh, h);
      geo = new THREE.BufferGeometry();
      geo.setAttribute('position', new THREE.BufferAttribute(pos, 3));
      geo.setAttribute('normal', new THREE.BufferAttribute(nor, 3));
      // cloth patterns wrap around the body (stripes run down a galabeya)
      const n = pos.length / 3, uv = new Float32Array(n * 2), si4 = new Uint16Array(n * 4), sw4 = new Float32Array(n * 4);
      const p = new THREE.Vector3(), dist = new Float32Array(bones.length);
      for (let v = 0; v < n; v++) {
        p.set(pos[v * 3], pos[v * 3 + 1], pos[v * 3 + 2]);
        uv[v * 2] = Math.atan2(p.x, p.z) / (Math.PI * 2) + 0.5; uv[v * 2 + 1] = p.y * 0.16;
        // skin weights: the nearest joints by distance to their shapes, softly blended (a smooth bend at the shoulder, elbow, hip, knee)
        dist.fill(1e9);
        for (const P of sh.prims) { const d = primDist(P, p, false); if (d < dist[P.bone]) dist[P.bone] = d; }
        let dmin = 1e9; for (let b = 0; b < bones.length; b++) dmin = Math.min(dmin, dist[b]);
        const ws: [number, number][] = [];
        for (let b = 0; b < bones.length; b++) if (dist[b] < 1e8) ws.push([b, Math.exp(-(dist[b] - dmin) / 0.11)]);
        ws.sort((x, y) => y[1] - x[1]);
        let tot = 0; for (let q = 0; q < Math.min(4, ws.length); q++) tot += ws[q][1];
        for (let q = 0; q < 4; q++) { si4[v * 4 + q] = ws[q]?.[0] ?? 0; sw4[v * 4 + q] = ws[q] ? ws[q][1] / tot : 0; }
      }
      geo.setAttribute('uv', new THREE.BufferAttribute(uv, 2));
      geo.setAttribute('skinIndex', new THREE.Uint16BufferAttribute(si4, 4));
      geo.setAttribute('skinWeight', new THREE.Float32BufferAttribute(sw4, 4));
      geo.setIndex(pos.length / 3 > 65535 ? new THREE.Uint32BufferAttribute(ind, 1) : new THREE.Uint16BufferAttribute(ind, 1));
      geo.computeBoundingSphere();
      cache.set(ck, geo);
    }
    // its own copy of the material: a material shared with rigid meshes (hands, collars) gets the wrong shader variant
    const m = new THREE.SkinnedMesh(geo, sh.material.clone());
    m.castShadow = true; m.receiveShadow = true; m.frustumCulled = false;
    m.userData.dynamic = true;
    root.add(m);
    m.updateMatrixWorld(true);
    m.bind(skeleton, m.matrixWorld);
    return m;
  });
}
