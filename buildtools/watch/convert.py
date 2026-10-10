#!/usr/bin/env python3
"""The VR watch's model from a GLB (a digital wristwatch made with Tripo for this mod): one watch taken out of
it, turned and sized to the watch's own axes, its triangles cut down (decimate.c) and coloured from its
picture, written as C (positions, normals, colours, triangles; the face's rectangle) or, for an output
ending in .cs, as C# (src/WatchModel.cs: the same, the colours as a palette of its tones).

  python3 convert.py model.glb decimate-binary out.inc|out.cs [triangles]

Watch space: X along the forearm (the strap's axis), Y out of the face (the back of the wrist), Z along the
strap at the face (X x Y); the origin on the axis under the face; 1 unit the strap's inner radius."""
import json, os, struct, subprocess, sys, tempfile
import numpy as np
from PIL import Image
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import connected_components
from scipy.spatial import cKDTree

def load(path):
    d = open(path, 'rb').read()
    n, _ = struct.unpack_from('<II', d, 12)
    j = json.loads(d[20:20 + n])
    b0 = 20 + n + 8
    def acc(i):
        a = j['accessors'][i]; bv = j['bufferViews'][a['bufferView']]
        dt = {5126: np.float32, 5125: np.uint32, 5123: np.uint16}[a['componentType']]
        k = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3}[a['type']]
        arr = np.frombuffer(d, dtype=dt, count=a['count'] * k, offset=b0 + bv['byteOffset'] + a.get('byteOffset', 0))
        return arr.reshape(a['count'], k) if k > 1 else arr
    pr = j['meshes'][0]['primitives'][0]
    P = acc(pr['attributes']['POSITION']).astype(np.float64)
    N = acc(pr['attributes']['NORMAL']).astype(np.float64)
    UV = acc(pr['attributes']['TEXCOORD_0']).astype(np.float64)
    I = acc(pr['indices']).reshape(-1, 3).astype(np.int64)
    img = j['images'][j['textures'][j['materials'][pr['material']]['pbrMetallicRoughness']['baseColorTexture']['index']]['source']]
    bv = j['bufferViews'][img['bufferView']]
    import io
    tex = np.asarray(Image.open(io.BytesIO(d[b0 + bv['byteOffset']:b0 + bv['byteOffset'] + bv['byteLength']])).convert('RGB'))
    return P, N, UV, I, tex

def sample(tex, uv):
    h, w, _ = tex.shape
    x = np.clip((uv[:, 0] * w).astype(int), 0, w - 1); y = np.clip((uv[:, 1] * h).astype(int), 0, h - 1)
    return tex[y, x].astype(np.float64)


def write_cs(out, dp, nrm, out_col, dt, face):
    """src/WatchModel.cs: the same model for the C# mod (each point's tone an index into the palette)."""
    tones = []
    index = []
    for c in out_col:
        t = tuple(int(v) for v in c)
        if t not in tones: tones.append(t)
        index.append(tones.index(t))
    def rows(values, per, fmt):
        items = [fmt % v for v in values]
        return '\n'.join('        ' + ','.join(items[i:i + per]) + ',' for i in range(0, len(items), per))
    with open(out, 'w', newline='\n') as f:
        f.write('// The 3D watch (buildtools/watch/convert.py from a digital wristwatch made with Tripo for this mod), the\n')
        f.write('// same as the classic XIII mod\'s: in its own axes (X along the forearm, Y out of the face, Z along the\n')
        f.write('// strap at the face; 1 unit the strap\'s inner radius), %d points and %d triangles, each point\'s normal\n' % (len(dp), len(dt)))
        f.write('// (x127) and tone (an index into Palette); the face\'s rectangle. Generated: not edited by hand.\n')
        f.write('namespace XiiiXR;\n')
        f.write('internal static class WatchModel\n{\n')
        f.write('    internal const int Points=%d,Triangles=%d;\n' % (len(dp), len(dt)))
        f.write('    // its height (Y), X from, to, Z from, to\n')
        f.write('    internal static readonly float[] Face={%s};\n' % ','.join('%.4ff' % v for v in face))
        f.write('    internal static readonly byte[] Palette={%s};\n' % ','.join('%d' % v for t in tones for v in t))
        f.write('    internal static readonly float[] Position=\n    {\n%s\n    };\n' % rows([v for p in dp for v in p], 12, '%.4ff'))
        f.write('    internal static readonly sbyte[] Normal=\n    {\n%s\n    };\n' % rows([int(round(c * 127)) for n in nrm for c in n], 24, '%d'))
        f.write('    internal static readonly byte[] Tone=\n    {\n%s\n    };\n' % rows(index, 48, '%d'))
        f.write('    internal static readonly ushort[] Triangle=\n    {\n%s\n    };\n' % rows([int(v) for t in dt for v in t], 24, '%d'))
        f.write('}\n')

