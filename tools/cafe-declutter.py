# Removes the loose furniture standing on the café floor (the extra tables, chairs, glasses, boards) from the room
# model, so only the game tables (placed by Cafe.cs) stand there. Removed triangles become degenerate (in place).
import json,struct,sys
import numpy as np
src,dst=sys.argv[1],sys.argv[2]; dry=len(sys.argv)>3
d=bytearray(open(src,'rb').read())
l=struct.unpack_from('<I',d,12)[0]; j=json.loads(d[20:20+l]); bin0=20+l+8
nodes=j['nodes']; meshes=j['meshes']; acc=j['accessors']; bv=j['bufferViews']
def mat(n):
    if 'matrix' in n: return np.array(n['matrix']).reshape(4,4).T
    T=np.eye(4)
    if 'translation' in n: T[:3,3]=n['translation']
    R=np.eye(4)
    if 'rotation' in n:
        x,y,z,w=n['rotation']
        R[:3,:3]=[[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]]
    S=np.eye(4)
    if 'scale' in n: S[0,0],S[1,1],S[2,2]=n['scale']
    return T@R@S
CT={5121:('B',1),5123:('H',2),5125:('I',4)}
def view(aid):
    A=acc[aid]; v=bv[A['bufferView']]; off=bin0+v.get('byteOffset',0)+A.get('byteOffset',0); return A,v,off
def positions(aid):
    A,v,off=view(aid); st=v.get('byteStride',12)
    return np.array([struct.unpack_from('<3f',d,off+i*st) for i in range(A['count'])])
def indices(aid):
    A,v,off=view(aid); f,sz=CT[A['componentType']]; st=v.get('byteStride',sz)
    return [struct.unpack_from('<'+f,d,off+i*st)[0] for i in range(A['count'])],(off,f,st)
room=[i for i,n in enumerate(nodes) if n.get('name')=='Room'][0]
removed=0; kept=0; seen=set()
def walk(i,M):
    global removed,kept
    n=nodes[i]; M=M@mat(n)
    if 'mesh' in n:
        for pi,pr in enumerate(meshes[n['mesh']]['primitives']):
            if 'indices' not in pr: print('no indices', i); continue
            key=(n['mesh'],pi)
            P=positions(pr['attributes']['POSITION']); W=(np.c_[P,np.ones(len(P))]@M.T)[:,:3]
            idx,(off,f,st)=indices(pr['indices'])
            # connected pieces: vertices joined by triangles, and vertices at the same place (split normals)
            par=list(range(len(P)))
            def find(a):
                while par[a]!=a: par[a]=par[par[a]]; a=par[a]
                return a
            def uni(a,b):
                a,b=find(a),find(b)
                if a!=b: par[a]=b
            for t in range(0,len(idx),3): uni(idx[t],idx[t+1]); uni(idx[t],idx[t+2])
            byp={}
            for vi,p in enumerate(np.round(W,3)): k=tuple(p); (uni(vi,byp[k]) if k in byp else byp.__setitem__(k,vi))
            comp={}
            for vi in range(len(P)): comp.setdefault(find(vi),[]).append(vi)
            drop=set()
            for c,vs in comp.items():
                lo=W[vs].min(0); hi=W[vs].max(0); ctr=(lo+hi)/2; size=hi-lo
                floor_item = abs(ctr[0])<23 and -14.3<ctr[2]<23.5 and hi[1]<3.5 and size[0]<10 and size[2]<10 and lo[1]>-4.5
                if floor_item: drop.add(c)
                elif dry and abs(ctr[0])<23 and -14<ctr[2]<23.5 and hi[1]<6 and size[0]<20: print("  KEEP", i, np.round(ctr,1), np.round(size,1), np.round(lo[1],1), np.round(hi[1],1))
            if dry and drop:
                for c in drop:
                    vs=comp[c]; lo=W[vs].min(0); hi=W[vs].max(0); print('  drop', i, np.round((lo+hi)/2,1), np.round(hi-lo,1))
            if key in seen and drop: print('shared mesh!', key)
            seen.add(key)
            for t in range(0,len(idx),3):
                if find(idx[t]) in drop:
                    removed+=1
                    a=idx[t]
                    for k in range(3): struct.pack_into('<'+f,d,off+(t+k)*st,a)
                else: kept+=1
    for c in n.get('children',[]): walk(c,M)
walk(room,np.eye(4))
print('triangles removed',removed,'kept',kept)
if not dry: open(dst,'wb').write(d)
