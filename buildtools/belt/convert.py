#!/usr/bin/env python3
"""The VR ammunition belt from a GLB (an ammo belt made with Tripo for the mods: a belt round the waist, a
pouch in front, two rows of eight shotgun shells, a buckle behind): welded, cut down (../watch/decimate.c),
coloured from its picture in a few tones, and worn as made (its own shape and size): the pouch in the middle
of the front, a row of shells on each hip (a right-hander's left hand takes the rounds from the left hip first
while the right holds the gun; mirrored at runtime for a left-hander). Written as C (the classic mod: beltmesh.inc) or,
for an output ending in .cs, as C# (the remake: BeltModel.cs, with its shells, which the reserve shows).

  python3 convert.py model.glb decimate-binary out.inc|out.cs [triangles]

Belt space (metres, left-handed as both games'): X to the wearer's right, Y up, Z forward; the origin at the
middle of the waist, at the belt's height. The model's own: Y up, its front (the pouch) +Z, 1 unit 0.47 m
(its shells' brass heads 22.5 mm across)."""
import json, os, struct, subprocess, sys, tempfile, io
import numpy as np
from PIL import Image
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import connected_components
from scipy.spatial import cKDTree

POUCH_ANGLE = 0.0                    # where the pouch sits, degrees from the front (- to the left): in the middle
HEAD_DIAMETER = .0225                # a 12-gauge shell's brass head (metres)

def load(path):
    d = open(path, 'rb').read()
    n, _ = struct.unpack_from('<II', d, 12)
    j = json.loads(d[20:20 + n]); b0 = 20 + n + 8
    def acc(i):
        a = j['accessors'][i]; bv = j['bufferViews'][a['bufferView']]
        dt = {5126: np.float32, 5125: np.uint32, 5123: np.uint16}[a['componentType']]
        k = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3}[a['type']]
        arr = np.frombuffer(d, dtype=dt, count=a['count'] * k, offset=b0 + bv['byteOffset'] + a.get('byteOffset', 0))
        return arr.reshape(a['count'], k) if k > 1 else arr
    pr = j['meshes'][0]['primitives'][0]
    P = acc(pr['attributes']['POSITION']).astype(np.float64); N = acc(pr['attributes']['NORMAL']).astype(np.float64)
    UV = acc(pr['attributes']['TEXCOORD_0']).astype(np.float64); I = acc(pr['indices']).reshape(-1, 3).astype(np.int64)
    img = j['images'][j['textures'][j['materials'][pr['material']]['pbrMetallicRoughness']['baseColorTexture']['index']]['source']]
    bv = j['bufferViews'][img['bufferView']]
    tex = np.asarray(Image.open(io.BytesIO(d[b0 + bv['byteOffset']:b0 + bv['byteOffset'] + bv['byteLength']])).convert('RGB'))
    return P, N, UV, I, tex

def sample(tex, uv):
    h, w, _ = tex.shape
    x = np.clip((uv[:, 0] * w).astype(int), 0, w - 1); y = np.clip((uv[:, 1] * h).astype(int), 0, h - 1)
    return tex[y, x].astype(np.float64)

def kmeans(X, k, iters=40):
    """deterministic k-means (the farthest-point start)"""
    c = [X[np.argmin(X.sum(1))]]
    for _ in range(1, k):
        d = np.min(((X[:, None, :] - np.array(c)[None]) ** 2).sum(2), 1)
        c.append(X[np.argmax(d)])
    c = np.array(c)
    for _ in range(iters):
        lab = np.argmin(((X[:, None, :] - c[None]) ** 2).sum(2), 1)
        c = np.array([X[lab == i].mean(0) if (lab == i).any() else c[i] for i in range(k)])
    return c, lab

