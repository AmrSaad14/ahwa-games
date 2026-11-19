"""Adds a normal map to materials of a .glb: the image is appended to the binary chunk and every given material gets
it as its normalTexture (with the same texture transform as its colour, so the two line up).
Usage: python3 tools/glb-add-normal.py model.glb normal.jpg STRENGTH MATERIAL [MATERIAL...]"""
import json, struct, sys

def main(path, img, strength, mats):
    d = open(path, 'rb').read()
    jl = struct.unpack('<I', d[12:16])[0]
    j = json.loads(d[20:20 + jl])
    bl = struct.unpack('<I', d[20 + jl:24 + jl])[0]
    binary = bytearray(d[28 + jl:28 + jl + bl])
    data = open(img, 'rb').read()
    while len(binary) % 4: binary += b'\0'
    j['bufferViews'].append({'buffer': 0, 'byteOffset': len(binary), 'byteLength': len(data)})
    binary += data
    while len(binary) % 4: binary += b'\0'
    j['buffers'][0]['byteLength'] = len(binary)
    j['images'].append({'bufferView': len(j['bufferViews']) - 1, 'mimeType': 'image/jpeg' if img.endswith('.jpg') else 'image/png'})
    j['samplers'].append({'magFilter': 9729, 'minFilter': 9987, 'wrapS': 10497, 'wrapT': 10497})
    j['textures'].append({'sampler': len(j['samplers']) - 1, 'source': len(j['images']) - 1})
    tex = len(j['textures']) - 1
    for m in mats:
        mat = j['materials'][m]
        nt = {'index': tex, 'scale': strength}
        tt = mat.get('pbrMetallicRoughness', {}).get('baseColorTexture', {}).get('extensions', {}).get('KHR_texture_transform')
        if tt: nt['extensions'] = {'KHR_texture_transform': tt}
        mat['normalTexture'] = nt
    js = json.dumps(j, separators=(',', ':')).encode()
    while len(js) % 4: js += b' '
    total = 12 + 8 + len(js) + 8 + len(binary)
    open(path, 'wb').write(b'glTF' + struct.pack('<II', 2, total) + struct.pack('<I', len(js)) + b'JSON' + js + struct.pack('<I', len(binary)) + b'BIN\0' + bytes(binary))
    print('normal map', img, 'on materials', mats, 'total', total)

if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2], float(sys.argv[3]), [int(x) for x in sys.argv[4:]])
