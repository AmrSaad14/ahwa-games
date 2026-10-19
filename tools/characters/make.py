# The café regulars as realistic humans: Blender + MPFB (MakeHuman), free CC0 assets.
# Runs on GitHub's machine:  OUT=… PACKS=… blender -b -P make.py
# For each regular: body (age, build, ethnicity), skin, eyes, brows, lashes, teeth, hair, clothes, beard,
# a Mixamo-compatible rig for Unity, the Egyptian pieces made here (tarboosh, emma, hijab), studio renders
# of the face and the whole body, and a .glb for the game.
import bpy, bmesh, os, sys, glob, zipfile, traceback, importlib, math
from mathutils import Vector, Euler

OUT = os.environ.get("OUT", "/tmp/out")
os.makedirs(OUT, exist_ok=True)
log = open(os.path.join(OUT, "make.txt"), "w")
def p(*a):
    print(*a, file=log, flush=True); print(*a, flush=True)

def dyn(pkg, key):
    for m in list(sys.modules):
        if m.endswith(pkg):
            return getattr(importlib.import_module(m), key)
    raise ValueError("no module " + pkg)

HumanService = dyn("mpfb.services.humanservice", "HumanService")
AssetService = dyn("mpfb.services.assetservice", "AssetService")
TargetService = dyn("mpfb.services.targetservice", "TargetService")
LocationService = dyn("mpfb.services.locationservice", "LocationService")
ExportService = dyn("mpfb.services.exportservice", "ExportService")
ObjectService = dyn("mpfb.services.objectservice", "ObjectService")
HumanObjectProperties = dyn("mpfb.entities.objectproperties", "HumanObjectProperties")
GeneralObjectProperties = dyn("mpfb.entities.objectproperties", "GeneralObjectProperties")

# ---------- the asset packs ----------
data = LocationService.get_user_data()
for z in sorted(glob.glob(os.path.join(os.environ.get("PACKS", "/tmp/packs"), "*.zip"))):
    with zipfile.ZipFile(z) as zf: zf.extractall(data)
AssetService.update_all_asset_lists()
eye_mats = sorted(glob.glob(os.path.join(data, "**", "eyes", "materials", "*.mhmat"), recursive=True))
p("eye materials:", [os.path.basename(e) for e in eye_mats])

def find(name, sub):
    path = AssetService.find_asset_absolute_path(name, asset_subdir=sub)
    if path is None: p("  missing:", sub, name)
    return path

