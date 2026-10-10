/* Quadric mesh decimation (Garland & Heckbert's error quadrics, collapsed in passes of a growing error
 * threshold): a triangle mesh in, the same shape in fewer triangles out. For buildtools/watch/convert.py.
 *   decimate in.bin out.bin target
 * in/out: u32 vertex count, u32 triangle count, float x,y,z per vertex, u32 a,b,c per triangle.
 * Built with any C compiler (cc -O2 decimate.c -lm). */
#include <math.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

typedef struct { double m[10]; } Quad;
typedef struct { double p[3]; Quad q; int tstart, tcount, border; } Vert;
typedef struct { int v[3]; double err[4]; int deleted, dirty; double n[3]; } Tri;
typedef struct { int tid, corner; } Ref;

static Vert *verts; static Tri *tris; static Ref *refs;
static int nv, nt, nrefs, caprefs;

static Quad plane(double a, double b, double c, double d)
{
    Quad q = { { a * a, a * b, a * c, a * d, b * b, b * c, b * d, c * c, c * d, d * d } };
    return q;
}
static void qadd(Quad *a, const Quad *b) { for (int i = 0; i < 10; i++) a->m[i] += b->m[i]; }
static double qdet(const Quad *q, int a11, int a12, int a13, int a21, int a22, int a23, int a31, int a32, int a33)
{
    const double *m = q->m;
    return m[a11] * m[a22] * m[a33] + m[a13] * m[a21] * m[a32] + m[a12] * m[a23] * m[a31] - m[a13] * m[a22] * m[a31] - m[a11] * m[a23] * m[a32] - m[a12] * m[a21] * m[a33];
}
static double verr(const Quad *q, double x, double y, double z)
{
    const double *m = q->m;
    return m[0] * x * x + 2 * m[1] * x * y + 2 * m[2] * x * z + 2 * m[3] * x + m[4] * y * y + 2 * m[5] * y * z + 2 * m[6] * y + m[7] * z * z + 2 * m[8] * z + m[9];
}
static void sub3(const double *a, const double *b, double *r) { for (int i = 0; i < 3; i++) r[i] = a[i] - b[i]; }
static void cross3(const double *a, const double *b, double *r) { r[0] = a[1] * b[2] - a[2] * b[1]; r[1] = a[2] * b[0] - a[0] * b[2]; r[2] = a[0] * b[1] - a[1] * b[0]; }
static double dot3(const double *a, const double *b) { return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]; }
static void norm3(double *a) { double l = sqrt(dot3(a, a)); if (l > 0) for (int i = 0; i < 3; i++) a[i] /= l; }

static double edge_error(int i1, int i2, double *out)
{
    Quad q = verts[i1].q;
    qadd(&q, &verts[i2].q);
    int border = verts[i1].border & verts[i2].border;
    double det = qdet(&q, 0, 1, 2, 1, 4, 5, 2, 5, 7);
    if (det != 0 && !border) {
        out[0] = -1 / det * qdet(&q, 1, 2, 3, 4, 5, 6, 5, 7, 8);
        out[1] = 1 / det * qdet(&q, 0, 2, 3, 1, 5, 6, 2, 7, 8);
        out[2] = -1 / det * qdet(&q, 0, 1, 3, 1, 4, 6, 2, 5, 8);
        return verr(&q, out[0], out[1], out[2]);
    }
    const double *p1 = verts[i1].p, *p2 = verts[i2].p;
    double p3[3] = { (p1[0] + p2[0]) / 2, (p1[1] + p2[1]) / 2, (p1[2] + p2[2]) / 2 };
    double e1 = verr(&q, p1[0], p1[1], p1[2]), e2 = verr(&q, p2[0], p2[1], p2[2]), e3 = verr(&q, p3[0], p3[1], p3[2]);
    double e = e1 < e2 ? (e1 < e3 ? e1 : e3) : (e2 < e3 ? e2 : e3);
    const double *best = e == e1 ? p1 : e == e2 ? p2 : p3;
    memcpy(out, best, sizeof(double) * 3);
    return e;
}

