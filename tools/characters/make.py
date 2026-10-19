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
         skin="onlytheghosts_old_eurasian_male", skin_dark=0.3, hair="short02", hair_tint=(0.82, 0.8, 0.76), brows="mindfront_eyebrows_06", brow_tint=(0.75, 0.72, 0.68),
         clothes=["toigo_male_suit_tie_and_jacket", "shoes02", "kwnet_at_optical_glasses", "rehmanpolanski_moustache_viking"],
         tints={"rehmanpolanski_moustache_viking": (0.85, 0.83, 0.8)}, hat="tarboosh"),
    dict(name="CaptainSami", gender=1, age=0.66, weight=0.6, muscle=0.85, height=0.62, proportions=0.8, eth=(0.25, 0.05, 0.7),
         skin="middleage_caucasian_male", skin_dark=0.45, hair="short04", hair_tint=(0.12, 0.09, 0.07), brows="mindfront_eyebrows_03",
         clothes=["namuhekam_male_polo_shirt", "toigo_wool_pants", "shoes01", "rehmanpolanski_moustache_viking"],
         tints={"namuhekam_male_polo_shirt": (0.14, 0.25, 0.42), "toigo_wool_pants": (0.49, 0.43, 0.33), "rehmanpolanski_moustache_viking": (0.1, 0.08, 0.06)}),
    dict(name="Ziko", gender=1, age=0.48, weight=0.45, muscle=0.6, height=0.6, proportions=0.6, eth=(0.35, 0.05, 0.6),
         skin="toigo_light_skin_male_bronze", skin_dark=0.3, hair="short03", hair_tint=(0.1, 0.07, 0.05), brows="mindfront_eyebrows_02",
         clothes=["toigo_basic_tucked_t-shirt", "toigo_wool_pants", "shoes03", "grinsegold_beard_sigmund_wip"],
         tints={"toigo_basic_tucked_t-shirt": (0.18, 0.49, 0.36), "toigo_wool_pants": (0.16, 0.2, 0.3), "grinsegold_beard_sigmund_wip": (0.09, 0.06, 0.045)}),
    dict(name="Mona", gender=0, age=0.6, weight=0.58, muscle=0.35, height=0.42, proportions=0.35, eth=(0.15, 0.05, 0.8),
         skin="onlytheghosts_middle_aged_eurasian_female", skin_dark=0.22, hair=None, brows="mindfront_eyebrows_09",
         clothes=["toigo_fisherman_sweater", "toigo_long_full_skirt", "toigo_flats"],
         tints={"toigo_fisherman_sweater": (0.24, 0.2, 0.31), "toigo_long_full_skirt": (0.17, 0.14, 0.2)}, hat="hijab", scarf=(0.71, 0.32, 0.23)),
    dict(name="Nada", gender=0, age=0.48, weight=0.42, muscle=0.4, height=0.48, proportions=0.4, eth=(0.1, 0.05, 0.85),
         skin="onlytheghosts_young_eurasian_female", skin_dark=0.22, hair="long01", hair_tint=(0.16, 0.09, 0.05), brows="mindfront_eyebrows_11",
         clothes=["female_casualsuit01", "toigo_ballet_flats", "toigo_pearl_earrings"],
         tints={}),
    dict(name="HajjMahmoud", gender=1, age=0.88, weight=0.62, muscle=0.5, height=0.52, proportions=0.6, eth=(0.3, 0.05, 0.65),
         skin="onlytheghosts_old_eurasian_male", skin_dark=0.3, hair="short02", hair_tint=(0.86, 0.84, 0.8), brows="mindfront_eyebrows_06", brow_tint=(0.8, 0.78, 0.74),
         clothes=["mindfront_kimono", "shoes02", "grinsegold_beard_sigmund_wip"],
         tints={"mindfront_kimono": (0.85, 0.82, 0.75), "grinsegold_beard_sigmund_wip": (0.88, 0.86, 0.82)}, hat="emma", stripes=True),
]