# ---------- the regulars ----------
# age: young 0.5 (25), old 1.0 (90); gender 0 female, 1 male; ethnicity mix for Egyptians
REGULARS = [
    dict(name="AmFathi", gender=1, age=0.86, weight=0.62, muscle=0.35, height=0.42, proportions=0.45, eth=(0.15, 0.1, 0.75),
         skin="onlytheghosts_old_eurasian_male", skin_dark=0.45, hair="short02", hair_tint=(0.82, 0.8, 0.76), brows="eyebrow005", brow_tint=(0.75, 0.72, 0.68),
         clothes=["toigo_male_suit_tie_and_jacket", "shoes02", "kwnet_at_optical_glasses", "rehmanpolanski_moustache_viking"],
         tints={"rehmanpolanski_moustache_viking": (0.85, 0.83, 0.8)}, hat="tarboosh"),
    dict(name="CaptainSami", gender=1, age=0.66, weight=0.6, muscle=0.85, height=0.62, proportions=0.8, eth=(0.25, 0.05, 0.7),
         skin="middleage_caucasian_male", skin_dark=0.6, hair="short04", hair_tint=(0.12, 0.09, 0.07), brows="eyebrow001",
         clothes=["namuhekam_male_polo_shirt", "toigo_wool_pants", "shoes01", "rehmanpolanski_moustache_viking"],
         tints={"namuhekam_male_polo_shirt": (0.14, 0.25, 0.42), "toigo_wool_pants": (0.49, 0.43, 0.33), "rehmanpolanski_moustache_viking": (0.1, 0.08, 0.06)}),
    dict(name="Ziko", gender=1, age=0.48, weight=0.45, muscle=0.6, height=0.6, proportions=0.6, eth=(0.35, 0.05, 0.6),
         skin="toigo_light_skin_male_bronze", skin_dark=0.55, hair="short02", hair_tint=(0.1, 0.07, 0.05), brows="eyebrow003",
         clothes=["toigo_basic_tucked_t-shirt", "toigo_wool_pants", "shoes03", "grinsegold_beard_sigmund_wip"],
         tints={"toigo_basic_tucked_t-shirt": (0.18, 0.49, 0.36), "toigo_wool_pants": (0.16, 0.2, 0.3), "grinsegold_beard_sigmund_wip": (0.09, 0.06, 0.045)}),
    dict(name="Mona", gender=0, age=0.6, weight=0.58, muscle=0.35, height=0.42, proportions=0.35, eth=(0.15, 0.05, 0.8),
         skin="onlytheghosts_middle_aged_eurasian_female", skin_dark=0.55, hair=None, brows="eyebrow010",
         clothes=["toigo_fisherman_sweater", "toigo_long_full_skirt", "toigo_flats"],
         tints={"toigo_fisherman_sweater": (0.5, 0.42, 0.62), "toigo_long_full_skirt": (0.17, 0.14, 0.2)}, hat="hijab", scarf=(0.2, 0.045, 0.028)),
    dict(name="Nada", gender=0, age=0.48, weight=0.42, muscle=0.4, height=0.48, proportions=0.4, eth=(0.1, 0.05, 0.85),
         skin="onlytheghosts_young_eurasian_female", skin_dark=0.5, hair="long01", hair_tint=(0.16, 0.09, 0.05), brows="eyebrow012",
         clothes=["female_casualsuit01", "toigo_ballet_flats", "toigo_pearl_earrings"],
         tints={}),
    dict(name="HajjMahmoud", gender=1, age=0.88, weight=0.62, muscle=0.5, height=0.52, proportions=0.6, eth=(0.3, 0.05, 0.65),
         skin="onlytheghosts_old_eurasian_male", skin_dark=0.5, hair="short02", hair_tint=(0.86, 0.84, 0.8), brows="eyebrow005", brow_tint=(0.8, 0.78, 0.74),
         clothes=["mindfront_kimono", "shoes02", "grinsegold_beard_sigmund_wip"],
         tints={"mindfront_kimono": (0.85, 0.82, 0.75), "grinsegold_beard_sigmund_wip": (0.88, 0.86, 0.82)}, hat="emma", stripes=True),
]

# ---------- materials ----------
def base_image(m):
    """The image node that feeds a material's base colour (directly or through a few nodes), or None."""
    nt = m.node_tree
    bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if bsdf is None: return None, None
    seen, todo = set(), [bsdf.inputs["Base Color"]]
    while todo:
        sock = todo.pop()
        for l in sock.links:
            n = l.from_node
            if n.name in seen: continue
            seen.add(n.name)
            if n.type == "TEX_IMAGE" and n.image: return bsdf, n
            todo.extend(i for i in n.inputs if i.is_linked)
    return bsdf, None

def tint_material(obj, rgb, value=None, sat=None):
    """Recolour cloth and hair by changing the texture's pixels (so the colour survives the export to the game)."""
    import numpy as np
    for slot in obj.material_slots:
        m = slot.material
        if not m or not m.use_nodes: continue
        bsdf, node = base_image(m)
        if bsdf is None: continue
        if node is None:
            c = bsdf.inputs["Base Color"].default_value
            bsdf.inputs["Base Color"].default_value = (c[0] * rgb[0], c[1] * rgb[1], c[2] * rgb[2], 1); continue
        img = node.image.copy(); img.name = obj.name + "_" + node.image.name
        px = np.empty(len(img.pixels), dtype=np.float32); img.pixels.foreach_get(px)
        px = px.reshape(-1, 4)
        if sat is not None:
            g = px[:, :3] @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
            px[:, :3] = g[:, None] + (px[:, :3] - g[:, None]) * sat
        if value is not None: px[:, :3] *= value
        px[:, :3] = np.clip(px[:, :3] * np.array(rgb, dtype=np.float32), 0, 1)
        img.pixels.foreach_set(px.ravel()); img.update(); img.pack()
        node.image = img

