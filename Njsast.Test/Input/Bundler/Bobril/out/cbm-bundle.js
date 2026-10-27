(e => {
    var t, n, r, i, a, d, o, l, c, s, u, f, h, p, m, v, b, g, y, x, C, w, _, k, S, E, I, N, D, P, A, R, F, j, B, O, T, M, V, U, H, L, K, W, X, Y, z, $, Q, q, G, Z, J, ee, te, ne, re, ie, ae, de, oe, le, ce, se, ue, fe, he, pe, me, ve, be, ge, ye, xe, Ce, we, _e, ke, Se, Ee, Ie, Ne, De, Pe, Ae, Re, Fe, je, Be, Oe, Te, Me, Ve, Ue, He, Le, Ke, We, Xe, Ye, ze, $e, Qe, qe, Ge, Ze, Je, et, tt, nt, rt, it, at, dt, ot, lt, ct, st, ut, ft, ht, pt, mt, vt, bt, gt, yt, xt, Ct, wt, _t, kt, St, Et, It, Nt, Dt = {}, Pt, At, Rt, Ft, jt, Bt, Ot, Tt;
    t = function(e) {
        var t = typeof Symbol === "function" && e[Symbol.iterator], n = 0;
        if (t) return t.call(e);
        return {
            next: function() {
                e && n >= e.length && (e = void 0);
                return {
                    value: e && e[n++],
                    done: !e
                };
            }
        };
    };
    n = function(e, t) {
        var n, r, i, a, d;
        n = typeof Symbol === "function" && e[Symbol.iterator];
        if (!n) return e;
        r = n.call(e);
        i = [];
        try {
            while ((t === void 0 || t-- > 0) && !(d = r.next()).done) i.push(d.value);
        } catch (e) {
            a = {
                error: e
            };
        } finally {
            try {
                d && !d.done && (n = r["return"]) && n.call(r);
            } finally {
                if (a) throw a.error;
            }
        }
        return i;
    };
    r = 1;
    i = 2;
    a = 4;
    d = 8;
    o = 16;
    l = Array.isArray;
    c = l;
    s = {};
    function Mt(e) {
        return document.createTextNode(e);
    }
    function Vt(e) {
        return document.createElement(e);
    }
    function Ut(t) {
        return t === null ? e : t;
    }
    function Ht(e) {
        return typeof e == "number";
    }
    function Lt(e) {
        return typeof e == "string";
    }
    function Kt(e) {
        return typeof e == "boolean";
    }
    function Wt(e) {
        return typeof e == "function";
    }
    function Xt(e) {
        return typeof e === "object";
    }
    Object.assign == e && (Object.assign = function t(n) {
        var r = [], i, a, d, o, l, c, s, u;
        for (i = 1; i < arguments.length; i++) {
            r[i - 1] = arguments[i];
        }
        if (n == e) throw new TypeError("Target in assign cannot be undefined or null");
        a = arguments.length;
        for (d = 1; d < a; d++) {
            o = arguments[d];
            if (o == e) continue;
            l = Object.keys(o);
            c = l.length;
            for (s = 0; s < c; s++) {
                u = l[s];
                n[u] = o[u];
            }
        }
        return n;
    });
    Object.is || (Object.is = function(e, t) {
        if (e === t) {
            return e !== 0 || 1 / e === 1 / t;
        } else {
            return e !== e && t !== t;
        }
    });
    u = Object.prototype.hasOwnProperty;
    f = Object.assign;
    function Yt(e, t, n) {
        e[t] || Object.defineProperty(e, t, {
            value: n,
            configurable: !0,
            writable: !0
        });
    }
    Yt(Array.prototype, "find", function(e) {
        var t, n, r, i, a;
        t = Object(this);
        n = t.length >>> 0;
        r = arguments[1];
        for (i = 0; i < n; i++) {
            a = t[i];
            if (e.call(r, a, i, t)) {
                return a;
            }
        }
    });
    Yt(Array.prototype, "findIndex", function(e) {
        var t, n, r, i, a;
        t = Object(this);
        n = t.length >>> 0;
        r = arguments[1];
        for (i = 0; i < n; i++) {
            a = t[i];
            if (e.call(r, a, i, t)) {
                return i;
            }
        }
        return -1;
    });
    Yt(Array.prototype, "some", function(e) {
        var t, n, r, i;
        t = Object(this);
        n = t.length >>> 0;
        r = arguments[1];
        for (i = 0; i < n; i++) {
            if (i in t && e.call(r, t[i], i, t)) {
                return !0;
            }
        }
        return !1;
    });
    Yt(String.prototype, "includes", function(e, t) {
        Ht(t) || (t = 0);
        if (t + e.length > this.length) {
            return !1;
        } else {
            return this.indexOf(e, t) !== -1;
        }
    });
    Yt(String.prototype, "startsWith", function(e, t) {
        return this.substr(!t || t < 0 ? 0 : +t, e.length) === e;
    });
    Yt(String.prototype, "endsWith", function(e, t) {
        var n, r;
        n = this.toString();
        (!Ht(t) || !isFinite(t) || Math.floor(t) !== t || t > n.length) && (t = n.length);
        t -= e.length;
        r = n.indexOf(e, t);
        return r !== -1 && r === t;
    });
    h = !1;
    p = !1;
    m = [];
    v = [];
    b = function(e, t, n, r) {
        n !== r && (e[fe] = n);
    };
    function zt(e) {
        var t = b;
        b = e;
        return t;
    }
    function $t() {
        return Object.create(null);
    }
    g = [ "Webkit", "Moz", "ms", "O" ];
    y = document.createElement("div").style;
    function Qt(e) {
        return Lt(y[e]);
    }
    x = new Map();
    C = {
        boxFlex: !0,
        boxFlexGroup: !0,
        columnCount: !0,
        flex: !0,
        flexGrow: !0,
        flexNegative: !0,
        flexPositive: !0,
        flexShrink: !0,
        fontWeight: !0,
        lineClamp: !0,
        lineHeight: !0,
        opacity: !0,
        order: !0,
        orphans: !0,
        strokeDashoffset: !0,
        widows: !0,
        zIndex: !0,
        zoom: !0
    };
    function qt(t) {
        return function(n, r, i) {
            n[t] = r;
            n[i] = e;
        };
    }
    function Gt(t) {
        return function(n, r, i) {
            if (Ht(r)) {
                n[t] = r + "px";
            } else {
                n[t] = r;
            }
            n[i] = e;
        };
    }
    function Zt(e, t, n) {
        Ht(t) && (e[n] = t + "px");
    }
    function Jt() {
        return document.documentMode;
    }
    function en(t) {
        var n, r, i, a, d, o, l, c;
        n = Object.keys(t);
        for (r = 0, i = n.length; r < i; r++) {
            a = n[r];
            d = x.get(a);
            o = t[a];
            if (o === e) continue;
            if (d === e) {
                if (Qt(a)) {
                    d = C[a] === !0 ? Ci : Zt;
                } else {
                    l = a.replace(/^\w/, function(e) {
                        return e.toUpperCase();
                    });
                    for (c = 0; c < g.length; c++) {
                        if (Qt(g[c] + l)) {
                            d = (C[a] === !0 ? qt : Gt)(g[c] + l);
                            break;
                        }
                    }
                    d === e && (d = C[a] === !0 ? Ci : Zt);
                }
                x.set(a, d);
            }
            d(t, o, a);
        }
    }
    function tn(e, t) {
        e[t] = "";
    }
    function nn(e, t, n) {
        var r;
        if (Lt(n)) {
            r = n.length;
            if (r > 11 && n.substr(r - 11, 11) === " !important") {
                e.setProperty(Ei(t), n.substr(0, r - 11), "important");
                return;
            }
        }
        e[t] = n;
    }
    function rn(t, n, r) {
        var i, a, d;
        i = t.style;
        if (Xt(n)) {
            en(n);
            if (Xt(r)) {
                for (a in r) {
                    if (r[a] === e) continue;
                    n[a] === e && tn(i, a);
                }
                for (a in n) {
                    d = n[a];
                    if (d !== e) {
                        r[a] !== d && nn(i, a, d);
                    } else r[a] !== e && tn(i, a);
                }
            } else {
                r && (i.cssText = "");
                for (a in n) {
                    d = n[a];
                    d !== e && nn(i, a, d);
                }
            }
        } else if (n) {
            i.cssText = n;
        } else {
            if (Xt(r)) {
                for (a in r) {
                    tn(i, a);
                }
            } else r && (i.cssText = "");
        }
    }
    function an(e, t) {
        if (h) e.setAttribute("class", t); else e.className = t;
    }
    w = /^input$|^select$|^textarea$|^button$/;
    _ = "tabindex";
    function dn(t, n) {
        if (t == e) return !1;
        if (w.test(t)) return !0;
        if (t === "a" && n != null && n.href != null) return !0;
        return !1;
    }
    function on(t, n, r, i, a) {
        var d = !1, o, l, c, s, u;
        if (r != null) for (o in r) {
            l = r[o];
            c = i[o];
            if (a && o === _) {
                l = -1;
                d = !0;
            } else if (o === fe && !h) {
                if (Wt(l)) {
                    i[ce] = l;
                    l = l();
                }
                s = c;
                u = l;
                i[o] = l;
                continue;
            }
            if (c !== l) {
                i[o] = l;
                if (h) {
                    if (o === "href") n.setAttributeNS("http://www.w3.org/1999/xlink", "href", l); else n.setAttribute(o, l);
                } else if (o in n && !(o === "list" || o === "form")) {
                    n[o] = l;
                } else n.setAttribute(o, l);
            }
        }
        if (a && !d && dn(t.tag, r)) {
            n.setAttribute(_, "-1");
            i[_] = -1;
        }
        if (r == e) {
            for (o in i) {
                if (i[o] !== e) {
                    if (a && o === _) continue;
                    if (o === ce) continue;
                    i[o] = e;
                    n.removeAttribute(o);
                }
            }
        } else {
            for (o in i) {
                if (i[o] !== e && !(o in r)) {
                    if (a && o === _) continue;
                    if (o === ce) continue;
                    i[o] = e;
                    n.removeAttribute(o);
                }
            }
        }
        u !== e && b(n, t, u, s);
        return i;
    }
    function ln(e) {
        var t, n;
        t = e.component;
        if (t) {
            n = t.postInitDom;
            if (n) {
                m.push(n);
                v.push(e);
            }
            if ((e.ctx.$hookFlags || 0) & r) {
                m.push(Fi);
                v.push(e);
            }
        }
    }
    function cn(e) {
        var t, n, r;
        t = e.component;
        if (t) {
            n = t.postUpdateDom;
            if (n) {
                m.push(n);
                v.push(e);
            }
            r = e.ctx.$hookFlags || 0;
            if (r & i) {
                m.push(ji);
                v.push(e);
            }
            n = t.postUpdateDomEverytime;
            if (n) {
                m.push(n);
                v.push(e);
            }
            if (r & a) {
                m.push(Bi);
                v.push(e);
            }
        }
    }
    function sn(e) {
        var t, n;
        t = e.component;
        if (t) {
            n = t.postUpdateDomEverytime;
            if (n) {
                m.push(n);
                v.push(e);
            }
            if ((e.ctx.$hookFlags || 0) & a) {
                m.push(Bi);
                v.push(e);
            }
        }
    }
    function un(t) {
        var n;
        while (t) {
            n = t.cfg;
            if (n !== e) break;
            if (t.ctx !== e && t.component !== s) {
                n = t.ctx.cfg;
                break;
            }
            t = t.parent;
        }
        return n;
    }
    function fn(t, n) {
        var r, i;
        if (t === e) return;
        if ("current" in t) {
            t.current = n;
        } else if (Wt(t)) {
            t(n);
        } else if (l(t)) {
            r = t[0];
            i = r.refs;
            if (i === e) {
                i = $t();
                r.refs = i;
            }
            i[t[1]] = n;
        }
    }
    k = [];
    S = null;
    function hn(t, n, r, i) {
        var a, d, o, l, c, s, u, f, m, v, b, g, y, x, C, w;
        a = {
            tag: t.tag,
            key: t.key,
            ref: t.ref,
            className: t.className,
            style: t.style,
            attrs: t.attrs,
            children: t.children,
            component: t.component,
            data: t.data,
            cfg: e,
            parent: n,
            element: e,
            ctx: e,
            orig: t
        };
        d = h;
        o = p;
        l = a.component;
        fn(a.ref, a);
        if (l) {
            if (l.ctxClass) {
                c = new l.ctxClass(a.data || {}, a);
                c.data === e && (c.data = a.data || {});
                c.me === e && (c.me = a);
            } else {
                c = {
                    data: a.data || {},
                    me: a,
                    cfg: e
                };
            }
            c.cfg = t.cfg === e ? un(n) : t.cfg;
            a.ctx = c;
            Ft = c;
            l.init && l.init(c, a);
            X !== W && X(t, 0);
            l.render && l.render(c, a);
            Ft = e;
        } else {}
        s = a.tag;
        if (s === "-") {
            a.tag = e;
            a.children = e;
            return a;
        } else if (s === "@") {
            r = a.data;
            I.set(r, a);
            i = null;
            s = e;
        }
        u = a.children;
        f = !1;
        if (Ht(u)) {
            u = "" + u;
            a.children = u;
        }
        if (s === e) {
            if (Lt(u)) {
                m = Mt(u);
                a.element = m;
                r.insertBefore(m, i);
            } else {
                mn(a, r, i);
            }
            if (l) {
                l.postRender && l.postRender(a.ctx, a);
                ln(a);
            }
            return a;
        }
        if (s === "/") {
            v = u;
            if (v === "") {} else if (i == e) {
                b = r.lastChild;
                r.insertAdjacentHTML("beforeend", v);
                a.element = [];
                if (b) {
                    b = b.nextSibling;
                } else {
                    b = r.firstChild;
                }
                while (b) {
                    a.element.push(b);
                    b = b.nextSibling;
                }
            } else {
                m = i;
                g = i.previousSibling;
                y = !1;
                x = r;
                if (!m.insertAdjacentHTML) {
                    m = x.insertBefore(Vt("i"), m);
                    y = !0;
                }
                m.insertAdjacentHTML("beforebegin", v);
                if (g) {
                    g = g.nextSibling;
                } else {
                    g = x.firstChild;
                }
                C = [];
                while (g !== m) {
                    C.push(g);
                    g = g.nextSibling;
                }
                a.element = C;
                y && x.removeChild(m);
            }
            if (l) {
                l.postRender && l.postRender(a.ctx, a);
                ln(a);
            }
            return a;
        }
        if (h || s === "svg") {
            m = document.createElementNS("http://www.w3.org/2000/svg", s);
            f = s === "foreignObject";
            h = !f;
        } else {
            m = Vt(s);
        }
        r.insertBefore(m, i);
        a.element = m;
        mn(a, m, null);
        l && (l.postRender && l.postRender(a.ctx, a));
        p && S === a && (p = !1);
        f && (h = !0);
        (a.attrs || p) && (a.attrs = on(a, m, a.attrs, {}, p));
        a.style && rn(m, a.style, e);
        w = a.className;
        w && an(m, w);
        h = d;
        p = o;
        ln(a);
        return a;
    }
    function pn(t) {
        if (t === !1 || t === !0 || t === null) return e;
        if (Lt(t)) {
            return {
                children: t
            };
        }
        if (Ht(t)) {
            return {
                children: "" + t
            };
        }
        return t;
    }
    function mn(e, t, n) {
        var r, i, a;
        r = e.children;
        if (Lt(r)) {
            t.textContent = r;
            return;
        }
        i = [];
        jn(i, r);
        for (a = 0; a < i.length; a++) {
            i[a] = hn(i[a], e, t, n);
        }
        e.children = i;
    }
    function vn(t) {
        var n, r, i, a, d, o, c, s;
        fn(t.ref, e);
        n = t.children;
        if (l(n)) {
            for (r = 0, i = n.length; r < i; r++) {
                vn(n[r]);
            }
        }
        a = t.component;
        if (a) {
            d = t.ctx;
            Ft = d;
            X !== W && X(t, 3);
            a.destroy && a.destroy(d, t, t.element);
            o = d.disposables;
            if (l(o)) {
                for (c = o.length; c-- > 0; ) {
                    s = o[c];
                    if (Wt(s)) s(d); else s.dispose();
                }
            }
            Ft = e;
        }
        if (t.tag === "@") {
            I.delete(t.data);
            bn(t);
        }
    }
    function bn(e) {
        var t, n, r, i, a, d, o;
        t = e.element;
        if (l(t)) {
            n = t[0].parentNode;
            if (n) {
                for (r = 0; r < t.length; r++) {
                    n.removeChild(t[r]);
                }
            }
        } else if (t != null) {
            i = t.parentNode;
            i && i.removeChild(t);
        } else {
            a = e.children;
            if (l(a)) {
                for (d = 0, o = a.length; d < o; d++) {
                    bn(a[d]);
                }
            }
        }
    }
    function gn(e) {
        vn(e);
        bn(e);
    }
    E = $t();
    I = new Map();
    function yn(t, n, r, i) {
        var a, d, o, c, s;
        a = t.element;
        d = t.children;
        if (l(a)) {
            for (o = 0; o < a.length; o++) {
                if (a[o] === n) {
                    i.push(t);
                    if (l(d)) {
                        return d;
                    }
                    return null;
                }
            }
        } else if (a == e) {
            if (l(d)) {
                for (c = 0; c < d.length; c++) {
                    s = yn(d[c], n, r, i);
                    if (s !== e) {
                        i.splice(r, 0, t);
                        return s;
                    }
                }
            }
        } else if (a === n) {
            i.push(t);
            if (l(d)) {
                return d;
            }
            return null;
        }
        return e;
    }
    function xn(t) {
        var n = [], r, i, a, d, o, l, c, s, u, f, h, p, m;
        if (t == e) return n;
        r = Object.keys(E);
        i = r.map(function(e) {
            return E[e].e || document.body;
        });
        a = r.map(function(e) {
            return E[e].n;
        });
        I.forEach(function(e, t) {
            i.push(t);
            a.push(e);
        });
        d = [];
        e: while (!0) {
            t: while (t) {
                for (o = 0; o < i.length; o++) {
                    if (t === i[o]) break t;
                }
                d.push(t);
                t = t.parentNode;
            }
            if (!t || d.length === 0) return n;
            l = null;
            c = d.pop();
            for (o = 0; o < i.length; o++) {
                if (t === i[o]) {
                    s = a[o];
                    if (s === e) continue;
                    n = [];
                    if (s.parent !== e) {
                        u = s;
                        while (u = u.parent) {
                            n.push(u);
                        }
                        n.reverse();
                    }
                    f = yn(s, c, n.length, n);
                    if (f !== e) {
                        l = f;
                        break e;
                    }
                }
            }
            d.push(c);
            d.push(t);
            t = t.parentNode;
        }
        e: while (d.length) {
            c = d.pop();
            if (l && l.length) for (h = 0, p = l.length; h < p; h++) {
                m = l[h];
                f = yn(m, c, n.length, n);
                if (f !== e) {
                    l = f;
                    continue e;
                }
            }
            n.push(null);
            break;
        }
        return n;
    }
    function Cn(e) {
        var t, n;
        t = xn(e);
        n = null;
        while (n === null) {
            n = t.pop();
        }
        return n;
    }
    function wn(t, n, r) {
        if (r) {
            if (r.postRender) {
                Ft = n.ctx;
                r.postRender(Ft, t, n);
                Ft = e;
            }
        }
        n.data = t.data;
        cn(n);
    }
    function _n(t, n, r) {
        var i, a;
        Ft = e;
        if (l(t.children)) {
            i = h;
            a = p;
            if (t.tag === "svg") {
                h = !0;
            } else h && t.tag === "foreignObject" && (h = !1);
            p && S === t && (p = !1);
            Ln(t.children, t.element || n, t.element != null ? null : r);
            h = i;
            p = a;
        }
        sn(t);
    }
    function kn(t, n, r, i, a, d) {
        var o, c = !1, u, m, v, b, g, y, x, C, w, _, k, E;
        o = t.component;
        u = n.ctx;
        if (o != null && u != null) {
            m = !1;
            if (u[j] >= V) {
                a = Math.max(a, u[B]);
                m = !0;
            }
            if (o.id !== n.component.id) {
                c = !0;
            } else {
                Ft = u;
                if (t.cfg !== e) u.cfg = t.cfg; else u.cfg = un(n.parent);
                if (o.shouldChange) if (!o.shouldChange(u, t, n) && !Z && !m) {
                    _n(n, r, i);
                    return n;
                }
                u.data = t.data || {};
                n.component = o;
                X !== W && X(t, d ? 2 : 1);
                if (o.render) {
                    n.orig = t;
                    t = f({}, t);
                    n.cfg = e;
                    t.cfg !== e && (t.cfg = e);
                    o.render(u, t, n);
                    if (t.cfg !== e) {
                        if (n.cfg === e) n.cfg = t.cfg; else f(n.cfg, t.cfg);
                    }
                }
                Ft = e;
            }
        } else {
            if (n.orig === t && !Z) {
                _n(n, r, i);
                return n;
            }
            n.orig = t;
        }
        v = t.children;
        b = n.children;
        g = t.tag;
        if (g === "-") {
            _n(n, r, i);
            return n;
        }
        y = h;
        x = p;
        Ht(v) && (v = "" + v);
        if (c || o != e && u == e || o == e && u != e && u.me.component !== s) {} else if (g === "/") {
            if (n.tag === "/" && b === v) {
                wn(t, n, o);
                return n;
            }
        } else if (g === n.tag) {
            if (g === "@") {
                if (t.data !== n.data) {
                    C = hn(t, n.parent, t.data, null);
                    gn(n);
                    return C;
                }
                r = t.data;
                i = En(n);
                i != null && (i = i.nextSibling);
                g = e;
            }
            if (g === e) {
                if (Lt(v) && Lt(b)) {
                    if (v !== b) {
                        w = n.element;
                        w.textContent = v;
                        n.children = v;
                    }
                } else {
                    p && S === n && (p = !1);
                    if (a <= 0) {
                        l(b) && Ln(n.children, r, i);
                    } else {
                        n.children = Bn(r, v, b, n, i, a - 1);
                    }
                    h = y;
                    p = x;
                }
                wn(t, n, o);
                return n;
            } else {
                _ = !1;
                if (g === "svg") {
                    h = !0;
                } else if (h && g === "foreignObject") {
                    _ = !0;
                    h = !1;
                }
                p && S === n && (p = !1);
                w = n.element;
                if (Lt(v) && !l(b)) {
                    if (v !== b) {
                        w.textContent = v;
                        b = v;
                    }
                } else {
                    if (a <= 0) {
                        l(b) && Ln(n.children, w, null);
                    } else {
                        b = Bn(w, v, b, n, null, a - 1);
                    }
                }
                n.children = b;
                _ && (h = !0);
                wn(t, n, o);
                (n.attrs || t.attrs || p) && (n.attrs = on(n, w, t.attrs, n.attrs || {}, p));
                rn(w, t.style, n.style);
                n.style = t.style;
                k = t.className;
                if (k !== n.className) {
                    an(w, k || "");
                    n.className = k;
                }
                h = y;
                p = x;
                return n;
            }
        }
        E = n.element;
        l(E) && (E = E[0]);
        if (E == e) E = r; else E = E.parentNode;
        C = hn(t, n.parent, E, Sn(n));
        gn(n);
        return C;
    }
    function Sn(t) {
        var n, r, i;
        if (t === e) return null;
        n = t.element;
        if (n != null) {
            if (l(n)) return n[0];
            return n;
        }
        r = t.children;
        if (!l(r)) return null;
        for (i = 0; i < r.length; i++) {
            n = Sn(r[i]);
            if (n) return n;
        }
        return null;
    }
    function En(t) {
        var n, r, i;
        if (t === e) return null;
        n = t.element;
        if (n != null) {
            if (l(n)) return n[n.length - 1];
            return n;
        }
        r = t.children;
        if (!l(r)) return null;
        for (i = r.length; i-- > 0; ) {
            n = En(r[i]);
            if (n) return n;
        }
        return null;
    }
    function In(t, n, r, i) {
        var a, d;
        while (++n < r) {
            a = t[n];
            if (a == e) continue;
            d = Sn(a);
            if (d != null) return d;
        }
        return i;
    }
    function Nn() {
        var t, n, r;
        t = v.length;
        for (n = 0; n < t; n++) {
            r = v[n];
            Ft = r.ctx;
            m[n].call(r.component, Ft, r, r.element);
        }
        Ft = e;
        m = [];
        v = [];
    }
    function Dn(e, t, n, r, i, a, d) {
        t[n] = kn(e, t[n], a, In(t, n, r, i), d);
    }
    function Pn(e, t, n) {
        var r, i, a;
        r = e.element;
        if (r != null) {
            if (l(r)) {
                for (i = 0; i < r.length; i++) {
                    t.insertBefore(r[i], n);
                }
            } else t.insertBefore(r, n);
            return;
        }
        a = e.children;
        if (!l(a)) return;
        for (i = 0; i < a.length; i++) {
            Pn(a[i], t, n);
        }
    }
    function An(e, t, n, r, i) {
        var a, d, o;
        a = In(e, t, n, r);
        d = e[t];
        o = Sn(d);
        o != null && o !== a && Pn(d, i, a);
    }
    function Rn(e, t, n, r, i, a, d) {
        var o, l, c;
        o = In(t, n, r, i);
        l = t[n];
        c = Sn(l);
        c != null && c !== o && Pn(l, a, o);
        t[n] = kn(e, l, a, o, d);
    }
    function Fn(t, n) {
        var r, i;
        if (n == e) return;
        if (c(n)) {
            for (r = 0; r < n.length; r++) {
                Fn(t, n[r]);
            }
        } else {
            i = pn(n);
            i !== e && t.push(i);
        }
    }
    function jn(e, t) {
        Fn(e, t);
    }
    function Bn(t, n, r, i, a, d) {
        var o;
        r == e && (r = []);
        if (!l(r)) {
            t.firstChild && t.removeChild(t.firstChild);
            r = [];
        }
        o = [];
        jn(o, n);
        return On(t, o, r, i, a, d);
    }
    function On(t, n, r, i, a, d) {
        var o, l, c, s = 0, u = 0, f, h, p, m, v, b, g, y, x, C, w;
        o = n.length;
        l = r.length;
        c = l;
        while (s < o && u < c) {
            if (n[s].key === r[u].key) {
                Dn(n[s], r, u, l, a, t, d);
                s++;
                u++;
                continue;
            }
            while (!0) {
                if (n[o - 1].key === r[c - 1].key) {
                    o--;
                    c--;
                    Dn(n[o], r, c, l, a, t, d);
                    if (s < o && u < c) continue;
                }
                break;
            }
            if (s < o && u < c) {
                if (n[s].key === r[c - 1].key) {
                    r.splice(u, 0, r[c - 1]);
                    r.splice(c, 1);
                    Rn(n[s], r, u, l, a, t, d);
                    s++;
                    u++;
                    continue;
                }
                if (n[o - 1].key === r[u].key) {
                    r.splice(c, 0, r[u]);
                    r.splice(u, 1);
                    c--;
                    o--;
                    Rn(n[o], r, c, l, a, t, d);
                    continue;
                }
            }
            break;
        }
        if (u === c) {
            if (s === o) {
                return r;
            }
            while (s < o) {
                r.splice(u, 0, hn(n[s], i, t, In(r, u - 1, l, a)));
                u++;
                c++;
                l++;
                s++;
            }
            return r;
        }
        if (s === o) {
            while (u < c) {
                c--;
                gn(r[c]);
                r.splice(c, 1);
            }
            return r;
        }
        f = $t();
        h = $t();
        p = s;
        m = u;
        v = 0;
        for (;u < c; u++) {
            b = r[u];
            g = b.key;
            if (g != null) {
                f[g] = u;
            } else v--;
        }
        y = -v - v;
        for (;s < o; s++) {
            b = n[s];
            g = b.key;
            if (g != null) {
                h[g] = s;
            } else v++;
        }
        y += v;
        x = 0;
        s = p;
        u = m;
        while (u < c && s < o) {
            if (r[u] === null) {
                r.splice(u, 1);
                c--;
                l--;
                x--;
                continue;
            }
            C = r[u].key;
            if (C == e) {
                u++;
                continue;
            }
            g = n[s].key;
            if (g == e) {
                s++;
                while (s < o) {
                    g = n[s].key;
                    if (g != e) break;
                    s++;
                }
                if (g == e) break;
            }
            w = f[g];
            if (w === e) {
                r.splice(u, 0, hn(n[s], i, t, In(r, u - 1, l, a)));
                x++;
                s++;
                u++;
                c++;
                l++;
                continue;
            }
            if (!(C in h)) {
                gn(r[u]);
                r.splice(u, 1);
                x--;
                c--;
                l--;
                continue;
            }
            if (u === w + x) {
                Dn(n[s], r, u, l, a, t, d);
                s++;
                u++;
            } else {
                r.splice(u, 0, r[w + x]);
                x++;
                r[w + x] = null;
                Rn(n[s], r, u, l, a, t, d);
                u++;
                c++;
                l++;
                s++;
            }
        }
        while (u < c) {
            if (r[u] === null) {
                r.splice(u, 1);
                c--;
                l--;
                continue;
            }
            if (r[u].key != null) {
                gn(r[u]);
                r.splice(u, 1);
                c--;
                l--;
                continue;
            }
            u++;
        }
        while (s < o) {
            g = n[s].key;
            if (g != null) {
                r.splice(u, 0, hn(n[s], i, t, In(r, u - 1, l, a)));
                c++;
                l++;
                x++;
                u++;
            }
            s++;
        }
        if (!y) return r;
        y = y - Math.abs(v) >> 1;
        s = p;
        u = m;
        while (s < o) {
            if (u < c) {
                C = r[u].key;
                if (C != null) {
                    u++;
                    continue;
                }
            }
            g = n[s].key;
            if (s < c && g === r[s].key) {
                if (g != null) {
                    s++;
                    continue;
                }
                Dn(n[s], r, s, l, a, t, d);
                y--;
                s++;
                u = s;
                continue;
            }
            if (g != null) {
                if (y === 0 && v < 0) {
                    while (!0) {
                        gn(r[u]);
                        r.splice(u, 1);
                        c--;
                        l--;
                        v++;
                        if (r[u].key != null) break;
                    }
                    continue;
                }
                while (r[u].key == e) u++;
                r[u].key;
                r.splice(s, 0, r[u]);
                r.splice(u + 1, 1);
                An(r, s, l, a, t);
                s++;
                u = s;
                continue;
            }
            if (u < c) {
                r.splice(s, 0, r[u]);
                r.splice(u + 1, 1);
                Rn(n[s], r, s, l, a, t, d);
                y--;
                s++;
                u++;
            } else {
                r.splice(s, 0, hn(n[s], i, t, In(r, s - 1, l, a)));
                c++;
                l++;
                s++;
                u++;
            }
        }
        while (c > s) {
            c--;
            gn(r[c]);
            r.splice(c, 1);
        }
        return r;
    }
    N = !1;
    D = window.requestAnimationFrame;
    D && D(function(e) {
        e === +e && (N = !0);
    });
    P = window.setTimeout;
    A = Date.now || function() {
        return new Date().getTime();
    };
    R = A();
    F = 0;
    function Tn(e) {
        var t;
        if (N) {
            D(e);
        } else {
            t = 50 / 3 + F - A();
            t < 0 && (t = 0);
            P(function() {
                F = A();
                e(F - R);
            }, t);
        }
    }
    j = "$invalidated";
    B = "$deepness";
    O = !0;
    T = !1;
    M = !0;
    V = 0;
    U = {};
    function Mn(t, n, r) {
        var i;
        jt == e && (jt = {});
        i = jt[t] || [];
        i.push({
            priority: n,
            callback: r
        });
        jt[t] = i;
    }
    function Vn(e, t, n, r) {
        var i, a;
        i = U[e];
        if (i) for (a = 0; a < i.length; a++) {
            if (i[a](t, n, r)) return !0;
        }
        return !1;
    }
    H = !1;
    try {
        L = Object.defineProperty({}, "passive", {
            get: function() {
                H = !0;
            }
        });
        window.addEventListener("test", L, L);
        window.removeEventListener("test", L, L);
    } catch (e) {
        H = !1;
    }
    K = 0;
    function Un(e, t) {
        var n, r;
        if (t[0] == "!") return;
        n = t[0] == "^";
        r = t;
        if (t[0] == "@") {
            r = t.slice(1);
            e = document;
        }
        n && (r = t.slice(1));
        function i(n) {
            var r, i;
            n = n || window.event;
            r = n.target || n.srcElement || e;
            i = Cn(r);
            K++;
            Vn(t, n, r, i);
            K--;
            K == 0 && Q && Yn();
        }
        "on" + r in window && (e = window);
        e.addEventListener(r, i, H ? {
            capture: n,
            passive: !1
        } : n);
    }
    function Hn() {
        var t, n, r, i, a, d;
        if (jt === e) return;
        t = Object.keys(jt);
        for (n = 0; n < t.length; n++) {
            r = t[n];
            i = jt[r];
            i = i.sort(function(e, t) {
                return e.priority - t.priority;
            });
            U[r] = i.map(function(e) {
                return e.callback;
            });
        }
        jt = e;
        a = document.body;
        for (d = 0; d < t.length; d++) {
            Un(a, t[d]);
        }
    }
    function Ln(t, n, r) {
        var i, a, d, o, c, s, u;
        i = t.length;
        for (a = 0; a < i; a++) {
            d = t[a];
            o = d.ctx;
            if (o != null && o[j] >= V) {
                t[a] = kn(d.orig, d, n, In(t, a, i, r), o[B], !0);
            } else if (l(d.children)) {
                c = h;
                s = p;
                p && S === d && (p = !1);
                if (d.tag === "svg") h = !0; else h && d.tag === "foreignObject" && (h = !1);
                u = d.element;
                if (u != e) {
                    Ln(d.children, u, null);
                } else {
                    Ln(d.children, n, In(t, a, i, r));
                }
                sn(d);
                h = c;
                p = s;
            }
        }
    }
    W = function() {};
    X = W;
    Y = function() {};
    z = function() {};
    $ = function() {};
    function Kn(e) {
        var t = Y;
        Y = e;
        return t;
    }
    function Wn(e) {
        var t = $;
        $ = e;
        return t;
    }
    function Xn(t, n, r) {
        var i, a, d;
        while (n != null) {
            if (t === n) return !0;
            i = n.parent;
            if (i == e) {
                for (a = 0; a < r.length; a++) {
                    d = E[r[a]];
                    if (!d) continue;
                    if (d.n === n) {
                        i = d.p;
                        break;
                    }
                }
            }
            n = i;
        }
        return !1;
    }
    Q = !1;
    function Yn() {
        Q = !1;
        $n(A() - R);
        Oi();
    }
    function zn(e) {
        T = !1;
        $n(e);
        le(Oi);
    }
    q = Ri({
        render: function(t, n) {
            var r, i;
            r = t.data;
            i = r.f(r);
            if (i === e) {
                n.tag = "-";
            } else {
                n.children = i;
            }
        }
    });
    function $n(t) {
        var n, r, i, a, d, o, c, s, u, f;
        A();
        Hn();
        z();
        V++;
        Z = G;
        G = !1;
        Y();
        n = !1;
        if (O) {
            O = !1;
            n = !0;
        }
        K++;
        for (r = 0; r < 2; r++) {
            S = k.length === 0 ? null : k[k.length - 1];
            p = !1;
            Bt = Object.keys(E);
            for (i = 0; i < Bt.length; i++) {
                a = E[Bt[i]];
                if (!a) continue;
                d = a.n;
                o = null;
                for (c = i + 1; c < Bt.length; c++) {
                    s = E[Bt[c]];
                    if (s === e) continue;
                    o = Sn(s.n);
                    if (o != null) break;
                }
                S && (p = !Xn(S, a.p, Bt));
                a.e === e && (a.e = document.body);
                if (d) {
                    if (n || d.ctx[j] >= V) {
                        u = q(a);
                        kn(u, d, a.e, o, n ? 1e6 : d.ctx[B]);
                    } else {
                        l(a.c) && Ln(a.c, a.e, o);
                    }
                } else {
                    u = q(a);
                    d = hn(u, e, a.e, o);
                    a.n = d;
                }
                a.c = d.children;
            }
            Bt = e;
            Nn();
            if (!Q) break;
        }
        Q = !1;
        K--;
        f = E["0"];
        $(f ? f.c : null);
        A();
    }
    G = !1;
    Z = !1;
    function Qn() {
        G = !0;
        J();
    }
    function qn(e) {
        var t = J;
        J = e;
        return t;
    }
    J = function(t, n) {
        if (t != null) {
            n == e && (n = 1e6);
            if (t[j] !== V + 1) {
                t[j] = V + 1;
                t[B] = n;
            } else {
                n > t[B] && (t[B] = n);
            }
        } else {
            O = !0;
        }
        if (T || M) return;
        T = !0;
        Tn(zn);
    };
    ee = 0;
    function Gn(t, n, r) {
        var i;
        ee++;
        i = "" + ee;
        E[i] = {
            f: t,
            e: n,
            c: [],
            p: r,
            n: e
        };
        if (Bt != null) {
            Bt.push(i);
        } else {
            tr();
        }
        return i;
    }
    function Zn(e) {
        var t = E[e];
        if (!t) return;
        t.n && gn(t.n);
        delete E[e];
    }
    function Jn() {
        return E;
    }
    function er() {
        M = !1;
        J();
    }
    te = er;
    function tr() {
        M = !0;
        te();
        te = er;
    }
    function nr(t, n) {
        Zn("0");
        E["0"] = {
            f: t,
            e: n,
            c: [],
            p: e,
            n: e
        };
        tr();
    }
    function rr(e) {
        var t = te;
        te = function() {
            e(t);
        };
    }
    function ir(t, n, r) {
        var i, a, o, l, c, s, u, f, h, p;
        if (r == e) {
            r = {
                target: t
            };
        } else Xt(r) && r.target == e && (r.target = t);
        i = or(n, r);
        if (i != e) return i;
        a = Ot;
        while (t) {
            o = t.component;
            if (o) {
                l = t.ctx;
                Ot = l;
                if (((l.$hookFlags || 0) & d) === d) {
                    c = l.$hooks;
                    for (s = 0, u = c.length; s < u; s++) {
                        f = c[s];
                        if (f instanceof At) {
                            h = f.events[n];
                            if (h !== e) {
                                p = +h.call(l, r);
                                if (p == 1) {
                                    Ot = a;
                                    return l;
                                }
                                if (p == 2) {
                                    Ot = a;
                                    return e;
                                }
                                p == Dt.NotHandledPreventDefault && (i = l);
                            }
                        }
                    }
                }
                h = o[n];
                if (h) {
                    p = +h.call(o, l, r);
                    if (p == 1) {
                        Ot = a;
                        return l;
                    }
                    if (p == 2) {
                        Ot = a;
                        return e;
                    }
                    p == Dt.NotHandledPreventDefault && (i = l);
                }
                h = o.shouldStopBubble;
                if (h) {
                    if (h.call(o, l, n, r)) break;
                }
            }
            t = t.parent;
        }
        Ot = a;
        return i;
    }
    function ar(t, n, r) {
        var i, a, o, c, s, u, f, h, p, m, v, b;
        if (!t) return e;
        i = t.component;
        if (i) {
            a = t.ctx;
            o = Ot;
            Ot = a;
            if (((a.$hookFlags || 0) & d) === d) {
                c = a.$hooks;
                for (s = 0, u = c.length; s < u; s++) {
                    f = c[s];
                    if (f instanceof At) {
                        h = f.events[n];
                        if (h !== e) {
                            p = +h.call(a, r);
                            if (p == 1) {
                                Ot = o;
                                return a;
                            }
                            if (p == 2) {
                                Ot = o;
                                return e;
                            }
                            p == Dt.NotHandledPreventDefault && (m = a);
                        }
                    }
                }
            }
            h = i[n];
            if (h) {
                p = +h.call(i, a, r);
                if (p == 1) {
                    Ot = o;
                    return a;
                }
                if (p == 2) {
                    Ot = o;
                    return e;
                }
                p == Dt.NotHandledPreventDefault && (m = a);
            }
            h = i.shouldStopBroadcast;
            if (h) {
                if (h.call(i, a, n, r)) {
                    Ot = o;
                    return m;
                }
            }
            Ot = o;
        }
        v = t.children;
        if (l(v)) {
            for (s = 0; s < v.length; s++) {
                b = ar(v[s], n, r);
                if (b != e) return b;
            }
        }
        return m;
    }
    function dr(t, n, r) {
        var i, a, d, c, s, u, f, h, p, m, v, b;
        if (!t) return e;
        i = t.component;
        if (i) {
            a = t.ctx;
            if (((a.$hookFlags || 0) & o) === o) {
                d = a.$hooks;
                c = Ot;
                Ot = a;
                for (s = 0, u = d.length; s < u; s++) {
                    f = d[s];
                    if (f instanceof Rt) {
                        h = f.events[n];
                        if (h !== e) {
                            p = +h.call(a, r);
                            if (p == 1) {
                                Ot = c;
                                return a;
                            }
                            if (p == 2) {
                                Ot = c;
                                return e;
                            }
                            p == Dt.NotHandledPreventDefault && (m = a);
                        }
                    }
                }
                Ot = c;
            }
        }
        v = t.children;
        if (l(v)) {
            for (s = 0, u = v.length; s < u; s++) {
                b = dr(v[s], n, r);
                if (b != e) return b;
            }
        }
        return m;
    }
    function or(t, n) {
        var r, i, a, d;
        r = Object.keys(E);
        for (i = 0; i < r.length; i++) {
            a = E[r[i]].n;
            if (a != null) {
                d = dr(a, t, n);
                if (d != null) return d;
            }
        }
        return e;
    }
    function lr(t, n) {
        var r, i, a, d;
        r = or(t, n);
        if (r != null) return r;
        i = Object.keys(E);
        for (a = 0; a < i.length; a++) {
            d = E[i[a]].n;
            if (d != null) {
                r = ar(d, t, n);
                if (r != null) return r;
            }
        }
        return e;
    }
    function cr(e) {
        var t = e.preventDefault;
        if (t) t.call(e); else e.returnValue = !1;
    }
    function sr(e, t) {
        x.set(e, t);
    }
    sr("float", qt("cssFloat"));
    ne = null;
    re = [ [ 414, 800, 900 ], [ 736, 1280, 1440 ] ];
    function ur() {
        ne = null;
        J();
        return !1;
    }
    ie = [ "resize", "orientationchange" ];
    for (ae = 0; ae < ie.length; ae++) Mn(ie[ae], 10, ur);
    de = window.document.documentElement;
    oe = /Android/i.test(navigator.userAgent);
    function fr() {
        var t, n, r, i, a, d;
        if (ne == e) {
            t = de.clientWidth;
            n = de.clientHeight;
            r = window.orientation;
            i = n >= t;
            if (r == e) r = i ? 0 : 90; else r = +r;
            if (oe) {
                a = Math.abs(r) % 180 === 90;
                if (Tt == e) {
                    Tt = a === i;
                } else {
                    i = a === Tt;
                }
            }
            d = 0;
            while (t > re[+!i][d]) d++;
            ne = {
                width: t,
                height: n,
                orientation: r,
                deviceCategory: d,
                portrait: i,
                dppx: window.devicePixelRatio
            };
        }
        return ne;
    }
    le = function() {
        var t = [], n, r, i;
        function a() {
            var e, n, r;
            e = t;
            t = [];
            for (n = 0, r = e.length; n < r; n++) {
                e[n]();
            }
        }
        if (window.MutationObserver) {
            n = document.createElement("div");
            new MutationObserver(a).observe(n, {
                attributes: !0
            });
            return function(e) {
                t.length || n.setAttribute("yes", "no");
                t.push(e);
            };
        } else {
            r = window.setImmediate || P;
            return function(n) {
                t.push(n);
                i || (i = r(function() {
                    i = e;
                    a();
                }, 0));
            };
        }
    }();
    window.Promise || function() {
        function t(e, t) {
            return function() {
                e.apply(t, arguments);
            };
        }
        function n(t) {
            var n = this;
            if (this.s === null) {
                this.d.push(t);
                return;
            }
            le(function() {
                var r, i;
                r = n.s ? t[0] : t[1];
                if (r == e) {
                    (n.s ? t[2] : t[3])(n.v);
                    return;
                }
                try {
                    i = r(n.v);
                } catch (e) {
                    t[3](e);
                    return;
                }
                t[2](i);
            });
        }
        function r() {
            var e, t;
            for (e = 0, t = this.d.length; e < t; e++) {
                n.call(this, this.d[e]);
            }
            this.d = null;
        }
        function i(e) {
            this.s = !1;
            this.v = e;
            r.call(this);
        }
        function a(e, t, n) {
            var r = !1;
            try {
                e(function(e) {
                    if (r) return;
                    r = !0;
                    t(e);
                }, function(e) {
                    if (r) return;
                    r = !0;
                    n(e);
                });
            } catch (e) {
                if (r) return;
                r = !0;
                n(e);
            }
        }
        function d(e) {
            var n;
            try {
                if (e === this) throw new TypeError("Promise self resolve");
                if (Object(e) === e) {
                    n = e.then;
                    if (typeof n === "function") {
                        a(t(n, e), t(d, this), t(i, this));
                        return;
                    }
                }
                this.s = !0;
                this.v = e;
                r.call(this);
            } catch (e) {
                i.call(this, e);
            }
        }
        function o(e) {
            this.s = null;
            this.v = null;
            this.d = [];
            a(e, t(d, this), t(i, this));
        }
        o.prototype.then = function(e, t) {
            var r = this;
            return new o(function(i, a) {
                n.call(r, [ e, t, i, a ]);
            });
        };
        o.prototype["catch"] = function(t) {
            return this.then(e, t);
        };
        o.all = function() {
            var e = [].slice.call(arguments.length === 1 && l(arguments[0]) ? arguments[0] : arguments);
            return new o(function(t, n) {
                var r, i;
                if (e.length === 0) {
                    t(e);
                    return;
                }
                r = e.length;
                function a(i, d) {
                    var o;
                    try {
                        if (d && (typeof d === "object" || typeof d === "function")) {
                            o = d.then;
                            if (typeof o === "function") {
                                o.call(d, function(e) {
                                    a(i, e);
                                }, n);
                                return;
                            }
                        }
                        e[i] = d;
                        --r === 0 && t(e);
                    } catch (e) {
                        n(e);
                    }
                }
                for (i = 0; i < e.length; i++) {
                    a(i, e[i]);
                }
            });
        };
        o.resolve = function(e) {
            if (e && typeof e === "object" && e.constructor === o) {
                return e;
            }
            return new o(function(t) {
                t(e);
            });
        };
        o.reject = function(e) {
            return new o(function(t, n) {
                n(e);
            });
        };
        o.race = function(e) {
            return new o(function(t, n) {
                var r, i;
                for (r = 0, i = e.length; r < i; r++) {
                    e[r].then(t, n);
                }
            });
        };
        window["Promise"] = o;
    }();
    ce = "b$value";
    se = "b$selStart";
    ue = "b$selEnd";
    fe = "value";
    function hr(e) {
        var t = e.type;
        return t === "checkbox" || t === "radio";
    }
    function pr(e, t) {
        var n, r;
        n = e.length;
        if (n !== t.length) return !1;
        for (r = 0; r < n; r++) {
            if (e[r] !== t[r]) return !1;
        }
        return !0;
    }
    function mr(e, t) {
        var n;
        for (n = 0; n < e.length; n++) {
            if (e[n] === t) return !0;
        }
        return !1;
    }
    function vr(e) {
        var t = [], n;
        for (n = 0; n < e.length; n++) {
            e[n].selected && t.push(e[n].value);
        }
        return t;
    }
    he = zt(function(t, n, r, i) {
        var a, d, o, l, c, u, f, h, p, m, v;
        a = t.tagName;
        d = a === "SELECT";
        o = a === "INPUT" || a === "TEXTAREA";
        if (!o && !d) {
            he(t, n, r, i);
            return;
        }
        if (n.ctx === e) {
            n.ctx = {
                data: e,
                me: n
            };
            n.component = s;
        }
        i === e && (n.ctx[ce] = r);
        l = d && t.multiple;
        c = !1;
        if (l) {
            u = t.options;
            f = vr(u);
            if (!pr(r, f)) {
                if (i === e || pr(f, i) || !pr(r, n.ctx[ce])) {
                    for (h = 0; h < u.length; h++) {
                        u[h].selected = mr(r, u[h].value);
                    }
                    f = vr(u);
                    pr(f, r) && (c = !0);
                } else {
                    c = !0;
                }
            }
        } else if (o || d) {
            if (o && hr(t)) {
                p = t.checked;
                if (r !== p) {
                    if (i === e || p === i || r !== n.ctx[ce]) {
                        t.checked = r;
                    } else {
                        c = !0;
                    }
                }
            } else {
                m = d && t.size < 2;
                v = t[fe];
                if (r !== v) {
                    if (i === e || v === i || r !== n.ctx[ce]) {
                        if (d) {
                            if (r === "") {
                                t.selectedIndex = m ? 0 : -1;
                            } else {
                                t[fe] = r;
                            }
                            if (r !== "" || m) {
                                v = t[fe];
                                r !== v && (c = !0);
                            }
                        } else {
                            t[fe] = r;
                        }
                    } else {
                        c = !0;
                    }
                }
            }
        }
        if (c) {
            br(e, t, n);
        } else {
            n.ctx[ce] = r;
        }
    });
    function br(t, n, r) {
        var i, a, d, o, l, c, u, f, h, p, m, v, b, g, y, x, C, w, _;
        if (n && n.nodeName === "OPTION") {
            n = document.activeElement;
            r = Cn(n);
        }
        if (!r) {
            return !1;
        }
        if (r.ctx === e) {
            r.ctx = {
                data: e,
                me: r
            };
            r.component = s;
        }
        i = r.ctx;
        a = n.tagName;
        d = a === "SELECT";
        o = d && n.multiple;
        if (o) {
            l = vr(n.options);
            if (!pr(i[ce], l)) {
                i[ce] = l;
                gr(r, l);
            }
        } else if (hr(n)) {
            if (t && t.type === "change") {
                P(function() {
                    br(e, n, r);
                }, 10);
                return !1;
            }
            if (n.type === "radio") {
                c = document.getElementsByName(n.name);
                for (u = 0; u < c.length; u++) {
                    f = c[u];
                    h = Cn(f);
                    if (!h) continue;
                    p = h.ctx;
                    m = f.checked;
                    if (p[ce] !== m) {
                        p[ce] = m;
                        gr(h, m);
                    }
                }
            } else {
                v = n.checked;
                if (i[ce] !== v) {
                    i[ce] = v;
                    gr(r, v);
                }
            }
        } else {
            b = n.value;
            if (i[ce] !== b) {
                i[ce] = b;
                gr(r, b);
            }
            g = n.selectionStart;
            y = n.selectionEnd;
            x = n.selectionDirection;
            C = !1;
            w = i[se];
            if (x == e) {
                y === w && (C = !0);
            } else x === "backward" && (C = !0);
            if (C) {
                _ = g;
                g = y;
                y = _;
            }
            yr(r, g, y);
        }
        return !1;
    }
    function gr(e, t) {
        var n, r, i, a, d;
        n = Ot;
        r = e.ctx;
        i = e.component;
        Ot = r;
        a = e.attrs && e.attrs[ce];
        Wt(a) && a(t);
        d = i && i.onChange;
        Wt(d) && d(r, t);
        Ot = n;
        ir(e, "onInput", {
            target: e,
            value: t
        });
    }
    function yr(e, t, n) {
        var r, i;
        r = e.component;
        i = e.ctx;
        if (r && (i[se] !== t || i[ue] !== n)) {
            i[se] = t;
            i[ue] = n;
            ir(e, "onSelectionChange", {
                target: e,
                startPosition: t,
                endPosition: n
            });
        }
    }
    function xr(e, t, n) {
        var r = ei();
        r && br(e, r.element, r);
        return !1;
    }
    ie = [ "input", "cut", "paste", "keydown", "keypress", "keyup", "click", "change" ];
    for (ae = 0; ae < ie.length; ae++) Mn(ie[ae], 10, br);
    pe = [ "!PointerDown", "!PointerMove", "!PointerUp", "!PointerCancel" ];
    for (ae = 0; ae < pe.length; ae++) Mn(pe[ae], 2, xr);
    function Cr(t) {
        return {
            target: e,
            shift: t.shiftKey,
            ctrl: t.ctrlKey,
            alt: t.altKey,
            meta: t.metaKey || !1,
            which: t.which || t.keyCode
        };
    }
    function wr(e, t, n) {
        var r;
        if (!n) return !1;
        r = Cr(e);
        if (ir(n, "onKeyDown", r)) {
            cr(e);
            return !0;
        }
        return !1;
    }
    function _r(e, t, n) {
        var r;
        if (!n) return !1;
        r = Cr(e);
        if (ir(n, "onKeyUp", r)) {
            cr(e);
            return !0;
        }
        return !1;
    }
    function kr(e, t, n) {
        var r;
        if (!n) return !1;
        if (e.which === 0 || e.altKey) return !1;
        r = {
            charCode: e.which || e.keyCode
        };
        if (ir(n, "onKeyPress", r)) {
            cr(e);
            return !0;
        }
        return !1;
    }
    Mn("keydown", 50, wr);
    Mn("keyup", 50, _r);
    Mn("keypress", 50, kr);
    me = 13;
    ve = 750;
    be = 500;
    ge = 800;
    ye = 50;
    xe = null;
    Ce = "onClick";
    we = "onDoubleClick";
    function Sr(t, n) {
        var r, i, a, d;
        if (xe == e) {
            return !1;
        }
        r = xe.me.component;
        i = r[t];
        if (!i) {
            return !1;
        }
        a = Ot;
        Ot = xe;
        d = i.call(r, xe, n);
        Ot = a;
        return d;
    }
    function Er(t) {
        var n, r;
        while (t) {
            n = t.style;
            if (n) {
                r = n.pointerEvents;
                if (r !== e) {
                    if (r === "none") return !0;
                    return !1;
                }
            }
            t = t.parent;
        }
        return !1;
    }
    function Ir(e) {
        var t;
        if (e.length) {
            for (t = e.length - 1; t >= 0; --t) {
                e[t].t.style.visibility = e[t].p;
            }
            return !0;
        }
        return !1;
    }
    function Nr(e, t) {
        e.push({
            t,
            p: t.style.visibility
        });
        t.style.visibility = "hidden";
    }
    function Dr(e, t) {
        Mn(e, 5, t);
    }
    _e = [ "PointerDown", "PointerMove", "PointerUp", "PointerCancel" ];
    function Pr(e) {
        if (e === "mouse" || e === 4) return 0;
        if (e === "pen" || e === 3) return 2;
        return 1;
    }
    function Ar(e, t, n, r) {
        var i = [], a;
        a = n;
        while (Er(r)) {
            Nr(i, a);
            a = document.elementFromPoint(e, t);
            r = Cn(a);
        }
        Ir(i);
        return [ a, r ];
    }
    function Rr(e) {
        return function t(n, r, i) {
            var a, d, o, l, c;
            r = document.elementFromPoint(n.clientX, n.clientY);
            i = Cn(r);
            if (Er(i)) {
                a = Ar(n.clientX, n.clientY, r, i);
                r = a[0];
                i = a[1];
            }
            d = n.button + 1;
            o = Pr(n.pointerType);
            l = n.buttons;
            if (d === 0 && o === 0 && l) {
                d = 1;
                while (!(l & 1)) {
                    l = l >> 1;
                    d++;
                }
            }
            c = {
                target: i,
                id: n.pointerId,
                cancelable: zr(n),
                type: o,
                x: n.clientX,
                y: n.clientY,
                button: d,
                shift: n.shiftKey,
                ctrl: n.ctrlKey,
                alt: n.altKey,
                meta: n.metaKey || !1,
                count: n.detail
            };
            if (Vn("!" + e, c, r, i)) {
                cr(n);
                return !0;
            }
            return !1;
        };
    }
    function Fr(e) {
        return function t(n, r, i) {
            var a = !1, d, o, l;
            for (d = 0; d < n.changedTouches.length; d++) {
                o = n.changedTouches[d];
                r = document.elementFromPoint(o.clientX, o.clientY);
                i = Cn(r);
                l = {
                    target: i,
                    id: o.identifier + 2,
                    cancelable: zr(n),
                    type: 1,
                    x: o.clientX,
                    y: o.clientY,
                    button: 1,
                    shift: n.shiftKey,
                    ctrl: n.ctrlKey,
                    alt: n.altKey,
                    meta: n.metaKey || !1,
                    count: n.detail
                };
                Vn("!" + e, l, r, i) && (a = !0);
            }
            if (a) {
                cr(n);
                return !0;
            }
            return !1;
        };
    }
    function jr(e) {
        return function t(n, r, i) {
            var a, d;
            r = document.elementFromPoint(n.clientX, n.clientY);
            i = Cn(r);
            if (Er(i)) {
                a = Ar(n.clientX, n.clientY, r, i);
                r = a[0];
                i = a[1];
            }
            d = {
                target: i,
                id: 1,
                type: 0,
                cancelable: zr(n),
                x: n.clientX,
                y: n.clientY,
                button: Yr(n),
                shift: n.shiftKey,
                ctrl: n.ctrlKey,
                alt: n.altKey,
                meta: n.metaKey || !1,
                count: n.detail
            };
            if (Vn("!" + e, d, r, i)) {
                cr(n);
                return !0;
            }
            return !1;
        };
    }
    function Br() {
        Dr("mousedown", jr(_e[0]));
        Dr("mousemove", jr(_e[1]));
        Dr("mouseup", jr(_e[2]));
    }
    if (window.ontouchstart !== e) {
        Dr("touchstart", Fr(_e[0]));
        Dr("touchmove", Fr(_e[1]));
        Dr("touchend", Fr(_e[2]));
        Dr("touchcancel", Fr(_e[3]));
        Br();
    } else if (window.onpointerdown !== e) {
        for (ae = 0; ae < 4; ae++) {
            ke = _e[ae];
            Dr(ke.toLowerCase(), Rr(ke));
        }
    } else if (window.onmspointerdown !== e) {
        for (ae = 0; ae < 4; ae++) {
            ke = _e[ae];
            Dr("@MS" + ke, Rr(ke));
        }
    } else {
        Br();
    }
    for (Se = 0; Se < 4; Se++) {
        (function(t) {
            var n = "on" + t;
            Mn("!" + t, 50, function(t, r, i) {
                return Sr(n, t) || ir(i, n, t) != e;
            });
        })(_e[Se]);
    }
    Ee = $t();
    Ie = [];
    Ne = -1;
    De = 0;
    Pe = 0;
    Ae = 0;
    Re = !1;
    function Or(e, t, n) {
        return Math.abs(e - t) < n;
    }
    Fe = [];
    function Tr(t) {
        var n, r, i, a, d, o, l, c;
        n = document.elementFromPoint(t.x, t.y);
        r = xn(n);
        i = r.length == 0 ? e : r[r.length - 1];
        if (Er(i)) {
            a = Ar(t.x, t.y, n, i == e ? e : i);
            n = a[0];
            r = xn(n);
        }
        ir(i, "onMouseOver", t);
        d = 0;
        while (d < Fe.length && d < r.length && Fe[d] === r[d]) d++;
        o = Fe.length;
        if (o > 0 && (o > d || o != r.length)) {
            l = Fe[o - 1];
            if (l) {
                c = l.component;
                c && c.onMouseOut && c.onMouseOut(l.ctx, t);
            }
        }
        while (o > d) {
            o--;
            l = Fe[o];
            if (l) {
                c = l.component;
                c && c.onMouseLeave && c.onMouseLeave(l.ctx, t);
            }
        }
        while (o < r.length) {
            l = r[o];
            if (l) {
                c = l.component;
                c && c.onMouseEnter && c.onMouseEnter(l.ctx, t);
            }
            o++;
        }
        Fe = r;
        if (o > 0 && (o > d || o != Fe.length)) {
            l = Fe[o - 1];
            if (l) {
                c = l.component;
                c && c.onMouseIn && c.onMouseIn(l.ctx, t);
            }
        }
        return !1;
    }
    function Mr() {
        return Object.keys(Ee).length === 0;
    }
    function Vr(e, t, n) {
        if (Ne === -1 && Mr()) {
            Ne = e.id;
            De = A();
            Pe = e.x;
            Ae = e.y;
            Re = !1;
            Tr(e);
        }
        Ee[e.id] = e.type;
        Ne !== e.id && (Re = !0);
        return !1;
    }
    function Ur(e, t, n) {
        if (e.type === 0 && e.button === 0 && Ee[e.id] != null) {
            e.button = 1;
            Vn("!PointerUp", e, t, n);
            e.button = 0;
        }
        if (Ne === e.id) {
            Tr(e);
            (!Or(Pe, e.x, me) || !Or(Ae, e.y, me)) && (Re = !0);
        } else Mr() && Tr(e);
        return !1;
    }
    je = 0;
    Be = 0;
    function Hr(e) {
        var t;
        if (Be == 0) return !1;
        t = A();
        if (t < je + 1e3 && e >= Be) {
            je = t;
            Be = e;
            return !0;
        }
        Be = 0;
        return !1;
    }
    function Lr(e, t, n) {
        var r, i;
        delete Ee[e.id];
        if (Ne == e.id) {
            Tr(e);
            Ne = -1;
            if (e.type == 1 && !Re) {
                if (A() - De < ve) {
                    Vn("!PointerCancel", e, t, n);
                    Hr(1);
                    r = Sr(Ce, e) || ir(n, Ce, e) != null;
                    i = Jt() ? ge : be;
                    Ie.push([ e.x, e.y, A() + i, r ? 1 : 0 ]);
                    return r;
                }
            } else Re && Ue(e.x, e.y);
        }
        return !1;
    }
    function Kr(e, t, n) {
        delete Ee[e.id];
        Ne == e.id && (Ne = -1);
        return !1;
    }
    function Wr(e, t, n) {
        var r, i, a;
        r = A();
        for (i = 0; i < Ie.length; i++) {
            a = Ie[i];
            if (a[2] < r) {
                Ie.splice(i, 1);
                i--;
                continue;
            }
            if (Or(a[0], e.clientX, ye) && Or(a[1], e.clientY, ye)) {
                Ie.splice(i, 1);
                a[3] && cr(e);
                return !0;
            }
        }
        return !1;
    }
    Oe = [ "!PointerDown", "!PointerMove", "!PointerUp", "!PointerCancel", "^click" ];
    Te = [ Vr, Ur, Lr, Kr, Wr ];
    for (ae = 0; ae < 5; ae++) {
        Mn(Oe[ae], 3, Te[ae]);
    }
    function Xr(e) {
        return function(t, n, r) {
            if (Ne != t.id && !Mr()) return !1;
            if (Sr(e, t) || ir(r, e, t)) {
                return !0;
            }
            return !1;
        };
    }
    Me = [ "Down", "Move", "Up", "Up" ];
    for (ae = 0; ae < 4; ae++) {
        Mn(Oe[ae], 80, Xr("onMouse" + Me[ae]));
    }
    function Yr(e) {
        return e.which || e.button;
    }
    function zr(e) {
        var t = e.cancelable;
        return !Kt(t) || t;
    }
    function $r(e, t) {
        return function(n, r, i) {
            var a, d;
            a = Yr(n) || 1;
            if (!t && a !== 1) return !1;
            d = {
                target: i,
                x: n.clientX,
                y: n.clientY,
                button: a,
                cancelable: zr(n),
                shift: n.shiftKey,
                ctrl: n.ctrlKey,
                alt: n.altKey,
                meta: n.metaKey || !1,
                count: n.detail || 1
            };
            e == we && (d.count = 2);
            if (Hr(d.count) || Sr(e, d) || ir(i, e, d)) {
                cr(n);
                return !0;
            }
            return !1;
        };
    }
    function Qr(e, t) {
        var n, r, i;
        n = document.elementFromPoint(e, t);
        r = Cn(n);
        if (Er(r)) {
            i = Ar(e, t, n, r);
            r = i[1];
        }
        return r;
    }
    function qr(e, t, n) {
        var r, i;
        while (n) {
            r = n.style;
            if (r) {
                i = r.userSelect;
                if (i === "none") {
                    cr(e);
                    return !0;
                }
                if (i) {
                    break;
                }
            }
            n = n.parent;
        }
        return !1;
    }
    Dr("selectstart", qr);
    Dr("^click", $r(Ce));
    Dr("^dblclick", $r(we));
    Dr("contextmenu", $r("onContextMenu", !0));
    Ve = ("onwheel" in document.createElement("div") ? "" : "mouse") + "wheel";
    function Gr(e, t, n) {
        var r, i, a, d, o, l;
        if (Er(n)) {
            r = Ar(e.x, e.y, t, n);
            t = r[0];
            n = r[1];
        }
        i = e.button + 1;
        a = e.buttons;
        if (i === 0 && a) {
            i = 1;
            while (!(a & 1)) {
                a = a >> 1;
                i++;
            }
        }
        d = 0;
        if (Ve == "mousewheel") {
            o = -1 / 40 * e.wheelDelta;
            e.wheelDeltaX && (d = -1 / 40 * e.wheelDeltaX);
        } else {
            d = e.deltaX;
            o = e.deltaY;
        }
        l = {
            target: n,
            dx: d,
            dy: o,
            x: e.clientX,
            y: e.clientY,
            cancelable: zr(e),
            button: i,
            shift: e.shiftKey,
            ctrl: e.ctrlKey,
            alt: e.altKey,
            meta: e.metaKey || !1,
            count: e.detail
        };
        if (Sr("onMouseWheel", l) || ir(n, "onMouseWheel", l)) {
            cr(e);
            return !0;
        }
        return !1;
    }
    Dr(Ve, Gr);
    Ue = function(e, t) {
        var n = Jt() ? ge : be;
        Ie.push([ e, t, A() + n, 1 ]);
    };
    He = e;
    Le = e;
    Ke = [];
    We = !1;
    function Zr(t) {
        var n, r, i, a, d, o;
        if (We) return !1;
        We = !0;
        while (!0) {
            n = document.hasFocus() || t ? document.activeElement : e;
            if (n === He) break;
            He = n;
            r = xn(He);
            i = 0;
            while (i < Ke.length && i < r.length && Ke[i] === r[i]) i++;
            a = Ke.length - 1;
            if (a >= i) {
                d = Ke[a];
                ir(d, "onBlur");
                a--;
            }
            while (a >= i) {
                d = Ke[a];
                if (d) {
                    o = d.component;
                    o && o.onFocusOut && o.onFocusOut(d.ctx);
                }
                a--;
            }
            a = i;
            while (a + 1 < r.length) {
                d = r[a];
                if (d) {
                    o = d.component;
                    o && o.onFocusIn && o.onFocusIn(d.ctx);
                }
                a++;
            }
            if (a < r.length) {
                d = r[a];
                ir(d, "onFocus");
            }
            Ke = r;
            Le = Ke.length == 0 ? e : Ut(Ke[Ke.length - 1]);
        }
        We = !1;
        return !1;
    }
    function Jr() {
        P(function() {
            return Zr(!1);
        }, 10);
        return !1;
    }
    Mn("^focus", 50, function() {
        return Zr(!0);
    });
    Mn("^blur", 50, Jr);
    function ei() {
        return Le;
    }
    Xe = [];
    function ti(e, t, n) {
        var r, i;
        r = {
            node: n
        };
        for (i = 0; i < Xe.length; i++) {
            Xe[i](r);
        }
        or("onScroll", r);
        return !1;
    }
    Mn("^scroll", 10, ti);
    Ye = 0;
    ze = [];
    $e = null;
    Qe = null;
    qe = function(t) {
        this.id = ++Ye;
        this.pointerid = t;
        this.enabledOperations = 7;
        this.operation = 0;
        this.started = !1;
        this.beforeDrag = !0;
        this.local = !0;
        this.system = !1;
        this.ended = !1;
        this.cursor = null;
        this.overNode = e;
        this.targetCtx = null;
        this.dragView = e;
        this.startX = 0;
        this.startY = 0;
        this.distanceToStart = 10;
        this.x = 0;
        this.y = 0;
        this.deltaX = 0;
        this.deltaY = 0;
        this.totalX = 0;
        this.totalY = 0;
        this.lastX = 0;
        this.lastY = 0;
        this.shift = !1;
        this.ctrl = !1;
        this.alt = !1;
        this.meta = !1;
        this.data = $t();
        t >= 0 && (tt[t] = this);
        ze.push(this);
    };
    Ge = "b-dragging";
    function ni() {
        var t;
        if (Qe == e) {
            t = document.documentElement;
            t.classList.add(Ge);
            Qe = Gn(ii);
        }
    }
    Ze = {
        render: function(e, t) {
            var n = e.data;
            t.tag = "div";
            t.style = {
                position: "absolute",
                left: n.x,
                top: n.y
            };
            t.children = n.dragView(n);
        }
    };
    function ri() {
        var e = "no-drop", t;
        if (ze.length !== 0) {
            t = ze[0];
            if (t.beforeDrag) return "";
            if (t.cursor != null) return t.cursor;
            if (t.system) return "";
            switch (t.operation) {
              case 3:
                e = "move";
                break;

              case 1:
                e = "alias";
                break;

              case 2:
                e = "copy";
            }
        }
        return e;
    }
    Je = {
        render: function(e, t) {
            var n = [], r, i, a, d;
            for (r = 0; r < ze.length; r++) {
                i = ze[r];
                if (i.beforeDrag) continue;
                i.dragView != null && (i.x != 0 || i.y != 0) && n.push({
                    key: "" + i.id,
                    data: i,
                    component: Ze
                });
            }
            t.tag = "div";
            t.style = {
                position: "fixed",
                zIndex: 1e9,
                pointerEvents: "none",
                userSelect: "none",
                left: 0,
                top: 0,
                right: 0,
                bottom: 0
            };
            a = document.documentElement.style;
            d = ri();
            if (d) {
                a.cursor !== d && a.setProperty("cursor", d, "important");
            } else {
                a.setProperty("cursor", "");
            }
            t.children = n;
        },
        onDrag: function(e) {
            J(e);
            return !1;
        }
    };
    function ii() {
        return {
            component: Je
        };
    }
    et = qe.prototype;
    et.setOperation = function(e) {
        this.operation = e;
    };
    et.setDragNodeView = function(e) {
        this.dragView = e;
    };
    et.addData = function(e, t) {
        this.data[e] = t;
        return !0;
    };
    et.listData = function() {
        return Object.keys(this.data);
    };
    et.hasData = function(t) {
        return this.data[t] !== e;
    };
    et.getData = function(e) {
        return this.data[e];
    };
    et.setEnabledOps = function(e) {
        this.enabledOperations = e;
    };
    et.cancelDnd = function() {
        di(e, this);
        this.destroy();
    };
    et.destroy = function() {
        var e, t;
        this.ended = !0;
        this.started && lr("onDragEnd", this);
        delete tt[this.pointerid];
        for (e = 0; e < ze.length; e++) {
            if (ze[e] === this) {
                ze.splice(e, 1);
                break;
            }
        }
        $e === this && ($e = null);
        if (ze.length === 0 && Qe != null) {
            Zn(Qe);
            Qe = null;
            t = document.documentElement;
            t.classList.remove(Ge);
            t.style.setProperty("cursor", "");
        }
    };
    tt = $t();
    function ai(t, n, r) {
        var i, a, d, o, l;
        i = tt[t.id];
        i && i.cancelDnd();
        if (t.button <= 1) {
            i = new qe(t.id);
            i.startX = t.x;
            i.startY = t.y;
            i.lastX = t.x;
            i.lastY = t.y;
            i.overNode = r;
            oi(i, t);
            a = ir(r, "onDragStart", i);
            if (a) {
                d = Sn(a.me);
                if (d == e) {
                    i.destroy();
                    return !1;
                }
                i.started = !0;
                o = d.getBoundingClientRect;
                if (o) {
                    l = o.call(d);
                    i.deltaX = l.left - t.x;
                    i.deltaY = l.top - t.y;
                }
                if (i.distanceToStart <= 0) {
                    i.beforeDrag = !1;
                    di(r, i);
                }
                ni();
            } else {
                i.destroy();
            }
        }
        return !1;
    }
    function di(t, n) {
        n.overNode = t;
        n.targetCtx = ir(t, "onDragOver", n);
        n.targetCtx == e && (n.operation = 0);
        lr("onDrag", n);
    }
    function oi(e, t) {
        e.shift = t.shift;
        e.ctrl = t.ctrl;
        e.alt = t.alt;
        e.meta = t.meta;
        e.x = t.x;
        e.y = t.y;
    }
    function li(e, t, n) {
        var r = tt[e.id];
        if (!r) return !1;
        r.totalX += Math.abs(e.x - r.lastX);
        r.totalY += Math.abs(e.y - r.lastY);
        if (r.beforeDrag) {
            if (r.totalX + r.totalY <= r.distanceToStart) {
                r.lastX = e.x;
                r.lastY = e.y;
                return !1;
            }
            r.beforeDrag = !1;
        }
        oi(r, e);
        di(n, r);
        r.lastX = e.x;
        r.lastY = e.y;
        return !0;
    }
    function ci(e, t, n) {
        var r, i;
        r = tt[e.id];
        if (!r) return !1;
        if (!r.beforeDrag) {
            oi(r, e);
            di(n, r);
            i = r.targetCtx;
            if (i && ir(i.me, "onDrop", r)) {
                r.destroy();
            } else {
                r.cancelDnd();
            }
            Ue(e.x, e.y);
            return !0;
        }
        r.destroy();
        return !1;
    }
    function si(e, t, n) {
        var r = tt[e.id];
        if (!r) return !1;
        if (r.system) return !1;
        if (!r.beforeDrag) {
            r.cancelDnd();
        } else {
            r.destroy();
        }
        return !1;
    }
    function ui(e, t) {
        var n;
        e.shift = t.shiftKey;
        e.ctrl = t.ctrlKey;
        e.alt = t.altKey;
        e.meta = t.metaKey;
        e.x = t.clientX;
        e.y = t.clientY;
        e.totalX += Math.abs(e.x - e.lastX);
        e.totalY += Math.abs(e.y - e.lastY);
        n = Qr(e.x, e.y);
        di(n, e);
        e.lastX = e.x;
        e.lastY = e.y;
    }
    nt = [ "none", "link", "copy", "copyLink", "move", "linkMove", "copyMove", "all" ];
    function fi(t, n, r) {
        var i, a, d, o, l, c, s, u, f, h, p, m, v, b, g, y, x, C, w, _, k;
        i = $e;
        i != null && i.destroy();
        a = Object.keys(tt);
        if (a.length > 0) {
            i = tt[a[0]];
            i.system = !0;
            $e = i;
        } else {
            d = t.clientX;
            o = t.clientY;
            i = new qe(-1);
            i.system = !0;
            $e = i;
            i.x = d;
            i.y = o;
            i.lastX = d;
            i.lastY = o;
            i.startX = d;
            i.startY = o;
            l = ir(r, "onDragStart", i);
            if (l) {
                c = Sn(l.me);
                if (c == e) {
                    i.destroy();
                    return !1;
                }
                i.started = !0;
                s = c.getBoundingClientRect;
                if (s) {
                    u = s.call(c);
                    i.deltaX = u.left - d;
                    i.deltaY = u.top - o;
                }
                ni();
            } else {
                i.destroy();
                return !1;
            }
        }
        i.beforeDrag = !1;
        f = nt[i.enabledOperations];
        h = t.dataTransfer;
        h.effectAllowed = f;
        if (h.setDragImage) {
            p = document.createElement("div");
            p.style.pointerEvents = "none";
            h.setDragImage(p, 0, 0);
        } else {
            m = t.target.style;
            v = m.opacity;
            b = m.width;
            g = m.height;
            y = m.padding;
            m.opacity = "0";
            m.width = "0";
            m.height = "0";
            m.padding = "0";
            P(function() {
                m.opacity = v;
                m.width = b;
                m.height = g;
                m.padding = y;
            }, 0);
        }
        x = i.data;
        C = Object.keys(x);
        for (w = 0; w < C.length; w++) {
            try {
                _ = C[w];
                k = x[_];
                Lt(k) || (k = JSON.stringify(k));
                t.dataTransfer.setData(_, k);
            } catch (e) {}
        }
        ui(i, t);
        return !1;
    }
    function hi(e, t) {
        e.dataTransfer.dropEffect = [ "none", "link", "copy", "move" ][t];
    }
    function pi(t, n, r) {
        var i, a, d, o, l, c, s;
        i = $e;
        if (i == e) {
            i = new qe(-1);
            i.system = !0;
            $e = i;
            i.x = t.clientX;
            i.y = t.clientY;
            i.startX = i.x;
            i.startY = i.y;
            i.local = !1;
            a = t.dataTransfer;
            d = 0;
            o = e;
            try {
                o = a.effectAllowed;
            } catch (e) {}
            for (;d < 7; d++) {
                if (nt[d] === o) break;
            }
            i.enabledOperations = d;
            l = a.types;
            if (l) {
                for (c = 0; c < l.length; c++) {
                    s = l[c];
                    if (s === "text/plain") s = "Text"; else s === "text/uri-list" && (s = "Url");
                    i.data[s] = null;
                }
            } else {
                a.getData("Text") !== e && (i.data["Text"] = null);
            }
        }
        ui(i, t);
        hi(t, i.operation);
        if (i.operation != 0) {
            cr(t);
            return !0;
        }
        return !1;
    }
    function mi(e, t, n) {
        var r, i, a;
        r = e.clientX;
        i = e.clientY;
        a = fr();
        if ($e != null && (r === 0 && i === 0 || r < 0 || i < 0 || r >= a.width || i >= a.height)) {
            $e.x = 0;
            $e.y = 0;
            $e.operation = 0;
            lr("onDrag", $e);
        }
        return !0;
    }
    function vi(e, t, n) {
        $e != null && $e.destroy();
        return !1;
    }
    function bi(t, n, r) {
        var i, a, d, o, l, c, s;
        i = $e;
        if (i == e) return !1;
        i.x = t.clientX;
        i.y = t.clientY;
        if (!i.local) {
            a = Object.keys(i.data);
            d = t.dataTransfer;
            for (o = 0; o < a.length; o++) {
                l = a[o];
                if (l === "Files") {
                    c = [].slice.call(d.files, 0);
                } else {
                    c = d.getData(l);
                }
                i.data[l] = c;
            }
        }
        ui(i, t);
        s = i.targetCtx;
        if (s && ir(s.me, "onDrop", i)) {
            hi(t, i.operation);
            i.destroy();
            cr(t);
        } else {
            i.cancelDnd();
        }
        return !0;
    }
    function gi(e, t, n) {
        cr(e);
        return !0;
    }
    function yi(e, t, n) {
        if (ze.length === 0) return !1;
        cr(e);
        return !0;
    }
    Mn("!PointerDown", 4, ai);
    Mn("!PointerMove", 4, li);
    Mn("!PointerUp", 4, ci);
    Mn("!PointerCancel", 4, si);
    Mn("selectstart", 4, yi);
    Mn("dragstart", 5, fi);
    Mn("dragover", 5, pi);
    Mn("dragend", 5, vi);
    Mn("drag", 5, mi);
    Mn("drop", 5, bi);
    Mn("dragenter", 5, gi);
    Mn("dragleave", 5, gi);
    rt = function() {
        return ze;
    };
    it = -1;
    function xi() {
        it >= 0 && clearTimeout(it);
        it = -1;
        J();
        return !1;
    }
    Mn("hashchange", 10, xi);
    $t();
    function Ci() {
        return e;
    }
    at = $t();
    dt = $t();
    ot = $t();
    $t();
    lt = $t();
    $t();
    ct = [];
    st = [];
    ut = $t();
    ft = "";
    ht = !1;
    pt = null;
    mt = 0;
    vt = Kn(Si);
    bt = /\:|\ |\>/;
    function wi(e) {
        var t, n;
        t = bt.exec(e);
        if (!t) return at[e].name;
        n = t.index;
        return at[e.substring(0, n)].name + e.substring(n);
    }
    function _i(e, t) {
        var n = "", r;
        if (e) {
            if (l(e)) {
                for (r = 0; r < e.length; r++) {
                    r > 0 && (n += ",");
                    n += "." + wi(e[r]) + "." + t;
                }
            } else {
                n = "." + wi(e) + "." + t;
            }
        } else {
            n = "." + t;
        }
        return n;
    }
    function ki(t, n, r, i) {
        var a, d, o, c, s, f;
        if (Lt(r)) {
            a = at[r];
            if (a === e) {
                throw new Error("Unknown style " + r);
            }
            ki(t, n, a.style, a.pseudo);
        } else if (Wt(r)) {
            r(t, n);
        } else if (l(r)) {
            for (d = 0; d < r.length; d++) {
                ki(t, n, r[d], e);
            }
        } else if (typeof r === "object") {
            for (s in r) {
                if (!u.call(r, s)) continue;
                o = r[s];
                Wt(o) && (o = o(t, s));
                t[s] = o;
            }
        }
        if (i != e && n != e) {
            for (f in i) {
                c = n[f];
                if (c === e) {
                    c = $t();
                    n[f] = c;
                }
                ki(c, e, i[f], e);
            }
        }
    }
    gt = 0;
    yt = "";
    xt = 1;
    Ct = !1;
    wt = !0;
    function Si() {
        var r, i, a, d, o, l, c, s, u, f, h, p, m, v, b, g, y, x, C, w, _, k, S, E, I, N, D, P, A, R, F, j, B, O, T, M, V, U, H, L, K, W, X, Y, z, $;
        if (Ct && gt != fr().dppx) {
            gt = fr().dppx;
            r = It;
            i = 1;
            if (gt > 1) {
                for (a = 0; a < Nt.length; a++) {
                    if (a == Nt.length - 1 || Nt[a][1] >= gt) {
                        r = Nt[a][0];
                        i = Nt[a][1];
                    } else break;
                }
            }
            if (yt != r) {
                yt = r;
                xt = i;
                ht = !0;
                wt = !0;
            }
        }
        if (ht) {
            if (Ct) {
                d = ut[yt];
                if (d === e) {
                    d = null;
                    ut[yt] = d;
                    Ai(yt, function(e) {
                        ut[yt] = e;
                        Di();
                    });
                }
                if (d != null) {
                    for (o = 0; o < st.length; o++) {
                        l = st[o];
                        c = l.color;
                        Lt(c) || (c = c());
                        if (wt || c !== l.lastColor) {
                            l.lastColor = c;
                            s = l.width * xt | 0;
                            u = l.height * xt | 0;
                            f = Pi(d, c, s, u, l.left * xt | 0, l.top * xt | 0);
                            h = at[l.styleId];
                            h.style = {
                                backgroundImage: "url(" + f + ")",
                                width: l.width,
                                height: l.height,
                                backgroundPosition: 0,
                                backgroundSize: "100%"
                            };
                        }
                    }
                    if (wt) {
                        p = d.width / xt;
                        m = d.height / xt;
                        for (Y in lt) {
                            v = lt[Y];
                            if (v.color !== e) continue;
                            h = at[v.styleId];
                            b = v.width;
                            g = v.height;
                            y = 100 * p / b;
                            x = 100 * m / g;
                            h.style = {
                                backgroundImage: "url(" + yt + ")",
                                width: b,
                                height: g,
                                backgroundPosition: 100 * v.left / (p - b) + "% " + 100 * v.top / (m - g) + "%",
                                backgroundSize: y + "% " + x + "%"
                            };
                        }
                    }
                    wt = !1;
                }
            }
            for (C = 0; C < ct.length; C++) {
                l = ct[C];
                w = ut[l.url];
                if (w == e) continue;
                c = l.color();
                if (c !== l.lastColor) {
                    l.lastColor = c;
                    l.width == e && (l.width = w.width);
                    l.height == e && (l.height = w.height);
                    f = Pi(w, c, l.width, l.height, l.left, l.top);
                    h = at[l.styleId];
                    h.style = {
                        backgroundImage: "url(" + f + ")",
                        width: l.width,
                        height: l.height,
                        backgroundPosition: 0
                    };
                }
            }
            _ = ft;
            for (z in dt) {
                k = dt[z];
                _ += "@keyframes " + k.name + " {";
                for ($ in k.def) {
                    S = k.def[$];
                    E = $t();
                    ki(E, e, S, e);
                    en(E);
                    _ += $ + ($ == "from" || $ == "to" ? "" : "%") + " {" + Ii(E) + "}\n";
                }
                _ += "}\n";
            }
            for (z in at) {
                I = at[z];
                N = I.parent;
                D = I.name;
                P = I.pseudo;
                A = I.style;
                Wt(A) && A.length === 0 && (R = n(A(), 2), A = R[0], P = R[1]);
                if (Lt(A) && P == e) {
                    I.realName = A;
                    continue;
                }
                I.realName = D;
                F = $t();
                j = $t();
                ki(e, j, e, P);
                ki(F, j, A, e);
                B = null;
                if (F["pointerEvents"]) {
                    B = $t();
                    B["pointerEvents"] = F["pointerEvents"];
                }
                I.inlStyle = B;
                en(F);
                O = Ii(F);
                O.length > 0 && (_ += (D == e ? N : _i(N, D)) + " {" + O + "}\n");
                for ($ in j) {
                    S = j[$];
                    en(S);
                    _ += (D == e ? N + ":" + $ : _i(N, D + ":" + $)) + " {" + Ii(S) + "}\n";
                }
            }
            for (z in ot) {
                T = ot[z];
                _ += "@media " + z + "{";
                try {
                    for (M = (L = void 0, t(T)), V = M.next(); !V.done; V = M.next()) {
                        U = V.value;
                        for ($ in U) {
                            S = U[$];
                            H = $t();
                            ki(H, e, S, e);
                            en(H);
                            _ += "." + $ + " {" + Ii(H) + "}\n";
                        }
                    }
                } catch (e) {
                    L = {
                        error: e
                    };
                } finally {
                    try {
                        V && !V.done && (K = M.return) && K.call(M);
                    } finally {
                        if (L) throw L.error;
                    }
                }
                _ += "}\n";
            }
            W = document.createElement("style");
            W.type = "text/css";
            if (W.styleSheet) {
                W.styleSheet.cssText = _;
            } else {
                W.appendChild(document.createTextNode(_));
            }
            X = document.head || document.getElementsByTagName("head")[0];
            if (pt != null) {
                X.replaceChild(W, pt);
            } else {
                X.appendChild(W);
            }
            pt = W;
            ht = !1;
        }
        vt();
    }
    _t = /([A-Z])/g;
    kt = /^ms-/;
    function Ei(e) {
        if (e === "cssFloat") return "float";
        return e.replace(_t, "-$1").toLowerCase().replace(kt, "-ms-");
    }
    function Ii(t) {
        var n = "", r, i;
        for (i in t) {
            r = t[i];
            if (r === e) continue;
            n += Ei(i) + ":" + (r === "" ? '""' : r) + ";";
        }
        n = n.slice(0, -1);
        return n;
    }
    function Ni(t, n, r) {
        at["b-" + mt++] = {
            name: null,
            realName: null,
            parent: t,
            style: n,
            inlStyle: e,
            pseudo: r
        };
        Di();
    }
    function Di() {
        ht = !0;
        J();
    }
    St = /\s*rgba\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d+|\d*\.\d+)\s*\)\s*/;
    function Pi(e, t, n, r, i, a) {
        var d, o, l, c, s, u, f, h, p, m, v, b;
        d = document.createElement("canvas");
        d.width = n;
        d.height = r;
        o = d.getContext("2d");
        o.drawImage(e, -i, -a);
        l = o.getImageData(0, 0, n, r);
        c = l.data;
        s = St.exec(t);
        if (s) {
            u = parseInt(s[1], 10);
            f = parseInt(s[2], 10);
            h = parseInt(s[3], 10);
            p = Math.round(parseFloat(s[4]) * 255);
        } else {
            u = parseInt(t.substr(1, 2), 16);
            f = parseInt(t.substr(3, 2), 16);
            h = parseInt(t.substr(5, 2), 16);
            p = parseInt(t.substr(7, 2), 16) || 255;
        }
        if (p === 255) {
            for (m = 0; m < c.length; m += 4) {
                v = c[m];
                if (v === c[m + 1] && v === c[m + 2] && (v === 128 || c[m + 3] < 255 && v > 112)) {
                    c[m] = u;
                    c[m + 1] = f;
                    c[m + 2] = h;
                }
            }
        } else {
            for (m = 0; m < c.length; m += 4) {
                v = c[m];
                b = c[m + 3];
                if (v === c[m + 1] && v === c[m + 2] && (v === 128 || b < 255 && v > 112)) {
                    if (b === 255) {
                        c[m] = u;
                        c[m + 1] = f;
                        c[m + 2] = h;
                        c[m + 3] = p;
                    } else {
                        b = b * (1 / 255);
                        c[m] = Math.round(u * b);
                        c[m + 1] = Math.round(f * b);
                        c[m + 2] = Math.round(h * b);
                        c[m + 3] = Math.round(p * b);
                    }
                }
            }
        }
        o.putImageData(l, 0, 0);
        return d.toDataURL();
    }
    Et = !1;
    function Ai(e, t) {
        var n = new Image();
        n.crossOrigin = Et ? "use-credentials" : "anonymous";
        n.addEventListener("load", function() {
            return t(n);
        });
        n.src = e;
    }
    It = window["bobrilBPath"] || "bundle.png";
    Nt = window["bobrilBPath2"] || [];
    Ni("html." + Ge + " *", {
        cursor: "inherit !important",
        userSelect: "none !important"
    });
    function Ri(t) {
        return function(n, r) {
            if (r !== e) {
                n == e && (n = {});
                n.children = r;
            }
            return {
                data: n,
                component: t
            };
        };
    }
    window.b || (window.b = {
        deref: Cn,
        getRoots: Jn,
        setInvalidate: qn,
        invalidateStyles: Di,
        ignoreShouldChange: Qn,
        setAfterFrame: Wn,
        setBeforeFrame: Kn,
        getDnds: rt,
        setBeforeInit: rr
    });
    (function(e) {
        e[e["NotHandled"] = 0] = "NotHandled";
        e[e["HandledPreventDefault"] = 1] = "HandledPreventDefault";
        e[e["HandledButRunDefault"] = 2] = "HandledButRunDefault";
        e[e["NotHandledPreventDefault"] = 3] = "NotHandledPreventDefault";
    })(Dt);
    function Fi(t) {
        var n, r, i, a, d;
        n = t.$hooks;
        r = n.length;
        for (i = 0; i < r; i++) {
            a = n[i];
            d = a.postInitDom;
            d !== e && d.call(a, t);
        }
    }
    function ji(t) {
        var n, r, i, a, d;
        n = t.$hooks;
        r = n.length;
        for (i = 0; i < r; i++) {
            a = n[i];
            d = a.postUpdateDom;
            d !== e && d.call(a, t);
        }
    }
    function Bi(t) {
        var n, r, i, a, d;
        n = t.$hooks;
        r = n.length;
        for (i = 0; i < r; i++) {
            a = n[i];
            d = a.postUpdateDomEverytime;
            d !== e && d.call(a, t);
        }
    }
    Pt = [];
    function Oi() {
        var e, t, n;
        e = Pt;
        Pt = [];
        for (t = 0, n = e.length; t < n; t++) {
            e[t]();
        }
    }
    At = function() {
        function e() {}
        return e;
    }();
    Rt = function() {
        function e() {}
        return e;
    }();
    nr(function() {
        return "hello";
    });
})();