/* would moving vertex i0 (collapsing edge i0-i1) to p turn a triangle over? (deleted: its triangles that go) */
static int flipped(const double *p, int i1, const Vert *v0, int *deleted)
{
    for (int k = 0; k < v0->tcount; k++) {
        Tri *t = &tris[refs[v0->tstart + k].tid];
        if (t->deleted) continue;
        int s = refs[v0->tstart + k].corner, id1 = t->v[(s + 1) % 3], id2 = t->v[(s + 2) % 3];
        if (id1 == i1 || id2 == i1) { deleted[k] = 1; continue; }
        double d1[3], d2[3], n[3];
        sub3(verts[id1].p, p, d1); norm3(d1);
        sub3(verts[id2].p, p, d2); norm3(d2);
        if (fabs(dot3(d1, d2)) > 0.999) return 1;
        cross3(d1, d2, n); norm3(n);
        deleted[k] = 0;
        if (dot3(n, t->n) < 0.2) return 1;
    }
    return 0;
}
static void push_ref(Ref r)
{
    if (nrefs == caprefs) { caprefs = caprefs ? caprefs * 2 : 1024; refs = realloc(refs, (size_t)caprefs * sizeof(Ref)); }
    refs[nrefs++] = r;
}
static void update_tris(int i0, Vert *v, const int *deleted, int *dcount)
{
    double p[3];
    for (int k = 0; k < v->tcount; k++) {
        Ref r = refs[v->tstart + k];
        Tri *t = &tris[r.tid];
        if (t->deleted) continue;
        if (deleted[k]) { t->deleted = 1; (*dcount)++; continue; }
        t->v[r.corner] = i0;
        t->dirty = 1;
        for (int j = 0; j < 3; j++) t->err[j] = edge_error(t->v[j], t->v[(j + 1) % 3], p);
        t->err[3] = fmin(t->err[0], fmin(t->err[1], t->err[2]));
        push_ref(r);
    }
}
static void update_mesh(int iteration)
{
    if (iteration > 0) {
        int dst = 0;
        for (int i = 0; i < nt; i++) if (!tris[i].deleted) tris[dst++] = tris[i];
        nt = dst;
    }
    for (int i = 0; i < nv; i++) { verts[i].tstart = 0; verts[i].tcount = 0; }
    for (int i = 0; i < nt; i++) for (int j = 0; j < 3; j++) verts[tris[i].v[j]].tcount++;
    int tstart = 0;
    for (int i = 0; i < nv; i++) { verts[i].tstart = tstart; tstart += verts[i].tcount; verts[i].tcount = 0; }
    nrefs = 0;
    if (caprefs < tstart) { caprefs = tstart; refs = realloc(refs, (size_t)caprefs * sizeof(Ref)); }
    for (int i = 0; i < nt; i++) for (int j = 0; j < 3; j++) {
        Vert *v = &verts[tris[i].v[j]];
        refs[v->tstart + v->tcount].tid = i; refs[v->tstart + v->tcount].corner = j; v->tcount++;
    }
    nrefs = tstart;
    if (iteration == 0) {
        /* the borders: an edge only one triangle has */
        int *vcount = calloc(64, sizeof(int)), *vids = calloc(64, sizeof(int)), cap = 64;
        for (int i = 0; i < nv; i++) verts[i].border = 0;
        for (int i = 0; i < nv; i++) {
            Vert *v = &verts[i];
            int n = 0;
            for (int k = 0; k < v->tcount; k++) {
                Tri *t = &tris[refs[v->tstart + k].tid];
                for (int j = 0; j < 3; j++) {
                    int id = t->v[j], m = 0;
                    while (m < n && vids[m] != id) m++;
                    if (m == n) { if (n == cap) { cap *= 2; vcount = realloc(vcount, cap * sizeof(int)); vids = realloc(vids, cap * sizeof(int)); } vcount[n] = 1; vids[n] = id; n++; }
                    else vcount[m]++;
                }
            }
            for (int m = 0; m < n; m++) if (vcount[m] == 1) verts[vids[m]].border = 1;
        }
        free(vcount); free(vids);
        for (int i = 0; i < nv; i++) memset(&verts[i].q, 0, sizeof(Quad));
        for (int i = 0; i < nt; i++) {
            Tri *t = &tris[i];
            double e1[3], e2[3], n[3];
            sub3(verts[t->v[1]].p, verts[t->v[0]].p, e1); sub3(verts[t->v[2]].p, verts[t->v[0]].p, e2);
            cross3(e1, e2, n); norm3(n);
            memcpy(t->n, n, sizeof(n));
            Quad q = plane(n[0], n[1], n[2], -dot3(n, verts[t->v[0]].p));
            for (int j = 0; j < 3; j++) qadd(&verts[t->v[j]].q, &q);
        }
        double p[3];
        for (int i = 0; i < nt; i++) {
            Tri *t = &tris[i];
            for (int j = 0; j < 3; j++) t->err[j] = edge_error(t->v[j], t->v[(j + 1) % 3], p);
            t->err[3] = fmin(t->err[0], fmin(t->err[1], t->err[2]));
        }
    }
}