def shrink_images(limit=1024):
    small = ("brow", "lash", "eye", "teeth", "tongue", "shoe", "flats", "glasses", "earring")
    for img in bpy.data.images:
        lim = 512 if any(k in img.name.lower() for k in small) else limit
        if img.size[0] > lim or img.size[1] > lim:
            limit_now = lim
            k = limit_now / max(img.size[0], img.size[1])
            img.scale(max(1, int(img.size[0] * k)), max(1, int(img.size[1] * k)))

def flat_material(name, rgb, rough=0.7, sheen=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*rgb, 1)
    b.inputs["Roughness"].default_value = rough
    if "Sheen Weight" in b.inputs: b.inputs["Sheen Weight"].default_value = sheen
    return m

def stripes(obj, base, stripe, n=34):
    """A striped galabeya: stripes painted into a texture in the garment's own UV layout (running down it)."""
    import numpy as np
    W = 1024
    me = obj.data; uv = me.uv_layers.active
    # which way is "down the garment" in UV space: compare UV spread against height
    us = np.array([d.uv[0] for d in uv.data]); vs = np.array([d.uv[1] for d in uv.data])
    zs = np.array([me.vertices[l.vertex_index].co.z for l in me.loops])
    down_is_v = abs(np.corrcoef(vs, zs)[0, 1]) >= abs(np.corrcoef(us, zs)[0, 1])
    x = np.linspace(0, 1, W, dtype=np.float32)
    f = np.mod(x * n, 1.0)
    band = ((f > 0.72) & (f < 0.8)).astype(np.float32) + 0.5 * ((f > 0.84) & (f < 0.87)).astype(np.float32)
    col = np.array(base, np.float32)[None, :] * (1 - band[:, None]) + np.array(stripe, np.float32)[None, :] * band[:, None]
    noise = (np.random.default_rng(1).random((W, W)).astype(np.float32) - 0.5) * 0.05
    if down_is_v: rgb = np.broadcast_to(col[None, :, :], (W, W, 3)).copy()
    else: rgb = np.broadcast_to(col[:, None, :], (W, W, 3)).copy()
    rgb = np.clip(rgb + noise[:, :, None], 0, 1)
    img = bpy.data.images.new("Galabeya", W, W)
    img.pixels.foreach_set(np.concatenate([rgb, np.ones((W, W, 1), np.float32)], axis=2).ravel()); img.pack()
    m = bpy.data.materials.new("Galabeya"); m.use_nodes = True
    nt = m.node_tree; b = nt.nodes["Principled BSDF"]
    t = nt.nodes.new("ShaderNodeTexImage"); t.image = img
    nt.links.new(t.outputs["Color"], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 0.85
    obj.data.materials.clear(); obj.data.materials.append(m)

# ---------- the Egyptian pieces ----------
def head_frame(basemesh):
    """Top of the head, its centre, and the head's radius, from the shaped body."""
    dg = bpy.context.evaluated_depsgraph_get()
    ev = basemesh.evaluated_get(dg); me = ev.to_mesh()
    mw = basemesh.matrix_world
    pts = [mw @ v.co for v in me.vertices]
    ev.to_mesh_clear()
    top = max(pt.z for pt in pts)
    head = [pt for pt in pts if pt.z > top - 0.13]
    cx = sum(pt.x for pt in head) / len(head); cy = sum(pt.y for pt in head) / len(head)
    rx = (max(pt.x for pt in head) - min(pt.x for pt in head)) / 2
    ry = (max(pt.y for pt in head) - min(pt.y for pt in head)) / 2
    return Vector((cx, cy, top)), rx, ry

def bone_parent(obj, rig, bone):
    obj.parent = rig; obj.parent_type = "BONE"; obj.parent_bone = bone
    obj.matrix_parent_inverse = (rig.matrix_world @ rig.pose.bones[bone].matrix @ __import__("mathutils").Matrix.Translation((0, rig.data.bones[bone].length, 0))).inverted()

def smooth(obj, levels=1):
    for poly in obj.data.polygons: poly.use_smooth = True
    if levels:
        m = obj.modifiers.new("Subdiv", "SUBSURF"); m.levels = levels; m.render_levels = levels

def make_tarboosh(basemesh, rig):
    top, rx, ry = head_frame(basemesh)
    r = max(rx, ry) * 1.02
    bpy.ops.mesh.primitive_cone_add(vertices=64, radius1=r, radius2=r * 0.8, depth=0.135, end_fill_type="NGON")
    t = bpy.context.active_object; t.name = "Tarboosh"
    t.location = (top.x, top.y + 0.01, top.z + 0.012); t.rotation_euler = (math.radians(-7), 0, 0)
    t.data.materials.append(flat_material("Tarboosh", (0.32, 0.015, 0.02), 0.75))
    for poly in t.data.polygons: poly.use_smooth = abs(poly.normal.z) < 0.5
    bev = t.modifiers.new("Bevel", "BEVEL"); bev.width = 0.004; bev.segments = 3; bev.limit_method = "ANGLE"
    # the black tassel from the centre of the top, falling down the back
    bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=0.006, depth=0.09)
    s = bpy.context.active_object; s.name = "Tassel"
    s.location = (top.x, top.y + r * 0.6, top.z + 0.04); s.rotation_euler = (math.radians(-35), 0, 0)
    s.data.materials.append(flat_material("Tassel", (0.015, 0.015, 0.015), 0.6))
    for poly in s.data.polygons: poly.use_smooth = True
    for o in (t, s): bone_parent(o, rig, "mixamorig:Head")
    return [t, s]