# ---------- materials ----------
def tint_material(obj, rgb, value=None, sat=None):
    """Multiply every base colour of the object's materials by rgb (cloth and hair recoloured, texture kept)."""
    for slot in obj.material_slots:
        m = slot.material
        if not m or not m.use_nodes: continue
        nt = m.node_tree
        for bsdf in [n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"]:
            inp = bsdf.inputs["Base Color"]
            mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = "RGBA"; mix.blend_type = "MULTIPLY"
            mix.inputs[0].default_value = 1.0
            mix.inputs[7].default_value = (*rgb, 1)
            if inp.is_linked:
                src = inp.links[0].from_socket
                if sat is not None or value is not None:
                    hsv = nt.nodes.new("ShaderNodeHueSaturation")
                    hsv.inputs["Saturation"].default_value = 0 if sat is None else sat
                    hsv.inputs["Value"].default_value = 1 if value is None else value
                    nt.links.new(src, hsv.inputs["Color"]); src = hsv.outputs["Color"]
                nt.links.new(src, mix.inputs[6])
            else:
                mix.inputs[6].default_value = inp.default_value
            nt.links.new(mix.outputs[2], inp)

def flat_material(name, rgb, rough=0.7, sheen=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*rgb, 1)
    b.inputs["Roughness"].default_value = rough
    if "Sheen Weight" in b.inputs: b.inputs["Sheen Weight"].default_value = sheen
    return m

def stripes_material(name, base, stripe):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; b = nt.nodes["Principled BSDF"]
    tex = nt.nodes.new("ShaderNodeTexWave"); tex.wave_type = "BANDS"; tex.bands_direction = "X"
    tex.inputs["Scale"].default_value = 9; tex.inputs["Distortion"].default_value = 0.3
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position = 0.7; ramp.color_ramp.elements[0].color = (*base, 1)
    ramp.color_ramp.elements[1].position = 0.75; ramp.color_ramp.elements[1].color = (*stripe, 1)
    coord = nt.nodes.new("ShaderNodeTexCoord")
    nt.links.new(coord.outputs["Object"], tex.inputs["Vector"])
    nt.links.new(tex.outputs["Fac"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 0.85
    return m

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
    r = max(rx, ry) * 0.93
    bpy.ops.mesh.primitive_cone_add(vertices=64, radius1=r, radius2=r * 0.78, depth=0.14, end_fill_type="NGON")
    t = bpy.context.active_object; t.name = "Tarboosh"
    t.location = (top.x, top.y + 0.004, top.z + 0.03); t.rotation_euler = (math.radians(-8), 0, 0)
    t.data.materials.append(flat_material("Tarboosh", (0.32, 0.015, 0.02), 0.75))
    for poly in t.data.polygons: poly.use_smooth = abs(poly.normal.z) < 0.5
    bev = t.modifiers.new("Bevel", "BEVEL"); bev.width = 0.004; bev.segments = 3; bev.limit_method = "ANGLE"
    # the black tassel from the centre of the top, falling down the back
    bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=0.006, depth=0.09)
    s = bpy.context.active_object; s.name = "Tassel"
    s.location = (top.x, top.y + r * 0.55, top.z + 0.06); s.rotation_euler = (math.radians(-35), 0, 0)
    s.data.materials.append(flat_material("Tassel", (0.015, 0.015, 0.015), 0.6))
    for poly in s.data.polygons: poly.use_smooth = True
    for o in (t, s): bone_parent(o, rig, "mixamorig:Head")
    return [t, s]

def make_emma(basemesh, rig):
    top, rx, ry = head_frame(basemesh)
    r = max(rx, ry) * 0.9
    parts = []
    mat = flat_material("Emma", (0.8, 0.79, 0.75), 0.9)
    for i in range(4):
        bpy.ops.mesh.primitive_torus_add(major_radius=r * (1.02 - i * 0.07), minor_radius=0.017, major_segments=64, minor_segments=12)
        o = bpy.context.active_object; o.name = f"EmmaWrap{i}"
        o.location = (top.x, top.y, top.z - 0.035 + i * 0.019)
        o.rotation_euler = (math.radians(-10 + (i % 2) * 9), math.radians((i % 2) * 8 - 4), math.radians(i * 40))
        o.data.materials.append(mat); smooth(o, 0); parts.append(o)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16, radius=r * 0.86)
    cap = bpy.context.active_object; cap.name = "EmmaCap"; cap.scale = (1, 1.05, 0.62)
    cap.location = (top.x, top.y, top.z - 0.005); cap.data.materials.append(flat_material("EmmaCap", (0.75, 0.73, 0.68), 0.9)); smooth(cap, 0); parts.append(cap)
    for o in parts: bone_parent(o, rig, "mixamorig:Head")
    return parts

def make_hijab(basemesh, rig, rgb):
    top, rx, ry = head_frame(basemesh)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=64, ring_count=40, radius=1)
    h = bpy.context.active_object; h.name = "Hijab"
    h.scale = (rx * 1.1, ry * 1.08, 0.125); h.location = (top.x, top.y + 0.008, top.z - 0.112)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    me = h.data; bm = bmesh.new(); bm.from_mesh(me)
    for v in bm.verts:
        if v.co.z < -0.04:   # below the ears it drapes around the neck and onto the shoulders
            k = min(1.0, (-0.04 - v.co.z) / 0.085)
            v.co.x *= 1 + k * k * 0.7; v.co.y *= 1 + k * k * 0.35; v.co.z -= k * k * 0.07
    # the oval face opening: brow to chin, cheek to cheek
    def in_face(c):
        return c.y < 0 and ((c.x / (rx * 0.66)) ** 2 + ((c.z + 0.035) / 0.075) ** 2) < 1 and c.y < -ry * 0.35
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if in_face(f.calc_center_median())], context="FACES")
    bm.to_mesh(me); bm.free()
    h.data.materials.append(flat_material("Hijab", rgb, 0.85))
    sol = h.modifiers.new("Thick", "SOLIDIFY"); sol.thickness = 0.005
    smooth(h, 1)
    bpy.ops.object.select_all(action="DESELECT")
    h.select_set(True); rig.select_set(True); bpy.context.view_layer.objects.active = rig
    try:
        bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    except Exception as e:
        p("  hijab auto weights failed:", e); bone_parent(h, rig, "mixamorig:Head")
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
    if spec.get("skin_dark"): tint_material(basemesh, (1 - spec["skin_dark"] * 0.45, 1 - spec["skin_dark"] * 0.55, 1 - spec["skin_dark"] * 0.6))
    rig = HumanService.add_builtin_rig(basemesh, "mixamo_unity")
    rig.name = spec["name"] + "_Rig"
    parts = [("eyes", "high-poly.mhclo", "Eyes"), ("eyebrows", spec["brows"] + ".mhclo", "Eyebrows"), ("eyelashes", "mindfront_eyelashes_02.mhclo", "Eyelashes"),
             ("teeth", "teeth_base.mhclo", "Teeth"), ("tongue", "tongue01.mhclo", "Tongue")]
    if spec.get("hair"): parts.append(("hair", spec["hair"] + ".mhclo", "Hair"))
    for c in spec["clothes"]: parts.append(("clothes", c + ".mhclo", "Clothes"))
    objs = {}
    for sub, fname, kind in parts:
        path = find(fname, sub)
        if not path: continue
        try:
            o = HumanService.add_mhclo_asset(path, basemesh, asset_type=kind, material_type="GAMEENGINE")
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
        k = objs["mindfront_kimono"]; k.data.materials.clear(); k.data.materials.append(stripes_material("Galabeya", (0.85, 0.82, 0.74), (0.45, 0.4, 0.33)))
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

try:
    for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
    built = []
    for i, spec in enumerate(REGULARS):
        try:
            built.append((spec, *build(spec, (i - 2.5) * 1.2)))
        except Exception:
            p(traceback.format_exc())
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
    p("done")
except Exception:
    p(traceback.format_exc())
log.flush()
