// Test-only stand-in for three.js so the UI can be exercised in a headless browser without the real package.
/* eslint-disable */
// Anything not modelled below is a chainable no-op, so new three.js calls in the game don't break the headless UI test.
const anyFn: any = new Proxy(function () {}, { get: (_t, k) => (k === Symbol.toPrimitive ? () => 0 : k === 'then' ? undefined : anyFn), apply: () => anyFn, construct: () => anyFn });
const loose = <T extends object>(o: T): T => new Proxy(o, { get: (t: any, k) => (k in t || typeof k === 'symbol' ? t[k] : anyFn) });
export class Vector3 { constructor(public x = 0, public y = 0, public z = 0) { return loose(this); } set(x: number, y: number, z: number) { this.x = x; this.y = y; this.z = z; return this; } project() { this.x = 0; this.y = 0; this.z = 0.5; return this; } clone() { return new Vector3(this.x, this.y, this.z); } copy(v: any) { this.x = v.x; this.y = v.y; this.z = v.z; return this; } setScalar(n: number) { this.x = this.y = this.z = n; return this; } lerp() { return this; } }
export class Vector2 { constructor(public x = 0, public y = 0) {} }
export class Obj_ {}
class Obj {
  constructor(..._a: any[]) { return loose(this); }
  parent: any = null;
  children: any[] = []; userData: any = {}; visible = true; castShadow = false; receiveShadow = false;
  position = new Vector3(); rotation = new Vector3(); scale = new Vector3(1, 1, 1);
  add(...o: any[]) { this.children.push(...o); return this; }
  remove(o: any) { this.children = this.children.filter(c => c !== o); return this; }
  clear() { this.children = []; return this; }
  traverse(fn: (o: any) => void) { fn(this); this.children.forEach(c => c.traverse?.(fn)); }
  lookAt() {}
}
export class Object3D extends Obj {}
export class Group extends Obj {}
export class Scene extends Obj { background: any; fog: any; }
export class Mesh extends Obj { isMesh = true; constructor(public geometry?: any, public material?: any) { super(); } }
export class PerspectiveCamera extends Obj { aspect = 1; constructor(public fov = 50) { super(); } updateProjectionMatrix() {} setViewOffset(..._a: any[]) {} clearViewOffset() {} updateMatrixWorld() {} }
class Light extends Obj { shadow: any = { mapSize: { set() {}, setScalar() {} }, camera: {}, bias: 0, map: null }; }
export class SpotLight extends Light { target = new Obj(); } export class HemisphereLight extends Light {} export class DirectionalLight extends Light {} export class PointLight extends Light {}
export class BufferGeometry { constructor(..._a: any[]) { return loose(this); } rotateX() { return this; } translate() { return this; } computeVertexNormals() {} } export class BoxGeometry extends BufferGeometry {} export class PlaneGeometry extends BufferGeometry {}
export class RingGeometry extends BufferGeometry {} export class ExtrudeGeometry extends BufferGeometry { constructor(..._a: any[]) { super(); } } export class LatheGeometry extends BufferGeometry { constructor(..._a: any[]) { super(); } } export class ConeGeometry extends BufferGeometry { constructor(..._a: any[]) { super(); } } export class TubeGeometry extends BufferGeometry { constructor(..._a: any[]) { super(); } }
export class Shape { moveTo() {} lineTo() {} quadraticCurveTo() {} bezierCurveTo() {} absarc() {} }
export class CatmullRomCurve3 { constructor(_p?: any) {} }
export class Sprite extends Obj { constructor(public material?: any) { super(); } } export class CylinderGeometry extends BufferGeometry {} export class SphereGeometry extends BufferGeometry {} export class TorusGeometry extends BufferGeometry {}
export class Material { opacity = 1; constructor(p: any = {}) { Object.assign(this, p); return loose(this); } clone(): any { return this; } }
export class MeshStandardMaterial extends Material {} export class MeshPhysicalMaterial extends Material {} export class MeshBasicMaterial extends Material {} export class SpriteMaterial extends Material { clone(): any { return new SpriteMaterial(this); } }
export type MeshStandardMaterialParameters = Record<string, unknown>;
export class CanvasTexture { center = new Vector3(); rotation = 0; wrapS = 0; wrapT = 0; repeat = { set() {} }; needsUpdate = false; colorSpace = ''; anisotropy = 1; constructor(public image?: any) {} clone() { return new CanvasTexture(this.image); } }
export class Color { constructor(public c?: any) { return loose(this); } }
export class Fog { constructor(..._a: any[]) {} }
export class Raycaster { setFromCamera() {} intersectObjects() { return []; } }
export class WebGLRenderer { shadowMap: any = {}; info: any = { autoReset: true, reset() {}, render: { calls: 0, triangles: 0 }, memory: { geometries: 0, textures: 0 } }; outputColorSpace = ''; toneMapping = 0; toneMappingExposure = 1; constructor(_o?: any) {} setPixelRatio() {} getPixelRatio() { return 1; } setSize() {} render() {} }
export const ACESFilmicToneMapping = 4, PCFSoftShadowMap = 2, PCFShadowMap = 1, SRGBColorSpace = 'srgb', RepeatWrapping = 1000, DoubleSide = 2;
export class Float32BufferAttribute { constructor(..._a: any[]) { return loose(this); } } export class Texture extends CanvasTexture {}
export class CircleGeometry extends BufferGeometry {} export class CapsuleGeometry extends BufferGeometry {} export class IcosahedronGeometry extends BufferGeometry {}
export class Quaternion { constructor(..._a: any[]) { return loose(this); } } export class Euler { constructor(..._a: any[]) { return loose(this); } } export class Matrix4 { constructor() { return loose(this); } }
export class InstancedMesh extends Mesh {} export class Points extends Obj {} export class Line extends Obj {}
export type Intersection = { object: any; point: Vector3; distance: number };
export const FrontSide = 0, BackSide = 1, AdditiveBlending = 2, NormalBlending = 1, ClampToEdgeWrapping = 1001, LinearFilter = 1006, MathUtils = { lerp: (a: number, b: number, t: number) => a + (b - a) * t, clamp: (v: number, a: number, b: number) => Math.min(b, Math.max(a, v)), degToRad: (d: number) => d * Math.PI / 180 };
export class PMREMGenerator { constructor(_r?: any) {} fromScene() { return { texture: new CanvasTexture() }; } dispose() {} }
export type MeshPhysicalMaterialParameters = Record<string, unknown>;