int main(int argc, char **argv)
{
    if (argc < 4) { fprintf(stderr, "decimate in.bin out.bin target\n"); return 2; }
    FILE *f = fopen(argv[1], "rb");
    if (!f) return 1;
    unsigned h[2];
    if (fread(h, 4, 2, f) != 2) return 1;
    nv = (int)h[0]; nt = (int)h[1];
    verts = calloc((size_t)nv, sizeof(Vert)); tris = calloc((size_t)nt, sizeof(Tri));
    for (int i = 0; i < nv; i++) { float p[3]; if (fread(p, 4, 3, f) != 3) return 1; for (int j = 0; j < 3; j++) verts[i].p[j] = p[j]; }
    for (int i = 0; i < nt; i++) { unsigned v[3]; if (fread(v, 4, 3, f) != 3) return 1; for (int j = 0; j < 3; j++) tris[i].v[j] = (int)v[j]; }
    fclose(f);
    int target = atoi(argv[3]), deleted = 0, start = nt;
    int *del0 = NULL, *del1 = NULL, capdel = 0;
    for (int iteration = 0; iteration < 200; iteration++) {
        if (start - deleted <= target) break;
        if (iteration % 5 == 0) { update_mesh(iteration); start = nt; deleted = 0; }
        for (int i = 0; i < nt; i++) tris[i].dirty = 0;
        double threshold = 1e-9 * pow((double)iteration + 3, 7);
        for (int i = 0; i < nt; i++) {
            Tri *t = &tris[i];
            if (t->err[3] > threshold || t->deleted || t->dirty) continue;
            for (int j = 0; j < 3; j++) {
                if (t->err[j] >= threshold) continue;
                int i0 = t->v[j], i1 = t->v[(j + 1) % 3];
                Vert *v0 = &verts[i0], *v1 = &verts[i1];
                if (v0->border || v1->border) continue;   /* the open edges kept */
                double p[3];
                edge_error(i0, i1, p);
                int need = v0->tcount > v1->tcount ? v0->tcount : v1->tcount;
                if (need > capdel) { capdel = need * 2; del0 = realloc(del0, capdel * sizeof(int)); del1 = realloc(del1, capdel * sizeof(int)); }
                if (flipped(p, i1, v0, del0) || flipped(p, i0, v1, del1)) continue;
                memcpy(v0->p, p, sizeof(p));
                qadd(&v0->q, &v1->q);
                int tstart = nrefs;
                update_tris(i0, v0, del0, &deleted);
                update_tris(i0, v1, del1, &deleted);
                v0 = &verts[i0];
                int tcount = nrefs - tstart;
                if (tcount <= v0->tcount) { if (tcount) memmove(&refs[v0->tstart], &refs[tstart], (size_t)tcount * sizeof(Ref)); }
                else v0->tstart = tstart;
                v0->tcount = tcount;
                break;
            }
            if (start - deleted <= target) break;
        }
    }
    /* the triangles left, their vertices renumbered */
    int *map = malloc((size_t)nv * sizeof(int)), outv = 0, outt = 0;
    for (int i = 0; i < nv; i++) map[i] = -1;
    for (int i = 0; i < nt; i++) if (!tris[i].deleted) { outt++; for (int j = 0; j < 3; j++) { int v = tris[i].v[j]; if (map[v] < 0) map[v] = outv++; } }
    FILE *o = fopen(argv[2], "wb");
    unsigned oh[2] = { (unsigned)outv, (unsigned)outt };
    fwrite(oh, 4, 2, o);
    float *pv = malloc((size_t)outv * 12);
    for (int i = 0; i < nv; i++) if (map[i] >= 0) for (int j = 0; j < 3; j++) pv[map[i] * 3 + j] = (float)verts[i].p[j];
    fwrite(pv, 12, (size_t)outv, o);
    for (int i = 0; i < nt; i++) if (!tris[i].deleted) { unsigned v[3] = { (unsigned)map[tris[i].v[0]], (unsigned)map[tris[i].v[1]], (unsigned)map[tris[i].v[2]] }; fwrite(v, 4, 3, o); }
    fclose(o);
    fprintf(stderr, "decimate: %d vertices %d triangles -> %d vertices %d triangles\n", nv, start, outv, outt);
    return 0;
}