def make_emma(basemesh, rig):
    top, rx, ry = head_frame(basemesh)
    parts = []
    mat = flat_material("Emma", (0.8, 0.79, 0.75), 0.9)
    for i in range(8):
        k = i / 7
        bpy.ops.mesh.primitive_torus_add(major_radius=max(rx, ry) * (1.02 - 0.2 * k * k), minor_radius=0.0115 - 0.002 * k, major_segments=64, minor_segments=10)
        o = bpy.context.active_object; o.name = f"EmmaWrap{i}"
        o.scale = (rx / max(rx, ry), ry / max(rx, ry) * 1.04, 1)
        o.location = (top.x, top.y + 0.004, top.z - 0.075 + i * 0.0125)
        o.rotation_euler = (math.radians(-14 + (i % 2) * 12), math.radians((i % 3 - 1) * 6), math.radians(i * 37))
        o.data.materials.append(mat); smooth(o, 0); parts.append(o)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16, radius=1)
    cap = bpy.context.active_object; cap.name = "EmmaCap"; cap.scale = (rx * 0.92, ry * 0.95, 0.07)
    cap.location = (top.x, top.y + 0.004, top.z - 0.005); cap.data.materials.append(flat_material("EmmaCap", (0.72, 0.7, 0.64), 0.9)); smooth(cap, 0); parts.append(cap)
    for o in parts: bone_parent(o, rig, "mixamorig:Head")
    return parts

