import * as THREE from 'three';

/*
 * The "film" look on capable devices, in one small pipeline (no extra packages):
 *  - the scene renders into an HDR, 4x-multisampled buffer with depth
 *  - depth of field: the table and its players stay sharp, the café around softly melts away,
 *    so the eye goes straight to the game (like a photo taken with a real lens)
 *  - bloom: the hanging bulbs, the brass and polished pieces glow a little
 *  - a gentle vignette and a warm grade, then filmic tone mapping and a touch of dither (no banding)
 * About 7 cheap passes, most at half or quarter resolution.
 */

const VERT = /* glsl */`varying vec2 vUv; void main() { vUv = uv; gl_Position = vec4(position.xy, 0.0, 1.0); }`;

const DOWN = /* glsl */`
uniform sampler2D src; uniform vec2 texel; varying vec2 vUv;
void main() {
  // 4 bilinear taps = a 4x4 box: a clean half-size copy without flicker
  vec4 c = texture2D(src, vUv + texel * vec2(-1.0, -1.0)) + texture2D(src, vUv + texel * vec2(1.0, -1.0))
         + texture2D(src, vUv + texel * vec2(-1.0, 1.0)) + texture2D(src, vUv + texel * vec2(1.0, 1.0));
  gl_FragColor = c * 0.25;
}`;

const BLUR = /* glsl */`
uniform sampler2D src; uniform vec2 dir; varying vec2 vUv;
void main() {
  // 9-tap gaussian folded into 5 bilinear reads
  vec4 c = texture2D(src, vUv) * 0.227027;
  vec2 o1 = dir * 1.3846153846, o2 = dir * 3.2307692308;
  c += (texture2D(src, vUv + o1) + texture2D(src, vUv - o1)) * 0.3162162162;
  c += (texture2D(src, vUv + o2) + texture2D(src, vUv - o2)) * 0.0702702703;
  gl_FragColor = c;
}`;

const COMPOSITE = /* glsl */`
uniform sampler2D sharp; uniform sampler2D soft; uniform sampler2D softer; uniform sampler2D depth;
uniform mat4 projInv; uniform mat4 camWorld; uniform float tableR; uniform float dof;
uniform float bloom; uniform float vignette; uniform vec2 aspect; uniform float seed;
varying vec2 vUv;
float hash(vec2 p) { return fract(sin(dot(p, vec2(12.9898, 78.233)) + seed) * 43758.5453); }
void main() {
  vec3 col = texture2D(sharp, vUv).rgb;
  vec3 s1 = texture2D(soft, vUv).rgb, s2 = texture2D(softer, vUv).rgb;
  // depth of field, by where the pixel is in the room: the table top and everything on and above it
  // stays sharp; the floor below it and the rest of the café (other tables, walls) go soft
  vec4 v = projInv * vec4(vUv * 2.0 - 1.0, texture2D(depth, vUv).x * 2.0 - 1.0, 1.0);
  vec3 w = (camWorld * vec4(v.xyz / v.w, 1.0)).xyz;
  float below = smoothstep(0.6, 3.2, -w.y);
  float away = smoothstep(tableR + 1.0, tableR + 7.0, length(w.xz));
  float coc = dof * max(below, away);
  col = coc < 0.5 ? mix(col, s1, coc * 2.0) : mix(s1, s2, coc * 2.0 - 1.0);
  // bloom: only real highlights (bulbs, glints) glow, so the boards stay crisp
  vec3 b = max(s1 - 2.2, 0.0) * 0.3 + max(s2 - 1.8, 0.0) * 0.5;
  col += b * bloom;
  // warm grade and a soft vignette
  col = mix(vec3(dot(col, vec3(0.2126, 0.7152, 0.0722))), col, 1.07);
  vec2 p = (vUv - 0.5) * aspect;
  col *= 1.0 - vignette * smoothstep(0.35, 1.05, length(p));
  gl_FragColor = vec4(col, 1.0);
  #include <tonemapping_fragment>
  #include <colorspace_fragment>
  // dither against banding in the dark wood and the night background
  gl_FragColor.rgb += (hash(gl_FragCoord.xy) - 0.5) / 255.0;
}`;

export class Post {
  private main: THREE.WebGLRenderTarget;
  private half = [this.rt(), this.rt()];
  private quarter = [this.rt(), this.rt()];
  private cam = new THREE.OrthographicCamera(-1, 1, 1, -1, 0, 1);
  private quad = new THREE.Mesh(new THREE.PlaneGeometry(2, 2));
  private scene = new THREE.Scene();
  private down = this.mat(DOWN, { src: { value: null }, texel: { value: new THREE.Vector2() } });
  private blur = this.mat(BLUR, { src: { value: null }, dir: { value: new THREE.Vector2() } });
  private comp: THREE.ShaderMaterial;
  private w = 1; private h = 1;