def main():
    src, decimator, out = sys.argv[1], sys.argv[2], sys.argv[3]
    target = int(sys.argv[4]) if len(sys.argv) > 4 else 8000
    P, N, UV, I, tex = load(src)
    C = sample(tex, UV)
    # welded (the picture's seams split the points)
    q = np.round(P * 2e4).astype(np.int64)
    _, first, inv = np.unique(q, axis=0, return_index=True, return_inverse=True)
    inv = inv.ravel(); W = P[first]
    T = inv[I]; T = T[(T[:, 0] != T[:, 1]) & (T[:, 1] != T[:, 2]) & (T[:, 0] != T[:, 2])]
    n = len(W)
    cnt = np.bincount(inv, minlength=n)
    Cw = np.zeros((n, 3)); np.add.at(Cw, inv, C); Cw /= np.maximum(cnt, 1)[:, None]
    Nw = np.zeros((n, 3)); np.add.at(Nw, inv, N); Nw /= np.maximum(np.linalg.norm(Nw, axis=1, keepdims=True), 1e-9)
    # the shells: the brass heads (yellow), one piece of the surface each, 0.04-0.06 across
    r, g, b = Cw[:, 0], Cw[:, 1], Cw[:, 2]
    brass = (r > 120) & (g > 95) & (b < 0.75 * g) & (r > b + 45)
    tb = T[brass[T].all(1)]
    A = coo_matrix((np.ones(3 * len(tb), np.int8), (tb.ravel(), np.roll(tb, 1, axis=1).ravel())), shape=(n, n))
    _, lab = connected_components(A, directed=False)
    shells = []
    parts = [W[brass & (lab == c)] for c in np.unique(lab[brass])]
    parts = [p for p in parts if len(p) >= 200 and (p.max(0) - p.min(0))[1] < 0.06]
    # a head in more than one piece (a seam of its picture): the pieces within one head of each other, one shell
    groups = []
    for p in parts:
        c = p.mean(0)
        for gp in groups:
            if np.hypot(*(gp['c'] - c)[[0, 2]]) < 0.035: gp['p'] = np.vstack([gp['p'], p]); gp['c'] = gp['p'].mean(0); break
        else: groups.append(dict(p=p, c=c))
    for gp in groups:
        p = gp['p']; size = p.max(0) - p.min(0)
        c = p.mean(0)
        if len(p) < 600 or not (0.025 < size[0] < 0.065 and 0.025 < size[2] < 0.065): continue
        if abs(np.degrees(np.arctan2(c[0], c[2]))) > 125: continue   # the buckle's brass, behind
        shells.append(dict(x=float(p[:, 0].mean()), z=float(p[:, 2].mean()), y0=float(p[:, 1].min()), y1=float(p[:, 1].max()), radius=float((size[0] + size[2]) / 4)))
    shells.sort(key=lambda s: np.arctan2(s['x'], s['z']))
    head = np.mean([2 * s['radius'] for s in shells])
    scale = HEAD_DIAMETER / head
    print('shells %d, heads %.4f across: %.3f m a unit' % (len(shells), head, scale), file=sys.stderr)
    # the belt's inside round it (its inner surface: where the body is), angle by angle: a closed curve
    ang = np.arctan2(W[:, 0], W[:, 2]); rad = np.hypot(W[:, 0], W[:, 2])
    bins = 720; bi = ((ang + np.pi) / (2 * np.pi) * bins).astype(int) % bins
    inner = np.array([np.percentile(rad[bi == k], 2) if (bi == k).sum() > 20 else np.nan for k in range(bins)])
    good = ~np.isnan(inner); inner = np.interp(np.arange(bins), np.arange(bins)[good], inner[good], period=bins)
    inner = np.array([np.median(np.take(inner, range(k - 6, k + 7), mode='wrap')) for k in range(bins)])
    tb_ = (np.arange(bins) + 0.5) / bins * 2 * np.pi - np.pi
    curve = np.stack([inner * np.sin(tb_), inner * np.cos(tb_)], 1)          # (x, z)
    seg = np.linalg.norm(np.roll(curve, -1, 0) - curve, axis=1)
    arc = np.concatenate([[0], np.cumsum(seg)[:-1]]); length = seg.sum()
    tang = np.roll(curve, -1, 0) - np.roll(curve, 1, 0); tang /= np.linalg.norm(tang, axis=1, keepdims=True)
    normal = np.stack([tang[:, 1], -tang[:, 0]], 1)
    normal *= np.sign(np.einsum('ij,ij->i', normal, curve))[:, None]          # outward
    near_bin = np.argmin(np.abs(tb_))                                          # the front (the pouch)
    y_ref = float(np.median(W[rad < np.interp(bi, np.arange(bins), inner) + 0.01][:, 1]))
    print('inside %.3f..%.3f, %.3f round, belt height %.3f' % (inner.min(), inner.max(), length, y_ref), file=sys.stderr)
    # cut down
    used = np.unique(T)
    remap = np.full(n, -1, np.int64); remap[used] = np.arange(len(used))
    wp = W[used]; wt = remap[T]
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
    # colours and normals from the nearest points of the model
    tree = cKDTree(wp)
    _, near = tree.query(dp, k=16)
    cols = Cw[used][near].mean(1)
    # the canvas's picture is grainy: its colours averaged over a wider patch (the tones in whole areas); the brass kept sharp
    _, wide = tree.query(dp, k=160)
    soft = Cw[used][wide].mean(1)
    nrm = Nw[used][near].mean(1); nrm /= np.maximum(np.linalg.norm(nrm, axis=1, keepdims=True), 1e-9)
    cr = np.cross(dp[dt[:, 1]] - dp[dt[:, 0]], dp[dt[:, 2]] - dp[dt[:, 0]])
    agree = (np.einsum('ij,ij->i', cr, nrm[dt].sum(1)) > 0).mean()
    if agree < 0.5: dt = dt[:, ::-1]; agree = 1 - agree
    print('triangles facing out by their normals: %.3f' % agree, file=sys.stderr)
    # the tones: a few colours (the cel look): the brass heads one, the canvas and straps the others
    isb = (cols[:, 0] > 110) & (cols[:, 1] > 88) & (cols[:, 2] < 0.75 * cols[:, 1]) & (cols[:, 0] > cols[:, 2] + 40)
    centres, labs = kmeans(soft[~isb], 3)
    palette = [tuple(int(round(v)) for v in c) for c in centres] + [tuple(int(round(v)) for v in cols[isb].mean(0))]
    tone = np.empty(nv, np.int64); tone[~isb] = labs; tone[isb] = len(palette) - 1
    order = np.argsort([sum(c) for c in palette])            # darkest first
    rank = np.empty_like(order); rank[order] = np.arange(len(order))
    palette = [palette[i] for i in order]; tone = rank[tone]
    mixed = int((tone[dt].min(1) != tone[dt].max(1)).sum())
    print('palette', palette, '%d of %d triangles between tones' % (mixed, len(dt)), file=sys.stderr)
    # each shell's head: its triangles (hidden when the round is used), the loop's top under it
    dang = np.arctan2(dp[:, 0], dp[:, 2])
    shell_of = np.full(nt, -1, np.int64)
    for i, s in enumerate(shells):
        axis = np.hypot(dp[:, 0] - s['x'], dp[:, 2] - s['z'])
        inside = (axis < s['radius'] * 1.35) & (dp[:, 1] > s['y0'] - 0.003)
        shell_of[inside[dt].all(1) & (shell_of < 0)] = i
    # worn: as made (its own shape and size), turned about the waist by POUCH_ANGLE (the pouch in front): the middle
    # of its inside at the middle of the waist
    cx = (curve[:, 0].max() + curve[:, 0].min()) / 2; cz = (curve[:, 1].max() + curve[:, 1].min()) / 2
    turn = np.radians(POUCH_ANGLE)
    def place(x, y, z):
        u, w = (x - cx) * scale, (z - cz) * scale
        return np.stack([u * np.cos(turn) + w * np.sin(turn), (y - y_ref) * scale, -u * np.sin(turn) + w * np.cos(turn)], -1)
    bp = place(dp[:, 0], dp[:, 1], dp[:, 2])
    bn = np.stack([nrm[:, 0] * np.cos(turn) + nrm[:, 2] * np.sin(turn), nrm[:, 1], -nrm[:, 0] * np.sin(turn) + nrm[:, 2] * np.cos(turn)], -1)
    span = (curve.max(0) - curve.min(0)) * scale
    print('the waist inside it %.3f x %.3f m (%.3f m round)' % (span[0], span[1], length * scale), file=sys.stderr)
    sh = []
    for s in shells:
        top = place(np.array([s['x']]), np.array([s['y1']]), np.array([s['z']]))
        low = place(np.array([s['x']]), np.array([s['y0']]), np.array([s['z']]))
        sh.append(dict(top=top[0], low=low[0], radius=s['radius'] * scale))
    # the pouch: the points standing out most near the front of the model
    pm = (np.abs(dang) < np.radians(30)) & (np.hypot(dp[:, 0], dp[:, 2]) - inner[(((dang + np.pi) / (2 * np.pi) * bins).astype(int)) % bins] > 0.05)
    pouch = bp[pm]; pc = (pouch.max(0) + pouch.min(0)) / 2; ph = (pouch.max(0) - pouch.min(0)) / 2
    # the order the shells are shown in, a round each of the reserve: the easiest to reach first
    reach = np.array([-0.12, 0.05, 0.16])     # where a right-hander's left hand goes for a round: the front of the left hip
    use = sorted(range(len(sh)), key=lambda i: np.linalg.norm(sh[i]['top'] - reach))
    print('pouch at %s (half size %s), %d shells' % (pc.round(3), ph.round(3), len(sh)), file=sys.stderr)
    if out.endswith('.cs'): write_cs(out, bp, bn, tone, palette, dt, sh, use, shell_of, pc, ph)
    else: write_c(out, bp, bn, tone, palette, dt, pc, ph)
    np.savez(os.path.join(tempfile.gettempdir(), 'belt-preview.npz'), pos=bp, tri=dt, tone=tone, palette=np.array(palette), shell_of=shell_of,
             tops=np.array([s['top'] for s in sh]), pouch=pc, raw=dp)

