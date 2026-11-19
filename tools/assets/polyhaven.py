"""Downloads free (CC0) Poly Haven textures and models for previews: run by the "Fetch free assets" workflow,
which has the open internet (the build machines do), and publishes them as the polyhaven-assets release.
Nothing here goes into the game by itself; assets are chosen and slimmed for phones afterwards."""
import json, os, sys, time, urllib.request

API = "https://api.polyhaven.com"
OUT = sys.argv[1] if len(sys.argv) > 1 else "polyhaven"
UA = {"User-Agent": "asset-fetch/1.0"}

TEXTURES = ["dark_wood", "wood_table_worn", "fine_grained_wood", "plastered_wall", "yellow_plaster_02",
            "beige_wall_001", "red_plaster_weathered", "patterned_clay_wall", "worn_tile_floor", "tiled_floor_001",
            "concrete_tiles", "painted_concrete_02"]
KEYWORDS = ["marble", "brass", "copper", "gold", "fabric", "felt", "leather", "walnut", "oak", "mahogany",
            "rosewood", "veneer", "terrazzo", "mosaic", "tile", "plaster", "carpet", "rug", "baize", "linen"]
MODELS = ["Chandelier_01", "Chandelier_03", "brass_vase_01", "WoodenChair_01", "painted_wooden_chair_02",
          "wooden_table_02", "WoodenTable_03", "painted_wooden_table", "folding_wooden_stool", "bar_chair_round_01",
          "chinese_tea_table"]
MODEL_WORDS = ["lantern", "lamp", "chandelier", "tea", "cup", "glass", "tray", "brass", "copper", "chair", "stool",
               "table", "bench", "vase", "pot", "kettle", "chess", "dice", "game", "clock", "mirror", "frame",
               "plant", "rug", "carpet", "cushion", "radio", "fan", "shelf", "bottle", "jug", "teapot", "hookah"]


def get(url, path=None):
    for attempt in range(4):
        try:
            req = urllib.request.Request(url, headers=UA)
            with urllib.request.urlopen(req, timeout=120) as r:
                data = r.read()
            if path:
                os.makedirs(os.path.dirname(path) or ".", exist_ok=True)
                open(path, "wb").write(data)
            return data
        except Exception as e:  # a short pause, then again
            print("retry", url, e); time.sleep(2 + attempt * 3)
    raise RuntimeError("failed " + url)


def texture(tid, res="1k"):
    files = json.loads(get(f"{API}/files/{tid}"))
    got = 0
    for key, want in (("Diffuse", "col"), ("nor_gl", "nrm"), ("Rough", "rgh"), ("AO", "ao")):
        m = files.get(key, {}).get(res, {})
        f = m.get("jpg") or m.get("png")
        if not f: continue
        ext = ".jpg" if "jpg" in m else ".png"
        get(f["url"], f"{OUT}/textures/{tid}/{tid}_{want}{ext}"); got += 1
    return got


def model(mid, res="1k"):
    files = json.loads(get(f"{API}/files/{mid}"))
    g = files.get("gltf", {}).get(res, {}).get("gltf")
    if not g: return 0
    base = f"{OUT}/models/{mid}"
    get(g["url"], f"{base}/{mid}.gltf")
    for rel, inc in (g.get("include") or {}).items():
        get(inc["url"], f"{base}/{rel}")
    return 1


def main():
    os.makedirs(OUT, exist_ok=True)
    tex = json.loads(get(f"{API}/assets?t=textures"))
    mod = json.loads(get(f"{API}/assets?t=models"))
    slim = lambda d: {k: {"name": v.get("name"), "categories": v.get("categories"), "tags": v.get("tags")} for k, v in d.items()}
    json.dump(slim(tex), open(f"{OUT}/textures-index.json", "w"), indent=0)
    json.dump(slim(mod), open(f"{OUT}/models-index.json", "w"), indent=0)
    want_t = [t for t in TEXTURES if t in tex]
    for k, v in tex.items():
        words = " ".join([k] + (v.get("tags") or []) + (v.get("categories") or [])).lower()
        if k not in want_t and any(w in words for w in KEYWORDS) and len(want_t) < 60: want_t.append(k)
    want_m = [m for m in MODELS if m in mod]
    for k, v in mod.items():
        words = " ".join([k, v.get("name") or ""] + (v.get("tags") or []) + (v.get("categories") or [])).lower()
        if k not in want_m and any(w in words for w in MODEL_WORDS) and len(want_m) < 45: want_m.append(k)
    print("textures:", want_t); print("models:", want_m)
    for t in want_t:
        try: print(t, texture(t))
        except Exception as e: print("skip", t, e)
    for m in want_m:
        try: print(m, model(m))
        except Exception as e: print("skip", m, e)


if __name__ == "__main__":
    main()