def make_hijab(basemesh, rig, rgb):
    """A hijab grown from the head's own surface: the scalp, ears, neck and the top of the shoulders, pushed out a
    little and thickened, with an oval opening for the face. It keeps the body's skin weights, so it moves with her."""
    top, rx, ry = head_frame(basemesh)
    h = basemesh.copy(); h.data = basemesh.data.copy(); h.name = "Hijab"
    bpy.context.collection.objects.link(h)
    GeneralObjectProperties.set_value("object_type", "Clothes", entity_reference=h)   # not a second body
    for m in list(h.modifiers):
        if m.type != "ARMATURE": h.modifiers.remove(m)
    bpy.ops.object.select_all(action="DESELECT"); h.select_set(True); bpy.context.view_layer.objects.active = h
    if h.data.shape_keys: bpy.ops.object.shape_key_remove(all=True, apply_mix=True)
    h.data.materials.clear()
    mw = h.matrix_world; inv = mw.inverted()
    body = h.vertex_groups.get("body")
    bm = bmesh.new(); bm.from_mesh(h.data)
    dl = bm.verts.layers.deform.active
    cx, cy, tz = top.x, top.y, top.z
    def keep(v):
        if body is not None and dl is not None and body.index not in v[dl]: return False   # the helper geometry
        c = mw @ v.co
        dz = tz - c.z
        if dz > 0.36: return False                                              # below the shoulders
        if dz > 0.26 and ((c.x - cx) / 0.2) ** 2 + ((c.y - cy - 0.01) / 0.14) ** 2 > 1: return False   # only around the neck
        # the face: an oval from the brow to under the chin
        fy = c.y - cy
        if fy < -ry * 0.2 and ((c.x - cx) / (rx * 0.62)) ** 2 + ((dz - 0.145) / 0.098) ** 2 < 1: return False
        return True
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not keep(v)], context="VERTS")
    bm.normal_update()
    for v in bm.verts:
        c = mw @ v.co; dz = tz - c.z
        push = 0.022 + (0.03 * min(1, (dz - 0.24) / 0.06) if dz > 0.24 else 0)
        v.co += v.normal * push
    # a little volume at the back of the head (hair under it)
    for v in bm.verts:
        c = mw @ v.co
        if c.y - cy > ry * 0.2 and tz - c.z < 0.16: v.co += (inv.to_3x3() @ Vector((0, 1, 0.3))).normalized() * 0.012
    bm.to_mesh(h.data); bm.free()
    hm = flat_material("Hijab", rgb, 0.85)
    h.data.materials.append(hm)
    for slot in h.material_slots: slot.link = "DATA"
    while len(h.material_slots) > 1: h.active_material_index = len(h.material_slots) - 1; bpy.ops.object.material_slot_remove()
    h.material_slots[0].material = hm
    for poly in h.data.polygons: poly.material_index = 0
    sol = h.modifiers.new("Thick", "SOLIDIFY"); sol.thickness = 0.004; sol.offset = 1
    sub = h.modifiers.new("Smooth", "SUBSURF"); sub.levels = 1; sub.render_levels = 1
    cs = h.modifiers.new("Relax", "CORRECTIVE_SMOOTH"); cs.iterations = 6; cs.smooth_type = "LENGTH_WEIGHTED"
    for poly in h.data.polygons: poly.use_smooth = True
    # the armature modifier last, so it bends the finished shape
    arm = next((m for m in h.modifiers if m.type == "ARMATURE"), None)
    if arm: h.modifiers.move(h.modifiers.find(arm.name), len(h.modifiers) - 1)
    h.parent = basemesh.parent; h.matrix_world = mw
    return [h]