HEADER = ('The VR ammunition belt (buildtools/belt/convert.py from an ammo belt made with Tripo for the mods): worn, in belt\n'
          'space (metres: X to the right, Y up, Z forward, the origin at the middle of the waist at the belt\'s height), the\n'
          'pouch in front, a row of shells on each hip (mirrored for a left-hander), %d points and %d triangles, each\n'
          'point\'s normal (x127) and '
          'tone. Generated: not edited by hand.')

def write_c(out, bp, bn, tone, palette, dt, pc, ph):
    with open(out, 'w', newline='\n') as f:
        f.write('/* ' + (HEADER % (len(bp), len(dt))).replace('\n', '\n * ') + ' */\n')
        f.write('#define BELT_POINTS %d\n#define BELT_TRIANGLES %d\n#define BELT_TONES %d\n' % (len(bp), len(dt), len(palette)))
        f.write('static const float belt_pouch[6] = { %s };   /* the pouch\'s middle, its half size */\n' % ', '.join('%.4ff' % v for v in list(pc) + list(ph)))
        f.write('static const unsigned char belt_palette[BELT_TONES][3] = { %s };\n' % ', '.join('{ %d, %d, %d }' % c for c in palette))
        f.write('static const float belt_pos[BELT_POINTS][3] = {\n')
        for p in bp: f.write('    { %.4ff, %.4ff, %.4ff },\n' % tuple(p))
        f.write('};\nstatic const signed char belt_normal[BELT_POINTS][3] = {\n')
        for nn in bn: f.write('    { %d, %d, %d },\n' % tuple(int(round(c * 127)) for c in nn))
        f.write('};\nstatic const unsigned char belt_tone[BELT_POINTS] = {\n')
        for i in range(0, len(tone), 32): f.write('    ' + ', '.join('%d' % t for t in tone[i:i + 32]) + ',\n')
        f.write('};\nstatic const unsigned short belt_tri[BELT_TRIANGLES][3] = {\n')
        for t in dt: f.write('    { %d, %d, %d },\n' % tuple(t))
        f.write('};\n')

