# Runs inside Blender (blender -b -P explore.py): installs the MakeHuman asset packs into MPFB,
# lists every asset, builds one test character and renders it. Output goes to $OUT.
import bpy, os, sys, glob, zipfile, traceback, importlib, math

OUT = os.environ.get("OUT", "/tmp/out")
os.makedirs(OUT, exist_ok=True)
log = open(os.path.join(OUT, "explore.txt"), "w")
def p(*a):
    print(*a, file=log, flush=True); print(*a, flush=True)

def dynamic_import(absolute_package_str, key):
    for amod in list(sys.modules):
        if amod.endswith(absolute_package_str):
            return getattr(importlib.import_module(amod), key)
    raise ValueError("no module " + absolute_package_str)

try:
    HumanService = dynamic_import("mpfb.services.humanservice", "HumanService")
    AssetService = dynamic_import("mpfb.services.assetservice", "AssetService")
    LocationService = dynamic_import("mpfb.services.locationservice", "LocationService")
    data = LocationService.get_user_data()
    p("user data:", data)
    for z in sorted(glob.glob(os.path.join(os.environ.get("PACKS", "/tmp/packs"), "*.zip"))):
        try:
            with zipfile.ZipFile(z) as zf: zf.extractall(data)
            p("installed", os.path.basename(z))
        except Exception as e:
            p("pack failed", z, e)
    AssetService.update_all_asset_lists()
    AssetService.rescan_pack_metadata()
    p("packs:", AssetService.get_pack_names() if AssetService.have_any_pack_meta_data() else "none")
    # every asset by kind
    for kind in ["skins", "eyes", "eyebrows", "eyelashes", "hair", "clothes", "proxymeshes", "teeth", "tongue"]:
        names = []
        for root, dirs, files in os.walk(data):
            if os.sep + kind + os.sep in root + os.sep:
                for f in files:
                    if f.endswith((".mhclo", ".mhmat")) and (kind == "skins") == f.endswith(".mhmat"):
                        names.append(os.path.relpath(os.path.join(root, f), data))
        p(f"--- {kind} ({len(names)})")
        for n in sorted(names): p("   ", n)
except Exception:
    p(traceback.format_exc())
log.flush()