# ---------- building ----------
def build(spec, x):
    p("== " + spec["name"])
    basemesh = HumanService.create_human()
    basemesh.name = spec["name"]
    af, asn, cau = spec["eth"]
    for k, v in (("gender", spec["gender"]), ("age", spec["age"]), ("weight", spec["weight"]), ("muscle", spec["muscle"]),
                 ("height", spec["height"]), ("proportions", spec["proportions"]), ("african", af), ("asian", asn), ("caucasian", cau)):
        HumanObjectProperties.set_value(k, v, entity_reference=basemesh)
    TargetService.reapply_macro_details(basemesh)
    skin = find(spec["skin"] + ".mhmat", "skins")
    if skin: HumanService.set_character_skin(skin, basemesh, skin_type="GAMEENGINE")
    if spec.get("skin_dark"): d = spec["skin_dark"]; tint_material(basemesh, (1 - d * 0.55, 1 - d * 0.75, 1 - d * 0.92))
    rig = HumanService.add_builtin_rig(basemesh, "mixamo_unity")
    rig.name = spec["name"] + "_Rig"
    parts = [("eyes", "high-poly.mhclo", "Eyes"), ("eyebrows", spec["brows"] + ".mhclo", "Eyebrows"), ("eyelashes", "eyelashes02.mhclo", "Eyelashes")]
    if spec.get("hair"): parts.append(("hair", spec["hair"] + ".mhclo", "Hair"))
    for c in spec["clothes"]: parts.append(("clothes", c + ".mhclo", "Clothes"))
    objs = {}
    for sub, fname, kind in parts:
        path = find(fname, sub)
        if not path: continue
        try:
            o = HumanService.add_mhclo_asset(path, basemesh, asset_type=kind, material_type="GAMEENGINE", subdiv_levels=0)
            objs[fname[:-6]] = o
            if kind == "Clothes" and o is not None and not any(w in fname for w in ("glasses", "beard", "moustache", "earrings")):
                d = o.modifiers.new("Offset", "DISPLACE"); d.mid_level = 0.0; d.strength = 0.004
                o.modifiers.move(o.modifiers.find("Offset"), 0)   # before the armature, so it moves with the body
        except Exception as e:
            p("  failed", fname, e)
    # colours
    if spec.get("hair") and spec["hair"] in objs and spec.get("hair_tint"): tint_material(objs[spec["hair"]], spec["hair_tint"], value=1.3, sat=0.25)
    if spec["brows"] in objs and spec.get("brow_tint"): tint_material(objs[spec["brows"]], spec["brow_tint"], value=1.6, sat=0.2)
    for c, rgb in spec.get("tints", {}).items():
        if c in objs: tint_material(objs[c], rgb, value=1.8 if sum(rgb) > 2 else None, sat=0.0 if sum(rgb) > 2 else None)
    if spec.get("stripes") and "mindfront_kimono" in objs:
        stripes(objs["mindfront_kimono"], (0.78, 0.74, 0.66), (0.36, 0.3, 0.24))
    hat = spec.get("hat")
    extra = []
    if hat == "tarboosh": extra = make_tarboosh(basemesh, rig)
    elif hat == "emma": extra = make_emma(basemesh, rig)
    elif hat == "hijab": extra = make_hijab(basemesh, rig, spec["scarf"])
    rig.location.x = x
    p("  objects:", sorted(objs.keys()), "+", [e.name for e in extra])
    return basemesh, rig

# ---------- renders ----------
def studio():
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"; sc.cycles.device = "CPU"; sc.cycles.samples = 64; sc.cycles.use_denoising = True
    sc.render.resolution_x = 640; sc.render.resolution_y = 800
    sc.view_settings.view_transform = "AgX"
    w = bpy.data.worlds.new("W"); sc.world = w; w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.18, 0.13, 0.1, 1); w.node_tree.nodes["Background"].inputs[1].default_value = 0.6
    for name, loc, energy, size, col in [("Key", (1.6, -2.2, 2.4), 450, 1.6, (1, 0.9, 0.8)), ("Fill", (-2.2, -1.6, 1.6), 160, 2.5, (0.8, 0.85, 1)), ("Rim", (0.5, 2.4, 2.2), 300, 1.2, (1, 0.85, 0.65))]:
        bpy.ops.object.light_add(type="AREA", location=loc)
        l = bpy.context.active_object; l.name = name; l.data.energy = energy; l.data.size = size; l.data.color = col
        d = Vector((0, 0, 1.2)) - Vector(loc); l.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    bpy.ops.object.camera_add(); cam = bpy.context.active_object; sc.camera = cam
    return cam