  constructor(private renderer: THREE.WebGLRenderer) {
    const caps = renderer.capabilities, ext = renderer.extensions;
    if (!caps.isWebGL2 && !ext.has('WEBGL_depth_texture')) throw new Error('no depth textures');
    // HDR so the bulbs can be brighter than white (bloom); plain 8-bit where the GPU can't render half floats
    const hdr = caps.isWebGL2 && (ext.has('EXT_color_buffer_half_float') || ext.has('EXT_color_buffer_float'));
    this.main = new THREE.WebGLRenderTarget(1, 1, {
      type: hdr ? THREE.HalfFloatType : THREE.UnsignedByteType,
      samples: caps.isWebGL2 ? Math.min(4, caps.maxSamples ?? 4) : 0,
      depthTexture: new THREE.DepthTexture(1, 1),
    });
    for (const r of [...this.half, ...this.quarter]) r.texture.type = hdr ? THREE.HalfFloatType : THREE.UnsignedByteType;
    this.comp = this.mat(COMPOSITE, {
      sharp: { value: this.main.texture }, soft: { value: this.half[0].texture }, softer: { value: this.quarter[0].texture },
      depth: { value: this.main.depthTexture }, projInv: { value: new THREE.Matrix4() }, camWorld: { value: new THREE.Matrix4() },
      tableR: { value: 6 }, dof: { value: 1 },
      bloom: { value: 1 }, vignette: { value: 0.32 }, aspect: { value: new THREE.Vector2(1, 1) }, seed: { value: 0 },
    });
    this.comp.toneMapped = true;
    this.quad.frustumCulled = false;
    this.scene.add(this.quad);
  }

  private rt() { return new THREE.WebGLRenderTarget(1, 1, { depthBuffer: false }); }
  private mat(frag: string, uniforms: Record<string, THREE.IUniform>) {
    const m = new THREE.ShaderMaterial({ vertexShader: VERT, fragmentShader: frag, uniforms, depthTest: false, depthWrite: false });
    m.toneMapped = false;
    return m;
  }

  /** Size in device pixels (the canvas drawing-buffer size). */
  setSize(w: number, h: number) {
    w = Math.max(1, Math.round(w)); h = Math.max(1, Math.round(h));
    if (w === this.w && h === this.h) return;
    this.w = w; this.h = h;
    this.main.setSize(w, h);
    const hw = Math.max(1, w >> 1), hh = Math.max(1, h >> 1), qw = Math.max(1, w >> 2), qh = Math.max(1, h >> 2);
    for (const r of this.half) r.setSize(hw, hh);
    for (const r of this.quarter) r.setSize(qw, qh);
    const a = w / h;
    this.comp.uniforms.aspect.value.set(a > 1 ? a : 1, a > 1 ? 1 : 1 / a).multiplyScalar(1 / Math.SQRT2 * 1.2);
  }

  /**
   * Draw a frame. The table (centred at the origin, top at y = 0, half-size `tableR`) stays sharp;
   * `dof` 0..1 is how soft the rest of the room gets.
   */
  render(scene: THREE.Scene, camera: THREE.PerspectiveCamera, tableR: number, dof: number) {
    const r = this.renderer, prevTarget = r.getRenderTarget();
    const u = this.comp.uniforms;
    camera.updateMatrixWorld();
    u.projInv.value.copy(camera.projectionMatrixInverse); u.camWorld.value.copy(camera.matrixWorld);
    u.tableR.value = tableR; u.dof.value = dof; u.seed.value = (u.seed.value + 0.618) % 10;

    r.setRenderTarget(this.main); r.render(scene, camera);
    const pass = (m: THREE.ShaderMaterial, to: THREE.WebGLRenderTarget | null) => { this.quad.material = m; r.setRenderTarget(to); r.render(this.scene, this.cam); };
    // half size copy, blurred
    this.down.uniforms.src.value = this.main.texture; this.down.uniforms.texel.value.set(0.5 / this.w, 0.5 / this.h);
    pass(this.down, this.half[0]);
    this.blurPass(this.half, pass);
    // quarter size, blurred twice as wide
    this.down.uniforms.src.value = this.half[0].texture; this.down.uniforms.texel.value.set(1 / this.half[0].width, 1 / this.half[0].height);
    pass(this.down, this.quarter[0]);
    this.blurPass(this.quarter, pass, 1.6);
    pass(this.comp, prevTarget);
  }

  private blurPass(pair: THREE.WebGLRenderTarget[], pass: (m: THREE.ShaderMaterial, to: THREE.WebGLRenderTarget) => void, k = 1) {
    const [a, b] = pair, d = this.blur.uniforms.dir.value as THREE.Vector2;
    this.blur.uniforms.src.value = a.texture; d.set(k / a.width, 0); pass(this.blur, b);
    this.blur.uniforms.src.value = b.texture; d.set(0, k / a.height); pass(this.blur, a);
  }

  dispose() {
    for (const r of [this.main, ...this.half, ...this.quarter]) r.dispose();
    this.main.depthTexture?.dispose();
    this.down.dispose(); this.blur.dispose(); this.comp.dispose(); this.quad.geometry.dispose();
  }
}