def main():
    src, decimator, out = sys.argv[1], sys.argv[2], sys.argv[3]
    target = int(sys.argv[4]) if len(sys.argv) > 4 else 3000
    P, N, UV, I, tex = load(src)
    C = sample(tex, UV)
    # the pieces; the sheet has two watches side by side and small views below: the left watch's pieces
    n = len(P)
    A = coo_matrix((np.ones(3 * len(I), np.int8), (I.ravel(), np.roll(I, 1, axis=1).ravel())), shape=(n, n))
    k, lab = connected_components(A, directed=False)
    cnt = np.bincount(lab, minlength=k)
    cen = np.stack([np.bincount(lab, weights=P[:, a], minlength=k) / np.maximum(cnt, 1) for a in range(3)], 1)
    keep = (cen[:, 1] > 0.18) & (cen[:, 0] < -0.05) & ~((cen[:, 1] < 0.25) & (cen[:, 2] > 0.15))
    vm = keep[lab]
    tris = I[vm[I].all(1)]
    print('watch: %d of %d pieces, %d triangles' % (keep.sum(), k, len(tris)), file=sys.stderr)
    # the face: the LCD's light olive, the plane through it
    lcd = vm & (C[:, 0] > 130) & (C[:, 1] > 130) & (C[:, 2] > 95) & (np.abs(C[:, 0] - C[:, 1]) < 30) & (C[:, 1] > C[:, 2] + 10)
    L = P[lcd]
    centre = np.median(L, 0)
    L = L[np.linalg.norm(L - centre, axis=1) < 0.2]
    m = L.mean(0)
    _, _, vt = np.linalg.svd(L - m, full_matrices=False)
    normal = vt[2]
    # the strap: the pieces away from the face, a ring; its axis the direction it varies least along
    S = P[vm & (np.linalg.norm(P - m, axis=1) > 0.2)]
    sm = S.mean(0)
    _, _, st = np.linalg.svd(S - sm, full_matrices=False)
    axis = st[2] / np.linalg.norm(st[2])
    # the ring's centre and radius: a circle in its plane through the strap (least squares)
    e1, e2 = st[0], st[1]
    u = (S - sm) @ e1; v = (S - sm) @ e2
    M = np.stack([u, v, np.ones_like(u)], 1)
    sol, *_ = np.linalg.lstsq(M, u * u + v * v, rcond=None)
    cu, cv = sol[0] / 2, sol[1] / 2
    ring_centre = sm + cu * e1 + cv * e2
    # the face's normal out from the axis; X the axis, Y the normal (square to it), Z = X x Y
    if np.dot(normal, m - ring_centre) < 0: normal = -normal
    Y = normal - axis * np.dot(normal, axis); Y /= np.linalg.norm(Y)
    X = axis
    Z = np.cross(X, Y)
    rel = P - ring_centre
    W = np.stack([rel @ X, rel @ Y, rel @ Z], 1)
    # the origin on the axis under the face; the inner radius from the strap (its nearest points to the axis)
    Sw = np.stack([(S - ring_centre) @ X, (S - ring_centre) @ Y, (S - ring_centre) @ Z], 1)
    radial = np.hypot(Sw[:, 1], Sw[:, 2])
    inner = np.percentile(radial, 3)
    x0 = ((m - ring_centre) @ X)
    W[:, 0] -= x0
    W /= inner
    NW = np.stack([N @ X, N @ Y, N @ Z], 1)
    Lw = np.stack([(L - ring_centre) @ X - x0, (L - ring_centre) @ Y, (L - ring_centre) @ Z], 1) / inner
    face_y = np.percentile(Lw[:, 1], 90)
    fx0, fx1 = np.percentile(Lw[:, 0], [1, 99]); fz0, fz1 = np.percentile(Lw[:, 2], [1, 99])
    print('inner radius %.4f, face at %.3f, %.3f..%.3f x %.3f..%.3f' % (inner, face_y, fx0, fx1, fz0, fz1), file=sys.stderr)
    # welded (the picture's seams split the points), cut down
    used = np.unique(tris)
    q = np.round(W[used] * 2e4).astype(np.int64)
    _, first, inv = np.unique(q, axis=0, return_index=True, return_inverse=True)
    remap = np.full(n, -1, np.int64); remap[used] = inv.ravel()
    wt = remap[tris]
    wt = wt[(wt[:, 0] != wt[:, 1]) & (wt[:, 1] != wt[:, 2]) & (wt[:, 0] != wt[:, 2])]
    wp = W[used][first]
    with tempfile.TemporaryDirectory() as tmp:
        fin = os.path.join(tmp, 'in.bin'); fout = os.path.join(tmp, 'out.bin')
        with open(fin, 'wb') as f:
            f.write(struct.pack('<II', len(wp), len(wt)))
            f.write(wp.astype('<f4').tobytes()); f.write(wt.astype('<u4').tobytes())
        subprocess.run([decimator, fin, fout, str(target)], check=True)
        d = open(fout, 'rb').read()
    nv, nt = struct.unpack_from('<II', d, 0)
    dp = np.frombuffer(d, '<f4', nv * 3, 8).reshape(nv, 3).astype(np.float64)
    dt = np.frombuffer(d, '<u4', nt * 3, 8 + nv * 12).reshape(nt, 3).astype(np.int64)
    # colours and normals from the nearest points of the model (its picture; the LCD's left to the face drawn on it)
    sel = np.unique(tris)
    tree = cKDTree(W[sel])
    dist, near = tree.query(dp, k=24)
    cols = C[sel][near].mean(1)
    nrm = NW[sel][near].mean(1)
    nrm /= np.maximum(np.linalg.norm(nrm, axis=1, keepdims=True), 1e-9)
    # the cel look: the colours in a few tones (the dark body, the grey of its edges, the metal, the red)
    lum = cols @ np.array([0.3, 0.59, 0.11])
    red = (cols[:, 0] > cols[:, 1] * 1.5) & (cols[:, 0] > 90)
    out_col = np.where(lum[:, None] < 45, [28, 28, 30], np.where(lum[:, None] < 80, [52, 52, 55], [120, 118, 112])).astype(np.float64)
    out_col[red] = [150, 32, 28]
    # the strap's inside and the case's back faces kept as they are; the triangles facing away by the normals
    if out.endswith('.cs'):
        write_cs(out, dp, nrm, out_col, dt, [face_y, fx0, fx1, fz0, fz1])
        return
    with open(out, 'w') as f:
        f.write('/* The VR watch (buildtools/watch/convert.py from a digital wristwatch made with Tripo for this mod): one\n')
        f.write(' * watch, in its own axes (X along the forearm, Y out of the face, Z along the strap at the face; 1 unit the\n')
        f.write(' * strap\'s inner radius), %d points and %d triangles; the face\'s rectangle. Generated: not edited by hand. */\n' % (nv, nt))
        f.write('#define WATCH_POINTS %d\n#define WATCH_TRIANGLES %d\n' % (nv, nt))
        f.write('static const float watch_face[5] = { %.4ff, %.4ff, %.4ff, %.4ff, %.4ff };   /* its height (Y), X from, to, Z from, to */\n' % (face_y, fx0, fx1, fz0, fz1))
        f.write('static const float watch_pos[WATCH_POINTS][3] = {\n')
        for i in range(nv): f.write('    { %.4ff, %.4ff, %.4ff },\n' % tuple(dp[i]))
        f.write('};\nstatic const signed char watch_normal[WATCH_POINTS][3] = {\n')
        for i in range(nv): f.write('    { %d, %d, %d },\n' % tuple(int(round(c * 127)) for c in nrm[i]))
        f.write('};\nstatic const unsigned char watch_colour[WATCH_POINTS][3] = {\n')
        for i in range(nv): f.write('    { %d, %d, %d },\n' % tuple(int(c) for c in out_col[i]))
        f.write('};\nstatic const unsigned short watch_tri[WATCH_TRIANGLES][3] = {\n')
        for i in range(nt): f.write('    { %d, %d, %d },\n' % tuple(dt[i]))
        f.write('};\n')
    np.savez(out + '.npz', pos=dp, tri=dt, col=out_col, nrm=nrm, face=np.array([face_y, fx0, fx1, fz0, fz1]), rawcol=cols)

if __name__ == '__main__':
    main()