def write_cs(out, bp, bn, tone, palette, dt, sh, use, shell_of, pc, ph):
    def rows(values, per, fmt):
        items = [fmt % v for v in values]
        return '\n'.join('        ' + ','.join(items[i:i + per]) + ',' for i in range(0, len(items), per))
    with open(out, 'w', newline='\n') as f:
        f.write('// ' + (HEADER % (len(bp), len(dt))).replace('\n', '\n// ') + '\n')
        f.write('// The shells: each one\'s brass head (its triangles: ShellOf), its top and the loop\'s top under it, in the\n// order a reserve uses them (Use: the first shown first).\n')
        f.write('namespace XiiiXR;\ninternal static class BeltModel\n{\n')
        f.write('    internal const int Points=%d,Triangles=%d,Shells=%d;\n' % (len(bp), len(dt), len(sh)))
        f.write('    internal static readonly float[] Pouch={%s};   // its middle, its half size\n' % ','.join('%.4ff' % v for v in list(pc) + list(ph)))
        f.write('    internal static readonly byte[] Palette={%s};\n' % ','.join('%d' % v for c in palette for v in c))
        f.write('    internal static readonly float[] ShellTop={%s};\n' % ','.join('%.4ff' % v for s in sh for v in s['top']))
        f.write('    internal static readonly float[] ShellLow={%s};\n' % ','.join('%.4ff' % v for s in sh for v in s['low']))
        f.write('    internal static readonly float[] ShellRadius={%s};\n' % ','.join('%.4ff' % s['radius'] for s in sh))
        f.write('    internal static readonly byte[] Use={%s};\n' % ','.join('%d' % i for i in use))
        f.write('    internal static readonly float[] Position=\n    {\n%s\n    };\n' % rows([v for p in bp for v in p], 12, '%.4ff'))
        f.write('    internal static readonly sbyte[] Normal=\n    {\n%s\n    };\n' % rows([int(round(c * 127)) for nn in bn for c in nn], 24, '%d'))
        f.write('    internal static readonly byte[] Tone=\n    {\n%s\n    };\n' % rows(list(tone), 48, '%d'))
        f.write('    internal static readonly ushort[] Triangle=\n    {\n%s\n    };\n' % rows([int(v) for t in dt for v in t], 24, '%d'))
        f.write('    internal static readonly sbyte[] ShellOf=\n    {\n%s\n    };\n' % rows(list(shell_of), 48, '%d'))
        f.write('}\n')

if __name__ == '__main__':
    main()