def shoot(cam, target, dist, lens, name, height_off=0.0):
    cam.data.lens = lens
    cam.location = target + Vector((0.25 * dist, -dist, height_off))
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.render.filepath = os.path.join(OUT, name)
    bpy.ops.render.render(write_still=True)

def export(spec, basemesh, rig):
    """A copy of the character with the body shape baked in, as a .glb for the game (skinned to the Mixamo rig)."""
    copy_root = ExportService.create_character_copy(rig, name_suffix="_x")
    objs = [copy_root] + ObjectService.get_list_of_children(copy_root)
    # hats ride on the head bone: the copy has to keep that
    for src in ObjectService.get_list_of_children(rig):
        if src.parent_type == "BONE":
            c = bpy.data.objects.get(src.name + "_x")
            if c is None: continue
            mw = src.matrix_world.copy()
            c.parent = copy_root; c.parent_type = "BONE"; c.parent_bone = src.parent_bone
            bpy.context.view_layer.update(); c.matrix_world = mw
    copy_body = next(o for o in objs if o.type == "MESH" and o.name.startswith(basemesh.name) and o.name.endswith("_x"))
    ExportService.bake_modifiers_remove_helpers(copy_body, bake_masks=True)
    for o in objs:
        if o.type == "MESH" and o.data.shape_keys:
            bpy.ops.object.select_all(action="DESELECT"); o.select_set(True)
            bpy.context.view_layer.objects.active = o
            bpy.ops.object.shape_key_remove(all=True, apply_mix=True)
    # light enough for phones: the body at about half, garments and shoes simplified (hair and brow cards untouched)
    for o in objs:
        if o.type != "MESH": continue
        n = len(o.data.polygons); low = o.name.lower()
        if o is copy_body: continue   # the face needs every polygon
        elif any(k in low for k in ("hair", "short", "long", "afro", "brow", "lash", "beard", "moustache", "high-poly", "glasses")): continue
        elif any(k in low for k in ("shoe", "flats")): target = 1500
        elif any(k in low for k in ("earring", "tarboosh", "tassel", "emma")): target = 1200
        else: target = 5000
        if n > target * 1.15:
            d = o.modifiers.new("Lighter", "DECIMATE"); d.ratio = max(0.08, target / n)
            o.modifiers.move(o.modifiers.find("Lighter"), 0)
    # how each surface is drawn in the game: skin and cloth solid, hair-like cards cut out (alpha clip), lenses see-through
    fixed = set()
    for o in objs:
        if o.type != "MESH": continue
        low = o.name.lower()
        hairlike = any(k in low for k in ("short", "long", "afro", "brow", "lash", "beard", "moustache", "hair"))
        glass = "glasses" in low
        for slot in o.material_slots:
            m = slot.material
            if not m or not m.use_nodes or m.name in fixed: continue
            fixed.add(m.name)
            nt = m.node_tree
            bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
            if bsdf is None or glass: continue
            al = bsdf.inputs["Alpha"]
            if hairlike and al.is_linked:
                src = al.links[0].from_socket
                rnd = nt.nodes.new("ShaderNodeMath"); rnd.operation = "ROUND"
                nt.links.new(src, rnd.inputs[0]); nt.links.new(rnd.outputs[0], al)
            else:
                for l in list(al.links): nt.links.remove(l)
                al.default_value = 1.0
                if hasattr(m, "blend_method"): m.blend_method = "OPAQUE"
            if hasattr(m, "use_backface_culling"): m.use_backface_culling = o is copy_body   # cloth is seen from inside too
    # textures: small, and as JPEG unless they carry transparency (hair, brows, lashes)
    tmp = os.path.join(OUT, "_tex"); os.makedirs(tmp, exist_ok=True)
    done = set()
    for o in objs:
        if o.type != "MESH": continue
        for slot in o.material_slots:
            m = slot.material
            if not m or not m.use_nodes: continue
            for n in m.node_tree.nodes:
                if n.type != "TEX_IMAGE" or not n.image or n.image.name in done: continue
                img = n.image; done.add(img.name)
                alpha = any(l.to_socket.name == "Alpha" for l in n.outputs["Alpha"].links) or any(
                    nn.type == "TEX_IMAGE" and nn.image == img and nn.outputs["Alpha"].is_linked for nn in m.node_tree.nodes)
                normal = any(l.to_node.type == "NORMAL_MAP" for l in n.outputs["Color"].links)
                small = any(k in (img.name + o.name).lower() for k in ("brow", "lash", "eye", "shoe", "flats", "glasses", "earring", "teeth"))
                lim = 256 if normal else 512 if small else 1024
                if img.size[0] > lim or img.size[1] > lim:
                    k = lim / max(img.size[0], img.size[1]); img.scale(max(1, int(img.size[0] * k)), max(1, int(img.size[1] * k)))
                safe = "".join(c if c.isalnum() else "_" for c in img.name)
                path = os.path.join(tmp, safe + (".png" if alpha else ".jpg"))
                img.file_format = "PNG" if alpha else "JPEG"
                try: img.save(filepath=path, quality=85)
                except TypeError: img.filepath_raw = path; img.save()
                if img.packed_file:
                    try: img.unpack(method="REMOVE")
                    except Exception as e: p("  unpack", img.name, e)
                img.source = "FILE"; img.filepath = path; img.reload()
    copy_root.location = (0, 0, 0)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        if "tongue" in o.name.lower(): continue
        o.hide_render = False; o.hide_set(False); o.select_set(True)
    bpy.context.view_layer.objects.active = copy_root
    path = os.path.join(OUT, spec["name"] + ".glb")
    bpy.ops.export_scene.gltf(filepath=path, use_selection=True, export_apply=True, export_skins=True, export_animations=False,
                              export_morph=False, export_yup=True, export_image_format="AUTO")
    tris = sum(len(o.evaluated_get(bpy.context.evaluated_depsgraph_get()).data.polygons) for o in objs if o.type == "MESH")
    p("  exported", spec["name"], round(os.path.getsize(path) / 1e6, 1), "MB,", tris, "faces")
    for o in objs: bpy.data.objects.remove(o, do_unlink=True)

try:
    for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
    built = []
    for i, spec in enumerate(REGULARS):
        try:
            built.append((spec, *build(spec, (i - 2.5) * 1.2)))
        except Exception:
            p(traceback.format_exc())
    shrink_images(1024)
    cam = studio()
    only = os.environ.get("ONLY")
    for spec, basemesh, rig in built:
        if only and spec["name"] != only: continue
        # hide everyone else (fast renders, no one in the background)
        for s2, b2, r2 in built:
            hide = r2 is not rig
            for o in [r2] + ObjectService.get_list_of_children(r2): o.hide_render = hide
        top, rx, ry = head_frame(basemesh)
        face = Vector((top.x, top.y, top.z - 0.12))
        try:
            shoot(cam, face, 0.75, 85, f"face-{spec['name']}.png", 0.02)
            shoot(cam, Vector((top.x, top.y, top.z * 0.52)), 3.4, 50, f"body-{spec['name']}.png", 0.15)
        except Exception:
            p(traceback.format_exc())
    for spec, basemesh, rig in built:
        if only and spec["name"] != only: continue
        try: export(spec, basemesh, rig)
        except Exception: p(traceback.format_exc())
    p("done")
except Exception:
    p(traceback.format_exc())
log.flush()
