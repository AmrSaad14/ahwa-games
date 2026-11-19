"""Replaces one embedded image of a .glb (by its index in "images") with a PNG file, keeping everything else.
Usage: python3 tools/glb-replace-image.py model.glb INDEX new.png"""
import json, struct, sys

def main(path, index, png):
    d = open(path, 'rb').read()
    assert d[:4] == b'glTF'
    jl = struct.unpack('<I', d[12:16])[0]
    j = json.loads(d[20:20 + jl])
    bin_start = 20 + jl + 8
    bl = struct.unpack('<I', d[20 + jl:24 + jl])[0]
    binary = bytearray(d[bin_start:bin_start + bl])
    new = open(png, 'rb').read()
    bvi = j['images'][index]['bufferView']
    views = j['bufferViews']
    # rebuild the binary chunk view by view (in offset order), swapping the image's bytes
    order = sorted(range(len(views)), key=lambda i: views[i].get('byteOffset', 0))
    out = bytearray()
    for i in order:
        v = views[i]
        chunk = new if i == bvi else bytes(binary[v.get('byteOffset', 0):v.get('byteOffset', 0) + v['byteLength']])
        while len(out) % 4: out += b'\0'
        v['byteOffset'] = len(out)
        v['byteLength'] = len(chunk)
        out += chunk
    while len(out) % 4: out += b'\0'
    j['buffers'][0]['byteLength'] = len(out)
    js = json.dumps(j, separators=(',', ':')).encode()
    while len(js) % 4: js += b' '
    total = 12 + 8 + len(js) + 8 + len(out)
    res = b'glTF' + struct.pack('<II', 2, total) + struct.pack('<I', len(js)) + b'JSON' + js + struct.pack('<I', len(out)) + b'BIN\0' + bytes(out)
    open(path, 'wb').write(res)
    print('replaced image', index, 'in', path, '(', len(new), 'bytes ) total', total)

if __name__ == '__main__':
    main(sys.argv[1], int(sys.argv[2]), sys.argv[3])
