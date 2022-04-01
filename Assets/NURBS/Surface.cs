﻿using Unity.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

//https://pages.mtu.edu/~shene/COURSES/cs3621/NOTES/surface/bspline-construct.html
//https://pages.mtu.edu/~shene/COURSES/cs3621/NOTES/surface/bspline-properties.html
//https://pages.mtu.edu/~shene/COURSES/cs3621/NOTES/surface/bspline-de-boor.html

namespace kmty.NURBS {
    public class Surface: System.IDisposable {
        NativeArray<CP> cps;
        public NativeArray<CP> CPs => cps;
        public Vector2 min => new Vector2(NURBSSurface.KnotVec(order, order, lx, xknot), NURBSSurface.KnotVec(order, order, ly, yknot));
        public Vector2 max => new Vector2(NURBSSurface.KnotVec(lx, order, lx, xknot),    NURBSSurface.KnotVec(ly, order, ly, yknot));
        public int order, lx, ly;
        public bool xloop { get; private set; }
        public bool yloop { get; private set; }
        public KnotType xknot { get; private set; }
        public KnotType yknot { get; private set; }
        int idx(int x, int y) => x + y * lx;

        public Surface(CP[] cps, int order, int lx, int ly, SplineType xtype, SplineType ytype) {
            this.order = order;
            this.xloop = xtype == SplineType.Loop;
            this.yloop = ytype == SplineType.Loop;
            this.xknot = xtype == SplineType.Clamped ? KnotType.OpenUniform : KnotType.Uniform;
            this.yknot = ytype == SplineType.Clamped ? KnotType.OpenUniform : KnotType.Uniform;
            this.xknot = xknot;
            this.yknot = yknot;
            if (this.xloop && this.yloop) {
                var arr1d = new CP[(lx + order) * ly];
                for (int y = 0; y < ly; y++) {
                    for (int x = 0; x < lx + order; x++) {
                        var i1 = x % lx + y * lx;
                        var i2 = x + y * (lx + order);
                        arr1d[i2] = cps[i1];
                    }
                }
                var _cps = arr1d.ToList();
                for (int i = 0; i < order; i++) {
                    var row = new CP[lx + order];
                    System.Array.Copy(arr1d, i * (lx + order), row, 0, lx + order);
                    _cps.AddRange(row);
                }
                this.lx = lx + order;
                this.ly = ly + order;
                this.cps = new NativeArray<CP>(_cps.ToArray(), Allocator.Persistent);
            } else if (this.yloop) {
                var _cps = cps.ToList();
                for (int i = 0; i < order; i++) {
                    var row = new CP[lx];
                    System.Array.Copy(cps, i * lx, row, 0, lx);
                    //for (int j = 0; j < lx; j++) { row[lx - j - 1] = cps[i * lx + j]; }
                    _cps.AddRange(row);
                }
                this.lx = lx;
                this.ly = ly + order;
                this.cps = new NativeArray<CP>(_cps.ToArray(), Allocator.Persistent);
            } else if (this.xloop) {
                var arr1d = new CP[(lx + order) * ly];
                for (int y = 0; y < ly; y++) {
                    for (int x = 0; x < lx + order; x++) {
                        var i1 = x % lx + y * lx;
                        var i2 = x + y * (lx + order);
                        arr1d[i2] = cps[i1];
                    }
                }
                this.lx = lx + order;
                this.ly = ly;
                this.cps = new NativeArray<CP>(arr1d, Allocator.Persistent);

            } else {
                this.lx = lx;
                this.ly = ly;
                this.cps = new NativeArray<CP>(cps.ToArray(), Allocator.Persistent);
            }
        }

        public bool GetCurve(float tx, float ty, out Vector3 v){
            var fx = tx >= min.x && tx <= max.x;
            var fy = ty >= min.y && ty <= max.y;
            v = NURBSSurface.GetCurve(cps, tx, ty, order, lx, ly, xknot, yknot);
            return fx && fy;

        }
        public void UpdateCP(Vector2Int i, CP cp) {
            cps[idx(i.x, i.y)] = cp;
            if (xloop && i.x < order) cps[idx(lx - order + i.x, i.y)] = cp;
            if (yloop && i.y < order) cps[idx(i.x, ly - order + i.y)] = cp;
        }
        public bool IsAccessbile => cps.IsCreated;
        public void Dispose() => cps.Dispose();
    }

    public static class NURBSSurface {

        public static Vector3 GetCurve(NativeArray<CP> cps, float tx, float ty, int order, int lx, int ly, KnotType xknot, KnotType yknot) {
            var frac = Vector3.zero;
            var deno = 1e-10f;
            tx = Mathf.Min(tx, 1f - 1e-5f);
            ty = Mathf.Min(ty, 1f - 1e-5f);
            for (int y = 0; y < ly; y++) {
                for (int x = 0; x < lx; x++) {
                    var bf = BasisFunc(x, order, order, tx, lx, xknot) * BasisFunc(y, order, order, ty, ly, yknot);
                    var cp = cps[x + y * lx];
                    frac += cp.pos * bf * cp.weight;
                    deno += bf * cp.weight;
                }
            }
            return frac / deno;
        }

        public static float BasisFunc(int j, int k, int order, float t, int l, KnotType knot) {
            if (k == 0) {
                return (t >= KnotVec(j, order, l, knot) && t < KnotVec(j + 1, order, l, knot)) ? 1 : 0;
            }
            else {
                var d1 = KnotVec(j + k, order, l, knot) - KnotVec(j, order, l, knot);
                var d2 = KnotVec(j + k + 1, order, l, knot) - KnotVec(j + 1, order, l, knot);
                var c1 = d1 != 0 ? (t - KnotVec(j, order, l, knot)) / d1 : 0;
                var c2 = d2 != 0 ? (KnotVec(j + k + 1, order, l, knot) - t) / d2 : 0;
                return c1 * BasisFunc(j, k - 1, order, t, l, knot) + c2 * BasisFunc(j + 1, k - 1, order, t, l, knot);
            }
        }

        public static float KnotVec(int j, int order, int l, KnotType t) {
            if (t == KnotType.Uniform)
                return UniformKnotVec(j, l + order + 1);
            else
                return OpenUniformKnotVec(j, l + order + 1, order);
        }

        static float UniformKnotVec(int j, int knotNum) {
            var t0 = 0f;
            var t1 = 1f;
            return t0 + (t1 - t0) / (knotNum - 1) * j;
        }

        static float OpenUniformKnotVec(int j, int knotNum, int order) {
            if (j <= order)               return 0f;
            if (j >= knotNum - 1 - order) return 1f;
            return (float)j / (knotNum - order + 1);
        }
    }
}
