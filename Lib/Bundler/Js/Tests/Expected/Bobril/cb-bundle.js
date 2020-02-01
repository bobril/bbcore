!function(undefined) {
    "use strict";
    var __export_isArray = Array.isArray, emptyComponent = {};
    function createTextNode(content) {
        return document.createTextNode(content);
    }
    function createEl(name_bobril) {
        return document.createElement(name_bobril);
    }
    function null2undefined(value) {
        return null === value ? undefined : value;
    }
    function isNumber(val) {
        return "number" == typeof val;
    }
    function isString(val) {
        return "string" == typeof val;
    }
    function isFunction(val) {
        return "function" == typeof val;
    }
    function isObject(val) {
        return "object" == typeof val;
    }
    null == Object.assign && (Object.assign = function(target) {
        for (var _sources = [], _i = 1; _i < arguments.length; _i++) _sources[_i - 1] = arguments[_i];
        if (null == target) throw new TypeError("Target in assign cannot be undefined or null");
        for (var totalArgs = arguments.length, i_1 = 1; i_1 < totalArgs; i_1++) {
            var source = arguments[i_1];
            if (null != source) for (var keys = Object.keys(source), totalKeys = keys.length, j_1 = 0; j_1 < totalKeys; j_1++) {
                var key = keys[j_1];
                target[key] = source[key];
            }
        }
        return target;
    });
    var __export_assign = Object.assign;
    var inSvg = !1, inNotFocusable = !1, updateCall = [], updateInstance = [], setValueCallback = function(el, _node, newValue, oldValue) {
        newValue !== oldValue && (el[tValue] = newValue);
    };
    function setSetValue(callback) {
        var prev = setValueCallback;
        return setValueCallback = callback, prev;
    }
    function newHashObj() {
        return Object.create(null);
    }
    var vendors = [ "Webkit", "Moz", "ms", "O" ], testingDivStyle = document.createElement("div").style;
    function testPropExistence(name_bobril) {
        return isString(testingDivStyle[name_bobril]);
    }
    var mapping = newHashObj(), isUnitlessNumber = {
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
    function renamer(newName) {
        return function(style_bobril, value, oldName) {
            style_bobril[newName] = value, style_bobril[oldName] = undefined;
        };
    }
    function renamerPx(newName) {
        return function(style_bobril, value, oldName) {
            isNumber(value) ? style_bobril[newName] = value + "px" : style_bobril[newName] = value, 
            style_bobril[oldName] = undefined;
        };
    }
    function pxAdder(style_bobril, value, name_bobril) {
        isNumber(value) && (style_bobril[name_bobril] = value + "px");
    }
    function ieVersion() {
        return document.documentMode;
    }
    function shimStyle(newValue) {
        for (var k = Object.keys(newValue), i_bobril = 0, l = k.length; i_bobril < l; i_bobril++) {
            var ki = k[i_bobril], mi = mapping[ki], vi = newValue[ki];
            if (vi !== undefined) {
                if (mi === undefined) {
                    if (testPropExistence(ki)) mi = !0 === isUnitlessNumber[ki] ? null : pxAdder; else {
                        for (var titleCaseKi = ki.replace(/^\w/, function(match) {
                            return match.toUpperCase();
                        }), j_bobril = 0; j_bobril < vendors.length; j_bobril++) if (testPropExistence(vendors[j_bobril] + titleCaseKi)) {
                            mi = (!0 === isUnitlessNumber[ki] ? renamer : renamerPx)(vendors[j_bobril] + titleCaseKi);
                            break;
                        }
                        mi === undefined && (mi = !0 === isUnitlessNumber[ki] ? null : pxAdder);
                    }
                    mapping[ki] = mi;
                }
                null !== mi && mi(newValue, vi, ki);
            }
        }
    }
    function removeProperty(s, name_bobril) {
        s[name_bobril] = "";
    }
    function setStyleProperty(s, name_bobril, value) {
        if (isString(value)) {
            var len = value.length;
            if (11 < len && " !important" === value.substr(len - 11, 11)) return void s.setProperty(name_bobril, value.substr(0, len - 11), "important");
        }
        s[name_bobril] = value;
    }
    function updateStyle(el, newStyle, oldStyle) {
        var rule, s = el.style;
        if (isObject(newStyle)) if (shimStyle(newStyle), isObject(oldStyle)) {
            for (rule in oldStyle) rule in newStyle || removeProperty(s, rule);
            for (rule in newStyle) {
                (v = newStyle[rule]) !== undefined ? oldStyle[rule] !== v && setStyleProperty(s, rule, v) : removeProperty(s, rule);
            }
        } else for (rule in oldStyle && (s.cssText = ""), newStyle) {
            var v;
            (v = newStyle[rule]) !== undefined && setStyleProperty(s, rule, v);
        } else if (newStyle) s.cssText = newStyle; else if (isObject(oldStyle)) for (rule in oldStyle) removeProperty(s, rule); else oldStyle && (s.cssText = "");
    }
    function setClassName(el, className) {
        inSvg ? el.setAttribute("class", className) : el.className = className;
    }
    var focusableTag = /^input$|^select$|^textarea$|^button$/, tabindexStr = "tabindex";
    function updateElement(n, el, newAttrs, oldAttrs, notFocusable) {
        var attrName, newAttr, oldAttr, valueOldAttr, valueNewAttr, wasTabindex = !1;
        if (null != newAttrs) for (attrName in newAttrs) {
            if (newAttr = newAttrs[attrName], oldAttr = oldAttrs[attrName], notFocusable && attrName === tabindexStr) newAttr = -1, 
            wasTabindex = !0; else if (attrName === tValue && !inSvg) {
                isFunction(newAttr) && (newAttr = (oldAttrs[bValue] = newAttr)()), valueOldAttr = oldAttr, 
                valueNewAttr = newAttr, oldAttrs[attrName] = newAttr;
                continue;
            }
            oldAttr !== newAttr && (oldAttrs[attrName] = newAttr, inSvg ? "href" === attrName ? el.setAttributeNS("http://www.w3.org/1999/xlink", "href", newAttr) : el.setAttribute(attrName, newAttr) : attrName in el && "list" !== attrName && "form" !== attrName ? el[attrName] = newAttr : el.setAttribute(attrName, newAttr));
        }
        if (notFocusable && !wasTabindex && n.tag && focusableTag.test(n.tag) && (el.setAttribute(tabindexStr, "-1"), 
        oldAttrs[tabindexStr] = -1), null == newAttrs) {
            for (attrName in oldAttrs) if (oldAttrs[attrName] !== undefined) {
                if (notFocusable && attrName === tabindexStr) continue;
                if (attrName === bValue) continue;
                oldAttrs[attrName] = undefined, el.removeAttribute(attrName);
            }
        } else for (attrName in oldAttrs) if (oldAttrs[attrName] !== undefined && !(attrName in newAttrs)) {
            if (notFocusable && attrName === tabindexStr) continue;
            if (attrName === bValue) continue;
            oldAttrs[attrName] = undefined, el.removeAttribute(attrName);
        }
        return valueNewAttr !== undefined && setValueCallback(el, n, valueNewAttr, valueOldAttr), 
        oldAttrs;
    }
    function pushInitCallback(c) {
        var cc = c.component;
        if (cc) {
            var fn = cc.postInitDom;
            fn && (updateCall.push(fn), updateInstance.push(c));
        }
    }
    function pushUpdateCallback(c) {
        var cc = c.component;
        if (cc) {
            var fn = cc.postUpdateDom;
            fn && (updateCall.push(fn), updateInstance.push(c)), (fn = cc.postUpdateDomEverytime) && (updateCall.push(fn), 
            updateInstance.push(c));
        }
    }
    function pushUpdateEverytimeCallback(c) {
        var cc = c.component;
        if (cc) {
            var fn = cc.postUpdateDomEverytime;
            fn && (updateCall.push(fn), updateInstance.push(c));
        }
    }
    function findCfg(parent) {
        for (var cfg; parent && (cfg = parent.cfg) === undefined; ) {
            if (parent.ctx) {
                cfg = parent.ctx.cfg;
                break;
            }
            parent = parent.parent;
        }
        return cfg;
    }
    function setRef(ref, value) {
        if (null != ref) if (isFunction(ref)) ref(value); else {
            var ctx = ref[0], refs = ctx.refs;
            null == refs && (refs = newHashObj(), ctx.refs = refs), refs[ref[1]] = value;
        }
    }
    var currentCtx, focusRootStack = [], focusRootTop = null;
    function createNode(n, parentNode, createInto, createBefore) {
        var el, ctx, c = {
            tag: n.tag,
            key: n.key,
            ref: n.ref,
            className: n.className,
            style: n.style,
            attrs: n.attrs,
            children: n.children,
            component: n.component,
            data: n.data,
            cfg: undefined,
            parent: parentNode,
            element: undefined,
            ctx: undefined,
            orig: n
        }, backupInSvg = inSvg, backupInNotFocusable = inNotFocusable, component = c.component;
        (setRef(c.ref, c), component) && (component.ctxClass ? ((ctx = new component.ctxClass(c.data || {}, c)).data === undefined && (ctx.data = c.data || {}), 
        ctx.me === undefined && (ctx.me = c)) : ctx = {
            data: c.data || {},
            me: c,
            cfg: undefined
        }, ctx.cfg = n.cfg === undefined ? findCfg(parentNode) : n.cfg, c.ctx = ctx, currentCtx = ctx, 
        component.init && component.init(ctx, c), beforeRenderCallback !== emptyBeforeRenderCallback && beforeRenderCallback(n, 0), 
        component.render && component.render(ctx, c), currentCtx = undefined);
        var tag = c.tag;
        if ("-" === tag) return c.tag = undefined, c.children = undefined, c;
        var children = c.children, inSvgForeignObject = !1;
        if (isNumber(children) && (children = "" + children, c.children = children), tag === undefined) return isString(children) ? (el = createTextNode(children), 
        c.element = el, createInto.insertBefore(el, createBefore)) : createChildren(c, createInto, createBefore), 
        component && (component.postRender && component.postRender(c.ctx, c), pushInitCallback(c)), 
        c;
        if ("/" === tag) {
            var htmlText = children;
            if ("" === htmlText) ; else if (null == createBefore) {
                var before = createInto.lastChild;
                for (createInto.insertAdjacentHTML("beforeend", htmlText), c.element = [], before = before ? before.nextSibling : createInto.firstChild; before; ) c.element.push(before), 
                before = before.nextSibling;
            } else {
                var elPrev = (el = createBefore).previousSibling, removeEl = !1, parent = createInto;
                el.insertAdjacentHTML || (el = parent.insertBefore(createEl("i"), el), removeEl = !0), 
                el.insertAdjacentHTML("beforebegin", htmlText), elPrev = elPrev ? elPrev.nextSibling : parent.firstChild;
                for (var newElements = []; elPrev !== el; ) newElements.push(elPrev), elPrev = elPrev.nextSibling;
                c.element = newElements, removeEl && parent.removeChild(el);
            }
            return component && (component.postRender && component.postRender(c.ctx, c), pushInitCallback(c)), 
            c;
        }
        inSvg || "svg" === tag ? (el = document.createElementNS("http://www.w3.org/2000/svg", tag), 
        inSvg = !(inSvgForeignObject = "foreignObject" === tag)) : el = createEl(tag), createInto.insertBefore(el, createBefore), 
        c.element = el, createChildren(c, el, null), component && component.postRender && component.postRender(c.ctx, c), 
        inNotFocusable && focusRootTop === c && (inNotFocusable = !1), inSvgForeignObject && (inSvg = !0), 
        (c.attrs || inNotFocusable) && (c.attrs = updateElement(c, el, c.attrs, {}, inNotFocusable)), 
        c.style && updateStyle(el, c.style, undefined);
        var className = c.className;
        return className && setClassName(el, className), inSvg = backupInSvg, inNotFocusable = backupInNotFocusable, 
        pushInitCallback(c), c;
    }
    function normalizeNode(n) {
        return !1 === n || !0 === n || null === n ? undefined : isString(n) ? {
            children: n
        } : isNumber(n) ? {
            children: "" + n
        } : n;
    }
    function createChildren(c, createInto, createBefore) {
        var ch = c.children;
        if (ch) {
            if (!__export_isArray(ch)) {
                if (isString(ch)) return void (createInto.textContent = ch);
                ch = [ ch ];
            }
            for (var i_bobril = 0, l = (ch = ch.slice(0)).length; i_bobril < l; ) {
                var item = ch[i_bobril];
                __export_isArray(item) ? (ch.splice.apply(ch, [ i_bobril, 1 ].concat(item)), l = ch.length) : null != (item = normalizeNode(item)) ? (ch[i_bobril] = createNode(item, c, createInto, createBefore), 
                i_bobril++) : (ch.splice(i_bobril, 1), l--);
            }
            c.children = ch;
        }
    }
    function destroyNode(c) {
        setRef(c.ref, null);
        var ch = c.children;
        if (__export_isArray(ch)) for (var i_3 = 0, l = ch.length; i_3 < l; i_3++) destroyNode(ch[i_3]);
        var component = c.component;
        if (component) {
            var ctx = c.ctx;
            currentCtx = ctx, beforeRenderCallback !== emptyBeforeRenderCallback && beforeRenderCallback(c, 3), 
            component.destroy && component.destroy(ctx, c, c.element);
            var disposables = ctx.disposables;
            if (__export_isArray(disposables)) for (var i_4 = disposables.length; 0 < i_4--; ) {
                var d = disposables[i_4];
                isFunction(d) ? d(ctx) : d.dispose();
            }
        }
    }
    function removeNodeRecursive(c) {
        var el = c.element;
        if (__export_isArray(el)) {
            var pa = el[0].parentNode;
            if (pa) for (var i_5 = 0; i_5 < el.length; i_5++) pa.removeChild(el[i_5]);
        } else if (null != el) {
            var p = el.parentNode;
            p && p.removeChild(el);
        } else {
            var ch = c.children;
            if (__export_isArray(ch)) for (var i_bobril = 0, l = ch.length; i_bobril < l; i_bobril++) removeNodeRecursive(ch[i_bobril]);
        }
    }
    function removeNode(c) {
        destroyNode(c), removeNodeRecursive(c);
    }
    var roots = newHashObj();
    function nodeContainsNode(c, n, resIndex, res) {
        var el = c.element, ch = c.children;
        if (__export_isArray(el)) {
            for (var ii = 0; ii < el.length; ii++) if (el[ii] === n) return res.push(c), __export_isArray(ch) ? ch : null;
        } else if (null == el) {
            if (__export_isArray(ch)) for (var i_bobril = 0; i_bobril < ch.length; i_bobril++) {
                var result = nodeContainsNode(ch[i_bobril], n, resIndex, res);
                if (result !== undefined) return res.splice(resIndex, 0, c), result;
            }
        } else if (el === n) return res.push(c), __export_isArray(ch) ? ch : null;
        return undefined;
    }
    function vdomPath(n) {
        var res = [];
        if (null == n) return res;
        var rootIds_bobril = Object.keys(roots), rootElements = rootIds_bobril.map(function(i_bobril) {
            return roots[i_bobril].e || document.body;
        }), nodeStack_bobril = [];
        rootFound: for (;n; ) {
            for (var j_bobril = 0; j_bobril < rootElements.length; j_bobril++) if (n === rootElements[j_bobril]) break rootFound;
            nodeStack_bobril.push(n), n = n.parentNode;
        }
        if (!n || 0 === nodeStack_bobril.length) return res;
        var currentCacheArray = null, currentNode = nodeStack_bobril.pop();
        for (j_bobril = 0; j_bobril < rootElements.length; j_bobril++) if (n === rootElements[j_bobril]) {
            var rn = roots[rootIds_bobril[j_bobril]].n;
            if (rn === undefined) continue;
            if ((findResult = nodeContainsNode(rn, currentNode, res.length, res)) !== undefined) {
                currentCacheArray = findResult;
                break;
            }
        }
        subtreeSearch: for (;nodeStack_bobril.length; ) {
            if (currentNode = nodeStack_bobril.pop(), currentCacheArray && currentCacheArray.length) for (var i_bobril = 0, l = currentCacheArray.length; i_bobril < l; i_bobril++) {
                var findResult;
                if ((findResult = nodeContainsNode(currentCacheArray[i_bobril], currentNode, res.length, res)) !== undefined) {
                    currentCacheArray = findResult;
                    continue subtreeSearch;
                }
            }
            res.push(null);
            break;
        }
        return res;
    }
    function deref(n) {
        for (var p = vdomPath(n), currentNode = null; null === currentNode; ) currentNode = p.pop();
        return currentNode;
    }
    function finishUpdateNode(n, c, component) {
        component && component.postRender && (currentCtx = c.ctx, component.postRender(currentCtx, n, c), 
        currentCtx = undefined), c.data = n.data, pushUpdateCallback(c);
    }
    function finishUpdateNodeWithoutChange(c, createInto, createBefore) {
        if (currentCtx = undefined, __export_isArray(c.children)) {
            var backupInSvg = inSvg, backupInNotFocusable = inNotFocusable;
            "svg" === c.tag ? inSvg = !0 : inSvg && "foreignObject" === c.tag && (inSvg = !1), 
            inNotFocusable && focusRootTop === c && (inNotFocusable = !1), selectedUpdate(c.children, c.element || createInto, null != c.element ? null : createBefore), 
            inSvg = backupInSvg, inNotFocusable = backupInNotFocusable;
        }
        pushUpdateEverytimeCallback(c);
    }
    function updateNode(n, c, createInto, createBefore, deepness, inSelectedUpdate) {
        var component = n.component, bigChange = !1, ctx = c.ctx;
        if (null != component && null != ctx) {
            var locallyInvalidated = !1;
            if (ctx[ctxInvalidated] === frameCounter && (deepness = Math.max(deepness, ctx[ctxDeepness]), 
            locallyInvalidated = !0), component.id !== c.component.id) bigChange = !0; else {
                if (currentCtx = ctx, n.cfg !== undefined ? ctx.cfg = n.cfg : ctx.cfg = findCfg(c.parent), 
                component.shouldChange && !component.shouldChange(ctx, n, c) && !ignoringShouldChange && !locallyInvalidated) return finishUpdateNodeWithoutChange(c, createInto, createBefore), 
                c;
                ctx.data = n.data || {}, c.component = component, beforeRenderCallback !== emptyBeforeRenderCallback && beforeRenderCallback(n, inSelectedUpdate ? 2 : 1), 
                component.render && (c.orig = n, n = __export_assign({}, n), c.cfg = undefined, 
                n.cfg !== undefined && (n.cfg = undefined), component.render(ctx, n, c), n.cfg !== undefined && (c.cfg === undefined ? c.cfg = n.cfg : __export_assign(c.cfg, n.cfg))), 
                currentCtx = undefined;
            }
        } else {
            if (c.orig === n) return c;
            c.orig = n;
        }
        var newChildren = n.children, cachedChildren = c.children, tag = n.tag;
        if ("-" === tag) return finishUpdateNodeWithoutChange(c, createInto, createBefore), 
        c;
        var backupInSvg = inSvg, backupInNotFocusable = inNotFocusable;
        if (isNumber(newChildren) && (newChildren = "" + newChildren), bigChange || null != component && null == ctx || null == component && null != ctx && ctx.me.component !== emptyComponent) ; else if ("/" === tag) {
            if ("/" === c.tag && cachedChildren === newChildren) return finishUpdateNode(n, c, component), 
            c;
        } else if (tag === c.tag) {
            if (tag === undefined) {
                if (isString(newChildren) && isString(cachedChildren)) {
                    if (newChildren !== cachedChildren) (el = c.element).textContent = newChildren, 
                    c.children = newChildren;
                } else inNotFocusable && focusRootTop === c && (inNotFocusable = !1), deepness <= 0 ? __export_isArray(cachedChildren) && selectedUpdate(c.children, createInto, createBefore) : c.children = updateChildren(createInto, newChildren, cachedChildren, c, createBefore, deepness - 1), 
                inSvg = backupInSvg, inNotFocusable = backupInNotFocusable;
                return finishUpdateNode(n, c, component), c;
            }
            var inSvgForeignObject = !1;
            "svg" === tag ? inSvg = !0 : inSvg && "foreignObject" === tag && (inSvg = !(inSvgForeignObject = !0)), 
            inNotFocusable && focusRootTop === c && (inNotFocusable = !1);
            var el = c.element;
            isString(newChildren) && !__export_isArray(cachedChildren) ? newChildren !== cachedChildren && (cachedChildren = el.textContent = newChildren) : deepness <= 0 ? __export_isArray(cachedChildren) && selectedUpdate(c.children, el, createBefore) : cachedChildren = updateChildren(el, newChildren, cachedChildren, c, null, deepness - 1), 
            c.children = cachedChildren, inSvgForeignObject && (inSvg = !0), finishUpdateNode(n, c, component), 
            (c.attrs || n.attrs || inNotFocusable) && (c.attrs = updateElement(c, el, n.attrs, c.attrs || {}, inNotFocusable)), 
            updateStyle(el, n.style, c.style), c.style = n.style;
            var className = n.className;
            return className !== c.className && (setClassName(el, className || ""), c.className = className), 
            inSvg = backupInSvg, inNotFocusable = backupInNotFocusable, c;
        }
        var parEl = c.element;
        __export_isArray(parEl) && (parEl = parEl[0]), parEl = null == parEl ? createInto : parEl.parentNode;
        var r = createNode(n, c.parent, parEl, getDomNode(c));
        return removeNode(c), r;
    }
    function getDomNode(c) {
        if (c === undefined) return null;
        var el = c.element;
        if (null != el) return __export_isArray(el) ? el[0] : el;
        var ch = c.children;
        if (!__export_isArray(ch)) return null;
        for (var i_bobril = 0; i_bobril < ch.length; i_bobril++) if (el = getDomNode(ch[i_bobril])) return el;
        return null;
    }
    function findNextNode(a, i_bobril, len, def) {
        for (;++i_bobril < len; ) {
            var ai = a[i_bobril];
            if (null != ai) {
                var n = getDomNode(ai);
                if (null != n) return n;
            }
        }
        return def;
    }
    function callPostCallbacks() {
        for (var count = updateInstance.length, i_bobril = 0; i_bobril < count; i_bobril++) {
            var n = updateInstance[i_bobril];
            currentCtx = n.ctx, updateCall[i_bobril].call(n.component, currentCtx, n, n.element);
        }
        currentCtx = undefined, updateCall = [], updateInstance = [];
    }
    function updateNodeInUpdateChildren(newNode, cachedChildren, cachedIndex, cachedLength, createBefore, element, deepness) {
        cachedChildren[cachedIndex] = updateNode(newNode, cachedChildren[cachedIndex], element, findNextNode(cachedChildren, cachedIndex, cachedLength, createBefore), deepness);
    }
    function reorderInUpdateChildrenRec(c, element, before) {
        var el = c.element;
        if (null == el) {
            var ch = c.children;
            if (__export_isArray(ch)) for (i_bobril = 0; i_bobril < ch.length; i_bobril++) reorderInUpdateChildrenRec(ch[i_bobril], element, before);
        } else if (__export_isArray(el)) for (var i_bobril = 0; i_bobril < el.length; i_bobril++) element.insertBefore(el[i_bobril], before); else element.insertBefore(el, before);
    }
    function reorderInUpdateChildren(cachedChildren, cachedIndex, cachedLength, createBefore, element) {
        var before = findNextNode(cachedChildren, cachedIndex, cachedLength, createBefore), cur = cachedChildren[cachedIndex], what = getDomNode(cur);
        null != what && what !== before && reorderInUpdateChildrenRec(cur, element, before);
    }
    function reorderAndUpdateNodeInUpdateChildren(newNode, cachedChildren, cachedIndex, cachedLength, createBefore, element, deepness) {
        var before = findNextNode(cachedChildren, cachedIndex, cachedLength, createBefore), cur = cachedChildren[cachedIndex], what = getDomNode(cur);
        null != what && what !== before && reorderInUpdateChildrenRec(cur, element, before), 
        cachedChildren[cachedIndex] = updateNode(newNode, cur, element, before, deepness);
    }
    function updateChildren(element, newChildren, cachedChildren, parentNode, createBefore, deepness) {
        null == newChildren && (newChildren = []), __export_isArray(newChildren) || (newChildren = [ newChildren ]), 
        null == cachedChildren && (cachedChildren = []), __export_isArray(cachedChildren) || (element.firstChild && element.removeChild(element.firstChild), 
        cachedChildren = []);
        var newIndex, newCh = newChildren, newLength = (newCh = newCh.slice(0)).length;
        for (newIndex = 0; newIndex < newLength; ) {
            var item = newCh[newIndex];
            __export_isArray(item) ? (newCh.splice.apply(newCh, [ newIndex, 1 ].concat(item)), 
            newLength = newCh.length) : null != (item = normalizeNode(item)) ? (newCh[newIndex] = item, 
            newIndex++) : (newCh.splice(newIndex, 1), newLength--);
        }
        return updateChildrenCore(element, newCh, cachedChildren, parentNode, createBefore, deepness);
    }
    function updateChildrenCore(element, newChildren, cachedChildren, parentNode, createBefore, deepness) {
        for (var newEnd = newChildren.length, cachedLength = cachedChildren.length, cachedEnd = cachedLength, newIndex = 0, cachedIndex = 0; newIndex < newEnd && cachedIndex < cachedEnd; ) {
            if (newChildren[newIndex].key !== cachedChildren[cachedIndex].key) {
                for (;newChildren[newEnd - 1].key === cachedChildren[cachedEnd - 1].key && (cachedEnd--, 
                updateNodeInUpdateChildren(newChildren[--newEnd], cachedChildren, cachedEnd, cachedLength, createBefore, element, deepness), 
                newIndex < newEnd && cachedIndex < cachedEnd); ) ;
                if (newIndex < newEnd && cachedIndex < cachedEnd) {
                    if (newChildren[newIndex].key === cachedChildren[cachedEnd - 1].key) {
                        cachedChildren.splice(cachedIndex, 0, cachedChildren[cachedEnd - 1]), cachedChildren.splice(cachedEnd, 1), 
                        reorderAndUpdateNodeInUpdateChildren(newChildren[newIndex], cachedChildren, cachedIndex, cachedLength, createBefore, element, deepness), 
                        newIndex++, cachedIndex++;
                        continue;
                    }
                    if (newChildren[newEnd - 1].key === cachedChildren[cachedIndex].key) {
                        cachedChildren.splice(cachedEnd, 0, cachedChildren[cachedIndex]), cachedChildren.splice(cachedIndex, 1), 
                        cachedEnd--, reorderAndUpdateNodeInUpdateChildren(newChildren[--newEnd], cachedChildren, cachedEnd, cachedLength, createBefore, element, deepness);
                        continue;
                    }
                }
                break;
            }
            updateNodeInUpdateChildren(newChildren[newIndex], cachedChildren, cachedIndex, cachedLength, createBefore, element, deepness), 
            newIndex++, cachedIndex++;
        }
        if (cachedIndex === cachedEnd) {
            if (newIndex === newEnd) return cachedChildren;
            for (;newIndex < newEnd; ) cachedChildren.splice(cachedIndex, 0, createNode(newChildren[newIndex], parentNode, element, findNextNode(cachedChildren, cachedIndex - 1, cachedLength, createBefore))), 
            cachedIndex++, cachedEnd++, cachedLength++, newIndex++;
            return cachedChildren;
        }
        if (newIndex === newEnd) {
            for (;cachedIndex < cachedEnd; ) removeNode(cachedChildren[--cachedEnd]), cachedChildren.splice(cachedEnd, 1);
            return cachedChildren;
        }
        for (var key, cachedKeys = newHashObj(), newKeys = newHashObj(), backupNewIndex = newIndex, backupCachedIndex = cachedIndex, deltaKeyless = 0; cachedIndex < cachedEnd; cachedIndex++) null != (key = cachedChildren[cachedIndex].key) ? cachedKeys[key] = cachedIndex : deltaKeyless--;
        for (var keyLess = -deltaKeyless - deltaKeyless; newIndex < newEnd; newIndex++) null != (key = newChildren[newIndex].key) ? newKeys[key] = newIndex : deltaKeyless++;
        keyLess += deltaKeyless;
        var cachedKey, delta = 0;
        for (newIndex = backupNewIndex, cachedIndex = backupCachedIndex; cachedIndex < cachedEnd && newIndex < newEnd; ) if (null !== cachedChildren[cachedIndex]) if (null != (cachedKey = cachedChildren[cachedIndex].key)) {
            if (null == (key = newChildren[newIndex].key)) {
                for (newIndex++; newIndex < newEnd && null == (key = newChildren[newIndex].key); ) newIndex++;
                if (null == key) break;
            }
            var akPos = cachedKeys[key];
            akPos !== undefined ? cachedKey in newKeys ? cachedIndex === akPos + delta ? (updateNodeInUpdateChildren(newChildren[newIndex], cachedChildren, cachedIndex, cachedLength, createBefore, element, deepness), 
            newIndex++, cachedIndex++) : (cachedChildren.splice(cachedIndex, 0, cachedChildren[akPos + delta]), 
            cachedChildren[akPos + ++delta] = null, reorderAndUpdateNodeInUpdateChildren(newChildren[newIndex], cachedChildren, cachedIndex, cachedLength, createBefore, element, deepness), 
            cachedIndex++, cachedEnd++, cachedLength++, newIndex++) : (removeNode(cachedChildren[cachedIndex]), 
            cachedChildren.splice(cachedIndex, 1), delta--, cachedEnd--, cachedLength--) : (cachedChildren.splice(cachedIndex, 0, createNode(newChildren[newIndex], parentNode, element, findNextNode(cachedChildren, cachedIndex - 1, cachedLength, createBefore))), 
            delta++, newIndex++, cachedIndex++, cachedEnd++, cachedLength++);
        } else cachedIndex++; else cachedChildren.splice(cachedIndex, 1), cachedEnd--, cachedLength--, 
        delta--;
        for (;cachedIndex < cachedEnd; ) null !== cachedChildren[cachedIndex] ? null == cachedChildren[cachedIndex].key ? cachedIndex++ : (removeNode(cachedChildren[cachedIndex]), 
        cachedChildren.splice(cachedIndex, 1), cachedEnd--, cachedLength--) : (cachedChildren.splice(cachedIndex, 1), 
        cachedEnd--, cachedLength--);
        for (;newIndex < newEnd; ) null != (key = newChildren[newIndex].key) && (cachedChildren.splice(cachedIndex, 0, createNode(newChildren[newIndex], parentNode, element, findNextNode(cachedChildren, cachedIndex - 1, cachedLength, createBefore))), 
        cachedEnd++, cachedLength++, delta++, cachedIndex++), newIndex++;
        if (!keyLess) return cachedChildren;
        for (keyLess = keyLess - Math.abs(deltaKeyless) >> 1, newIndex = backupNewIndex, 
        cachedIndex = backupCachedIndex; newIndex < newEnd; ) if (cachedIndex < cachedEnd && null != (cachedKey = cachedChildren[cachedIndex].key)) cachedIndex++; else if (key = newChildren[newIndex].key, 
        newIndex < cachedEnd && key === cachedChildren[newIndex].key) {
            if (null != key) {
                newIndex++;
                continue;
            }
            updateNodeInUpdateChildren(newChildren[newIndex], cachedChildren, newIndex, cachedLength, createBefore, element, deepness), 
            keyLess--, cachedIndex = ++newIndex;
        } else if (null == key) cachedIndex < cachedEnd ? (cachedChildren.splice(newIndex, 0, cachedChildren[cachedIndex]), 
        cachedChildren.splice(cachedIndex + 1, 1), reorderAndUpdateNodeInUpdateChildren(newChildren[newIndex], cachedChildren, newIndex, cachedLength, createBefore, element, deepness), 
        keyLess--) : (cachedChildren.splice(newIndex, 0, createNode(newChildren[newIndex], parentNode, element, findNextNode(cachedChildren, newIndex - 1, cachedLength, createBefore))), 
        cachedEnd++, cachedLength++), newIndex++, cachedIndex++; else {
            if (0 === keyLess && deltaKeyless < 0) {
                for (;removeNode(cachedChildren[cachedIndex]), cachedChildren.splice(cachedIndex, 1), 
                cachedEnd--, cachedLength--, deltaKeyless++, null == cachedChildren[cachedIndex].key; ) ;
                continue;
            }
            for (;null == cachedChildren[cachedIndex].key; ) cachedIndex++;
            cachedChildren[cachedIndex].key, cachedChildren.splice(newIndex, 0, cachedChildren[cachedIndex]), 
            cachedChildren.splice(cachedIndex + 1, 1), reorderInUpdateChildren(cachedChildren, newIndex, cachedLength, createBefore, element), 
            cachedIndex = ++newIndex;
        }
        for (;newIndex < cachedEnd; ) removeNode(cachedChildren[--cachedEnd]), cachedChildren.splice(cachedEnd, 1);
        return cachedChildren;
    }
    var hasNativeRaf = !1, nativeRaf = window.requestAnimationFrame;
    nativeRaf && nativeRaf(function(param) {
        param === +param && (hasNativeRaf = !0);
    });
    var __export_now = Date.now || function() {
        return new Date().getTime();
    }, startTime = __export_now(), lastTickTime = 0;
    function requestAnimationFrame(callback) {
        if (hasNativeRaf) nativeRaf(callback); else {
            var delay = 50 / 3 + lastTickTime - __export_now();
            delay < 0 && (delay = 0), window.setTimeout(function() {
                lastTickTime = __export_now(), callback(lastTickTime - startTime);
            }, delay);
        }
    }
    var registryEvents, ctxInvalidated = "$invalidated", ctxDeepness = "$deepness", fullRecreateRequested = !0, scheduled = !1, initializing = !0, uptimeMs = 0, frameCounter = 0, renderFrameBegin = 0, regEvents = {};
    function addEvent(name_bobril, priority, callback) {
        null == registryEvents && (registryEvents = {});
        var list = registryEvents[name_bobril] || [];
        list.push({
            priority: priority,
            callback: callback
        }), registryEvents[name_bobril] = list;
    }
    function emitEvent(name_bobril, ev, target, node) {
        var events_bobril = regEvents[name_bobril];
        if (events_bobril) for (var i_bobril = 0; i_bobril < events_bobril.length; i_bobril++) if (events_bobril[i_bobril](ev, target, node)) return !0;
        return !1;
    }
    var listeningEventDeepness = 0;
    function addListener(el, name_bobril) {
        if ("!" != name_bobril[0]) {
            var capture = "^" == name_bobril[0], eventName = name_bobril;
            "@" == name_bobril[0] && (eventName = name_bobril.slice(1), el = document), capture && (eventName = name_bobril.slice(1)), 
            "on" + eventName in window && (el = window), el.addEventListener(eventName, enhanceEvent, capture);
        }
        function enhanceEvent(ev) {
            var t = (ev = ev || window.event).target || ev.srcElement || el;
            listeningEventDeepness++, emitEvent(name_bobril, ev, t, deref(t)), 0 == --listeningEventDeepness && deferSyncUpdateRequested && syncUpdate();
        }
    }
    function initEvents() {
        if (registryEvents !== undefined) {
            for (var eventNames = Object.keys(registryEvents), j_bobril = 0; j_bobril < eventNames.length; j_bobril++) {
                var eventName = eventNames[j_bobril], arr = registryEvents[eventName];
                arr = arr.sort(function(a, b) {
                    return a.priority - b.priority;
                }), regEvents[eventName] = arr.map(function(v) {
                    return v.callback;
                });
            }
            registryEvents = undefined;
            for (var body = document.body, i_bobril = 0; i_bobril < eventNames.length; i_bobril++) addListener(body, eventNames[i_bobril]);
        }
    }
    function selectedUpdate(cache, element, createBefore) {
        for (var len = cache.length, i_bobril = 0; i_bobril < len; i_bobril++) {
            var node = cache[i_bobril], ctx = node.ctx;
            if (null != ctx && ctx[ctxInvalidated] === frameCounter) cache[i_bobril] = updateNode(node.orig, node, element, createBefore, ctx[ctxDeepness], !0); else if (__export_isArray(node.children)) {
                var backupInSvg = inSvg, backupInNotFocusable = inNotFocusable;
                inNotFocusable && focusRootTop === node && (inNotFocusable = !1), "svg" === node.tag ? inSvg = !0 : inSvg && "foreignObject" === node.tag && (inSvg = !1), 
                selectedUpdate(node.children, node.element || element, findNextNode(cache, i_bobril, len, createBefore)), 
                pushUpdateEverytimeCallback(node), inSvg = backupInSvg, inNotFocusable = backupInNotFocusable;
            }
        }
    }
    var emptyBeforeRenderCallback = function() {}, beforeRenderCallback = emptyBeforeRenderCallback, beforeFrameCallback = function() {}, reallyBeforeFrameCallback = function() {}, afterFrameCallback = function() {};
    function setBeforeFrame(callback) {
        var res = beforeFrameCallback;
        return beforeFrameCallback = callback, res;
    }
    function setAfterFrame(callback) {
        var res = afterFrameCallback;
        return afterFrameCallback = callback, res;
    }
    function isLogicalParent(parent, child, rootIds_bobril) {
        for (;null != child; ) {
            if (parent === child) return !0;
            var p = child.parent;
            if (null == p) for (var i_bobril = 0; i_bobril < rootIds_bobril.length; i_bobril++) {
                var r = roots[rootIds_bobril[i_bobril]];
                if (r && r.n === child) {
                    p = r.p;
                    break;
                }
            }
            child = p;
        }
        return !1;
    }
    var rootIds, deferSyncUpdateRequested = !1;
    function syncUpdate() {
        deferSyncUpdateRequested = !1, internalUpdate(__export_now() - startTime);
    }
    function update(time) {
        scheduled = !1, internalUpdate(time);
    }
    var RootComponent = createVirtualComponent({
        render: function(ctx, me) {
            var r = ctx.data, c = r.f(r);
            c === undefined ? me.tag = "-" : me.children = c;
        }
    });
    function internalUpdate(time) {
        renderFrameBegin = __export_now(), initEvents(), reallyBeforeFrameCallback(), frameCounter++, 
        ignoringShouldChange = nextIgnoreShouldChange, nextIgnoreShouldChange = !1, uptimeMs = time, 
        beforeFrameCallback(), focusRootTop = 0 === focusRootStack.length ? null : focusRootStack[focusRootStack.length - 1];
        var fullRefresh = inNotFocusable = !1;
        fullRecreateRequested && (fullRefresh = !(fullRecreateRequested = !1)), rootIds = Object.keys(roots);
        for (var i_bobril = 0; i_bobril < rootIds.length; i_bobril++) {
            var r = roots[rootIds[i_bobril]];
            if (r) {
                for (var rc = r.n, insertBefore = null, j_bobril = i_bobril + 1; j_bobril < rootIds.length; j_bobril++) {
                    var rafter = roots[rootIds[j_bobril]];
                    if (rafter !== undefined && null != (insertBefore = getDomNode(rafter.n))) break;
                }
                if (focusRootTop && (inNotFocusable = !isLogicalParent(focusRootTop, r.p, rootIds)), 
                r.e === undefined && (r.e = document.body), rc) if (fullRefresh || rc.ctx[ctxInvalidated] === frameCounter) updateNode(RootComponent(r), rc, r.e, insertBefore, fullRefresh ? 1e6 : rc.ctx[ctxDeepness]); else __export_isArray(r.c) && selectedUpdate(r.c, r.e, insertBefore); else rc = createNode(RootComponent(r), undefined, r.e, insertBefore), 
                r.n = rc;
                r.c = rc.children;
            }
        }
        rootIds = undefined, callPostCallbacks();
        var r0 = roots[0];
        afterFrameCallback(r0 ? r0.c : null), __export_now() - renderFrameBegin;
    }
    var nextIgnoreShouldChange = !1, ignoringShouldChange = !1;
    function ignoreShouldChange() {
        nextIgnoreShouldChange = !0, __export_invalidate();
    }
    function setInvalidate(inv) {
        var prev = __export_invalidate;
        return __export_invalidate = inv, prev;
    }
    var __export_invalidate = function(ctx, deepness) {
        null != ctx ? (deepness == undefined && (deepness = 1e6), ctx[ctxInvalidated] !== frameCounter + 1 ? (ctx[ctxInvalidated] = frameCounter + 1, 
        ctx[ctxDeepness] = deepness) : deepness > ctx[ctxDeepness] && (ctx[ctxDeepness] = deepness)) : fullRecreateRequested = !0, 
        scheduled || initializing || (scheduled = !0, requestAnimationFrame(update));
    }, lastRootId = 0;
    function addRoot(factory, element, parent) {
        var rootId_bobril = "" + ++lastRootId;
        return roots[rootId_bobril] = {
            f: factory,
            e: element,
            c: [],
            p: parent,
            n: undefined
        }, null != rootIds ? rootIds.push(rootId_bobril) : firstInvalidate(), rootId_bobril;
    }
    function removeRoot(id) {
        var root = roots[id];
        root && (root.n && removeNode(root.n), delete roots[id]);
    }
    function getRoots() {
        return roots;
    }
    function finishInitialize() {
        initializing = !1, __export_invalidate();
    }
    var beforeInit = finishInitialize;
    function firstInvalidate() {
        initializing = !0, beforeInit(), beforeInit = finishInitialize;
    }
    function init(factory, element) {
        removeRoot("0"), roots[0] = {
            f: factory,
            e: element,
            c: [],
            p: undefined,
            n: undefined
        }, firstInvalidate();
    }
    function setBeforeInit(callback) {
        var prevBeforeInit = beforeInit;
        beforeInit = function() {
            callback(prevBeforeInit);
        };
    }
    function bubble(node, name_bobril, param) {
        for (;node; ) {
            var c = node.component;
            if (c) {
                var ctx = node.ctx, m = c[name_bobril];
                if (m && m.call(c, ctx, param)) return ctx;
                if ((m = c.shouldStopBubble) && m.call(c, ctx, name_bobril, param)) break;
            }
            node = node.parent;
        }
        return undefined;
    }
    function broadcastEventToNode(node, name_bobril, param) {
        if (!node) return undefined;
        var c = node.component;
        if (c) {
            var ctx = node.ctx, m = c[name_bobril];
            if (m && m.call(c, ctx, param)) return ctx;
            if ((m = c.shouldStopBroadcast) && m.call(c, ctx, name_bobril, param)) return undefined;
        }
        var ch = node.children;
        if (__export_isArray(ch)) for (var i_bobril = 0; i_bobril < ch.length; i_bobril++) {
            var res = broadcastEventToNode(ch[i_bobril], name_bobril, param);
            if (null != res) return res;
        }
        return undefined;
    }
    function broadcast(name_bobril, param) {
        for (var k = Object.keys(roots), i_bobril = 0; i_bobril < k.length; i_bobril++) {
            var ch = roots[k[i_bobril]].n;
            if (null != ch) {
                var res = broadcastEventToNode(ch, name_bobril, param);
                if (null != res) return res;
            }
        }
        return undefined;
    }
    function preventDefault(event) {
        var pd = event.preventDefault;
        pd ? pd.call(event) : event.returnValue = !1;
    }
    function setStyleShim(name_bobril, action) {
        mapping[name_bobril] = action;
    }
    var media = null, breaks = [ [ 414, 800, 900 ], [ 736, 1280, 1440 ] ];
    function emitOnMediaChange() {
        return media = null, __export_invalidate(), !1;
    }
    for (var events = [ "resize", "orientationchange" ], i = 0; i < events.length; i++) addEvent(events[i], 10, emitOnMediaChange);
    var weirdPortrait, viewport = window.document.documentElement, isAndroid = /Android/i.test(navigator.userAgent);
    function getMedia() {
        if (null == media) {
            var w = viewport.clientWidth, h = viewport.clientHeight, o = window.orientation, p = w <= h;
            if (null == o && (o = p ? 0 : 90), isAndroid) {
                var op = Math.abs(o) % 180 == 90;
                null == weirdPortrait ? weirdPortrait = op === p : p = op === weirdPortrait;
            }
            for (var device = 0; w > breaks[+!p][device]; ) device++;
            media = {
                width: w,
                height: h,
                orientation: o,
                deviceCategory: device,
                portrait: p
            };
        }
        return media;
    }
    var testStyle, __export_asap = function() {
        var callbacks_bobril = [];
        function executeCallbacks() {
            var cbList = callbacks_bobril;
            callbacks_bobril = [];
            for (var i_bobril = 0, len = cbList.length; i_bobril < len; i_bobril++) cbList[i_bobril]();
        }
        var scriptEl, onreadystatechange = "onreadystatechange";
        if (window.MutationObserver) {
            var hiddenDiv = document.createElement("div");
            return new MutationObserver(executeCallbacks).observe(hiddenDiv, {
                attributes: !0
            }), function(callback) {
                callbacks_bobril.length || hiddenDiv.setAttribute("yes", "no"), callbacks_bobril.push(callback);
            };
        }
        if (!window.setImmediate && window.postMessage && window.addEventListener) {
            var MESSAGE_PREFIX = "basap" + Math.random(), hasPostMessage = !1, onGlobalMessage = function(event) {
                event.source === window && event.data === MESSAGE_PREFIX && (hasPostMessage = !1, 
                executeCallbacks());
            };
            return window.addEventListener("message", onGlobalMessage, !1), function(fn) {
                callbacks_bobril.push(fn), hasPostMessage || (hasPostMessage = !0, window.postMessage(MESSAGE_PREFIX, "*"));
            };
        }
        if (!window.setImmediate && onreadystatechange in document.createElement("script")) return function(callback) {
            callbacks_bobril.push(callback), scriptEl || ((scriptEl = document.createElement("script"))[onreadystatechange] = function() {
                scriptEl[onreadystatechange] = null, scriptEl.parentNode.removeChild(scriptEl), 
                scriptEl = null, executeCallbacks();
            }, document.body.appendChild(scriptEl));
        };
        var timeout, timeoutFn = window.setImmediate || setTimeout;
        return function(callback) {
            callbacks_bobril.push(callback), timeout || (timeout = timeoutFn(function() {
                timeout = undefined, executeCallbacks();
            }, 0));
        };
    }();
    window.Promise || function() {
        function bind(fn, thisArg) {
            return function() {
                fn.apply(thisArg, arguments);
            };
        }
        function handle(deferred) {
            var _this = this;
            null !== this.s ? __export_asap(function() {
                var cb = _this.s ? deferred[0] : deferred[1];
                if (null != cb) {
                    var ret;
                    try {
                        ret = cb(_this.v);
                    } catch (e) {
                        return void deferred[3](e);
                    }
                    deferred[2](ret);
                } else (_this.s ? deferred[2] : deferred[3])(_this.v);
            }) : this.d.push(deferred);
        }
        function finale() {
            for (var i_bobril = 0, len = this.d.length; i_bobril < len; i_bobril++) handle.call(this, this.d[i_bobril]);
            this.d = null;
        }
        function reject(newValue) {
            this.s = !1, this.v = newValue, finale.call(this);
        }
        function doResolve(fn, onFulfilled, onRejected) {
            var done = !1;
            try {
                fn(function(value) {
                    done || (done = !0, onFulfilled(value));
                }, function(reason) {
                    done || (done = !0, onRejected(reason));
                });
            } catch (ex) {
                if (done) return;
                done = !0, onRejected(ex);
            }
        }
        function resolve(newValue) {
            try {
                if (newValue === this) throw new TypeError("Promise self resolve");
                if (Object(newValue) === newValue) {
                    var then = newValue.then;
                    if ("function" == typeof then) return void doResolve(bind(then, newValue), bind(resolve, this), bind(reject, this));
                }
                this.s = !0, this.v = newValue, finale.call(this);
            } catch (e) {
                reject.call(this, e);
            }
        }
        function Promise_bobril(fn) {
            this.s = null, this.v = null, this.d = [], doResolve(fn, bind(resolve, this), bind(reject, this));
        }
        Promise_bobril.prototype.then = function(onFulfilled, onRejected) {
            var me = this;
            return new Promise_bobril(function(resolve, reject) {
                handle.call(me, [ onFulfilled, onRejected, resolve, reject ]);
            });
        }, Promise_bobril.prototype.catch = function(onRejected) {
            return this.then(undefined, onRejected);
        }, Promise_bobril.all = function() {
            var args = [].slice.call(1 === arguments.length && __export_isArray(arguments[0]) ? arguments[0] : arguments);
            return new Promise_bobril(function(resolve, reject) {
                if (0 !== args.length) for (var remaining = args.length, i_bobril = 0; i_bobril < args.length; i_bobril++) res(i_bobril, args[i_bobril]); else resolve(args);
                function res(i_bobril, val) {
                    try {
                        if (val && ("object" == typeof val || "function" == typeof val)) {
                            var then = val.then;
                            if ("function" == typeof then) return void then.call(val, function(val) {
                                res(i_bobril, val);
                            }, reject);
                        }
                        args[i_bobril] = val, 0 == --remaining && resolve(args);
                    } catch (ex) {
                        reject(ex);
                    }
                }
            });
        }, Promise_bobril.resolve = function(value) {
            return value && "object" == typeof value && value.constructor === Promise_bobril ? value : new Promise_bobril(function(resolve) {
                resolve(value);
            });
        }, Promise_bobril.reject = function(value) {
            return new Promise_bobril(function(_resolve, reject) {
                reject(value);
            });
        }, Promise_bobril.race = function(values) {
            return new Promise_bobril(function(resolve, reject) {
                for (var i_bobril = 0, len = values.length; i_bobril < len; i_bobril++) values[i_bobril].then(resolve, reject);
            });
        }, window.Promise = Promise_bobril;
    }(), 9 === ieVersion() ? function() {
        function addFilter(s, v) {
            null == s.zoom && (s.zoom = "1");
            var f = s.filter;
            s.filter = null == f ? v : f + " " + v;
        }
        var simpleLinearGradient = /^linear\-gradient\(to (.+?),(.+?),(.+?)\)/gi;
        setStyleShim("background", function(s, v, oldName) {
            var match = simpleLinearGradient.exec(v);
            if (null != match) {
                var tmp, dir = match[1], color1 = match[2], color2 = match[3];
                switch (dir) {
                  case "top":
                    dir = "0", tmp = color1, color1 = color2, color2 = tmp;
                    break;

                  case "bottom":
                    dir = "0";
                    break;

                  case "left":
                    dir = "1", tmp = color1, color1 = color2, color2 = tmp;
                    break;

                  case "right":
                    dir = "1";
                    break;

                  default:
                    return;
                }
                s[oldName] = "none", addFilter(s, "progid:DXImageTransform.Microsoft.gradient(startColorstr='" + color1 + "',endColorstr='" + color2 + "', gradientType='" + dir + "')");
            }
        });
    }() : ((testStyle = document.createElement("div").style).cssText = "background:-webkit-linear-gradient(top,red,red)", 
    0 < testStyle.background.length && function() {
        var startsWithGradient = /^(?:repeating\-)?(?:linear|radial)\-gradient/gi, revDirs = {
            top: "bottom",
            bottom: "top",
            left: "right",
            right: "left"
        };
        function gradientWebkitConvertor(style_bobril, value, name_bobril) {
            if (startsWithGradient.test(value)) {
                var pos = value.indexOf("(to ");
                if (0 < pos) {
                    pos += 4;
                    var posEnd = value.indexOf(",", pos), dir = value.slice(pos, posEnd);
                    dir = dir.split(" ").map(function(v) {
                        return revDirs[v] || v;
                    }).join(" "), value = value.slice(0, pos - 3) + dir + value.slice(posEnd);
                }
                value = "-webkit-" + value;
            }
            style_bobril[name_bobril] = value;
        }
        setStyleShim("background", gradientWebkitConvertor);
    }());
    var bValue = "b$value", bSelectionStart = "b$selStart", bSelectionEnd = "b$selEnd", tValue = "value";
    function isCheckboxLike(el) {
        var t = el.type;
        return "checkbox" === t || "radio" === t;
    }
    function stringArrayEqual(a1, a2) {
        var l = a1.length;
        if (l !== a2.length) return !1;
        for (var j_bobril = 0; j_bobril < l; j_bobril++) if (a1[j_bobril] !== a2[j_bobril]) return !1;
        return !0;
    }
    function stringArrayContains(a, v) {
        for (var j_bobril = 0; j_bobril < a.length; j_bobril++) if (a[j_bobril] === v) return !0;
        return !1;
    }
    function selectedArray(options) {
        for (var res = [], j_bobril = 0; j_bobril < options.length; j_bobril++) options[j_bobril].selected && res.push(options[j_bobril].value);
        return res;
    }
    var prevSetValueCallback = setSetValue(function(el, node, newValue, oldValue) {
        var tagName = el.tagName, isSelect = "SELECT" === tagName, isInput = "INPUT" === tagName || "TEXTAREA" === tagName;
        if (isInput || isSelect) {
            node.ctx === undefined && (node.ctx = {
                me: node
            }, node.component = emptyComponent), oldValue === undefined && (node.ctx[bValue] = newValue);
            var emitDiff = !1;
            if (isSelect && el.multiple) {
                var options = el.options, currentMulti = selectedArray(options);
                if (!stringArrayEqual(newValue, currentMulti)) if (oldValue === undefined || stringArrayEqual(currentMulti, oldValue) || !stringArrayEqual(newValue, node.ctx[bValue])) {
                    for (var j_bobril = 0; j_bobril < options.length; j_bobril++) options[j_bobril].selected = stringArrayContains(newValue, options[j_bobril].value);
                    stringArrayEqual(currentMulti = selectedArray(options), newValue) && (emitDiff = !0);
                } else emitDiff = !0;
            } else if (isInput || isSelect) if (isInput && isCheckboxLike(el)) {
                var currentChecked = el.checked;
                newValue !== currentChecked && (oldValue === undefined || currentChecked === oldValue || newValue !== node.ctx[bValue] ? el.checked = newValue : emitDiff = !0);
            } else {
                var isCombobox = isSelect && el.size < 2, currentValue = el[tValue];
                newValue !== currentValue && (oldValue === undefined || currentValue === oldValue || newValue !== node.ctx[bValue] ? isSelect ? ("" === newValue ? el.selectedIndex = isCombobox ? 0 : -1 : el[tValue] = newValue, 
                ("" !== newValue || isCombobox) && newValue !== (currentValue = el[tValue]) && (emitDiff = !0)) : el[tValue] = newValue : emitDiff = !0);
            }
            emitDiff ? emitOnChange(undefined, el, node) : node.ctx[bValue] = newValue;
        } else prevSetValueCallback(el, node, newValue, oldValue);
    });
    function emitOnChange(ev, target, node) {
        if (target && "OPTION" === target.nodeName && (target = document.activeElement, 
        node = deref(target)), !node) return !1;
        var c = node.component, hasProp = node.attrs && node.attrs[bValue], hasOnChange = c && null != c.onChange, hasPropOrOnChange = hasProp || hasOnChange, hasOnSelectionChange = c && null != c.onSelectionChange;
        if (!hasPropOrOnChange && !hasOnSelectionChange) return !1;
        var ctx = node.ctx, isMultiSelect = "SELECT" === target.tagName && target.multiple;
        if (hasPropOrOnChange && isMultiSelect) {
            var vs = selectedArray(target.options);
            stringArrayEqual(ctx[bValue], vs) || (ctx[bValue] = vs, hasProp && hasProp(vs), 
            hasOnChange && c.onChange(ctx, vs));
        } else if (hasPropOrOnChange && isCheckboxLike(target)) {
            if (ev && "change" === ev.type) return setTimeout(function() {
                emitOnChange(undefined, target, node);
            }, 10), !1;
            if ("radio" === target.type) for (var radios = document.getElementsByName(target.name), j_bobril = 0; j_bobril < radios.length; j_bobril++) {
                var radio = radios[j_bobril], radioNode = deref(radio);
                if (radioNode) {
                    var rbHasProp = node.attrs[bValue], radioComponent = radioNode.component, rbHasOnChange = radioComponent && null != radioComponent.onChange;
                    if (rbHasProp || rbHasOnChange) {
                        var radioCtx = radioNode.ctx, vrb = radio.checked;
                        radioCtx[bValue] !== vrb && (radioCtx[bValue] = vrb, rbHasProp && rbHasProp(vrb), 
                        rbHasOnChange && radioComponent.onChange(radioCtx, vrb));
                    }
                }
            } else {
                var vb = target.checked;
                ctx[bValue] !== vb && (ctx[bValue] = vb, hasProp && hasProp(vb), hasOnChange && c.onChange(ctx, vb));
            }
        } else {
            if (hasPropOrOnChange) {
                var v = target.value;
                ctx[bValue] !== v && (ctx[bValue] = v, hasProp && hasProp(v), hasOnChange && c.onChange(ctx, v));
            }
            if (hasOnSelectionChange) {
                var sStart = target.selectionStart, sEnd = target.selectionEnd, sDir = target.selectionDirection, swap = !1, oStart = ctx[bSelectionStart];
                if (null == sDir ? sEnd === oStart && (swap = !0) : "backward" === sDir && (swap = !0), 
                swap) {
                    var s = sStart;
                    sStart = sEnd, sEnd = s;
                }
                emitOnSelectionChange(node, sStart, sEnd);
            }
        }
        return !1;
    }
    function emitOnSelectionChange(node, start, end) {
        var c = node.component, ctx = node.ctx;
        !c || ctx[bSelectionStart] === start && ctx[bSelectionEnd] === end || (ctx[bSelectionStart] = start, 
        ctx[bSelectionEnd] = end, c.onSelectionChange && c.onSelectionChange(ctx, {
            startPosition: start,
            endPosition: end
        }));
    }
    function emitOnMouseChange(ev, _target, _node) {
        var f = focused();
        return f && emitOnChange(ev, f.element, f), !1;
    }
    for (events = [ "input", "cut", "paste", "keydown", "keypress", "keyup", "click", "change" ], 
    i = 0; i < events.length; i++) addEvent(events[i], 10, emitOnChange);
    var mouseEvents = [ "!PointerDown", "!PointerMove", "!PointerUp", "!PointerCancel" ];
    for (i = 0; i < mouseEvents.length; i++) addEvent(mouseEvents[i], 2, emitOnMouseChange);
    function buildParam(ev) {
        return {
            shift: ev.shiftKey,
            ctrl: ev.ctrlKey,
            alt: ev.altKey,
            meta: ev.metaKey || !1,
            which: ev.which || ev.keyCode
        };
    }
    function emitOnKeyDown(ev, _target, node) {
        return !!node && (!!bubble(node, "onKeyDown", buildParam(ev)) && (preventDefault(ev), 
        !0));
    }
    function emitOnKeyUp(ev, _target, node) {
        return !!node && (!!bubble(node, "onKeyUp", buildParam(ev)) && (preventDefault(ev), 
        !0));
    }
    function emitOnKeyPress(ev, _target, node) {
        return !!node && (0 !== ev.which && (!!bubble(node, "onKeyPress", {
            charCode: ev.which || ev.keyCode
        }) && (preventDefault(ev), !0)));
    }
    addEvent("keydown", 50, emitOnKeyDown), addEvent("keyup", 50, emitOnKeyUp), addEvent("keypress", 50, emitOnKeyPress);
    var MoveOverIsNotTap = 13, TapShouldBeShorterThanMs = 750, MaxBustDelay = 500, MaxBustDelayForIE = 800, BustDistance = 50, ownerCtx = null, onClickText = "onClick", onDoubleClickText = "onDoubleClick";
    function invokeMouseOwner(handlerName, param) {
        if (null == ownerCtx) return !1;
        var handler = ownerCtx.me.component[handlerName];
        if (!handler) return !1;
        !0;
        var stop = handler(ownerCtx, param);
        return !1, stop;
    }
    function hasPointerEventsNoneB(node) {
        for (;node; ) {
            var s = node.style;
            if (s) {
                var e = s.pointerEvents;
                if (e !== undefined) return "none" === e;
            }
            node = node.parent;
        }
        return !1;
    }
    function hasPointerEventsNone(target) {
        return hasPointerEventsNoneB(deref(target));
    }
    function revertVisibilityChanges(hiddenEls) {
        if (hiddenEls.length) {
            for (var i_bobril = hiddenEls.length - 1; 0 <= i_bobril; --i_bobril) hiddenEls[i_bobril].t.style.visibility = hiddenEls[i_bobril].p;
            return !0;
        }
        return !1;
    }
    function pushAndHide(hiddenEls, t) {
        hiddenEls.push({
            t: t,
            p: t.style.visibility
        }), t.style.visibility = "hidden";
    }
    function pointerThroughIE(ev, target, _node) {
        for (var hiddenEls = [], t = target; hasPointerEventsNone(t); ) pushAndHide(hiddenEls, t), 
        t = document.elementFromPoint(ev.x, ev.y);
        if (revertVisibilityChanges(hiddenEls)) {
            try {
                t.dispatchEvent(ev);
            } catch (e) {
                return !1;
            }
            return preventDefault(ev), !0;
        }
        return !1;
    }
    function addEvent5(name_bobril, callback) {
        addEvent(name_bobril, 5, callback);
    }
    var pointersEventNames = [ "PointerDown", "PointerMove", "PointerUp", "PointerCancel" ];
    if (ieVersion() && ieVersion() < 11) {
        mouseEvents = [ "click", "dblclick", "drag", "dragend", "dragenter", "dragleave", "dragover", "dragstart", "drop", "mousedown", "mousemove", "mouseout", "mouseover", "mouseup", "mousewheel", "scroll", "wheel" ];
        for (i = 0; i < mouseEvents.length; ++i) addEvent(mouseEvents[i], 1, pointerThroughIE);
    }
    function type2Bobril(t) {
        return "mouse" === t || 4 === t ? 0 : "pen" === t || 3 === t ? 2 : 1;
    }
    function pointerEventsNoneFix(x, y, target, node) {
        for (var hiddenEls = [], t = target; hasPointerEventsNoneB(node); ) pushAndHide(hiddenEls, t), 
        node = deref(t = document.elementFromPoint(x, y));
        return revertVisibilityChanges(hiddenEls), [ t, node ];
    }
    function buildHandlerPointer(name_bobril) {
        return function(ev, target, node) {
            if (hasPointerEventsNoneB(node)) {
                var fixed = pointerEventsNoneFix(ev.clientX, ev.clientY, target, node);
                target = fixed[0], node = fixed[1];
            }
            var button = ev.button + 1, type = type2Bobril(ev.pointerType), buttons = ev.buttons;
            if (0 === button && 0 === type && buttons) for (button = 1; !(1 & buttons); ) buttons >>= 1, 
            button++;
            var param = {
                id: ev.pointerId,
                type: type,
                x: ev.clientX,
                y: ev.clientY,
                button: button,
                shift: ev.shiftKey,
                ctrl: ev.ctrlKey,
                alt: ev.altKey,
                meta: ev.metaKey || !1,
                count: ev.detail
            };
            return !!emitEvent("!" + name_bobril, param, target, node) && (preventDefault(ev), 
            !0);
        };
    }
    function buildHandlerTouch(name_bobril) {
        return function(ev, target, node) {
            for (var preventDef = !1, i_bobril = 0; i_bobril < ev.changedTouches.length; i_bobril++) {
                var t = ev.changedTouches[i_bobril];
                node = deref(target = document.elementFromPoint(t.clientX, t.clientY));
                var param = {
                    id: t.identifier + 2,
                    type: 1,
                    x: t.clientX,
                    y: t.clientY,
                    button: 1,
                    shift: ev.shiftKey,
                    ctrl: ev.ctrlKey,
                    alt: ev.altKey,
                    meta: ev.metaKey || !1,
                    count: ev.detail
                };
                emitEvent("!" + name_bobril, param, target, node) && (preventDef = !0);
            }
            return !!preventDef && (preventDefault(ev), !0);
        };
    }
    function buildHandlerMouse(name_bobril) {
        return function(ev, target, node) {
            if (hasPointerEventsNoneB(node = deref(target = document.elementFromPoint(ev.clientX, ev.clientY)))) {
                var fixed = pointerEventsNoneFix(ev.clientX, ev.clientY, target, node);
                target = fixed[0], node = fixed[1];
            }
            var param = {
                id: 1,
                type: 0,
                x: ev.clientX,
                y: ev.clientY,
                button: decodeButton(ev),
                shift: ev.shiftKey,
                ctrl: ev.ctrlKey,
                alt: ev.altKey,
                meta: ev.metaKey || !1,
                count: ev.detail
            };
            return !!emitEvent("!" + name_bobril, param, target, node) && (preventDefault(ev), 
            !0);
        };
    }
    function listenMouse() {
        addEvent5("mousedown", buildHandlerMouse(pointersEventNames[0])), addEvent5("mousemove", buildHandlerMouse(pointersEventNames[1])), 
        addEvent5("mouseup", buildHandlerMouse(pointersEventNames[2]));
    }
    if (window.ontouchstart !== undefined) addEvent5("touchstart", buildHandlerTouch(pointersEventNames[0])), 
    addEvent5("touchmove", buildHandlerTouch(pointersEventNames[1])), addEvent5("touchend", buildHandlerTouch(pointersEventNames[2])), 
    addEvent5("touchcancel", buildHandlerTouch(pointersEventNames[3])), listenMouse(); else if (window.onpointerdown !== undefined) for (i = 0; i < 4; i++) {
        addEvent5((name = pointersEventNames[i]).toLowerCase(), buildHandlerPointer(name));
    } else if (window.onmspointerdown !== undefined) for (i = 0; i < 4; i++) {
        var name;
        addEvent5("@MS" + (name = pointersEventNames[i]), buildHandlerPointer(name));
    } else listenMouse();
    for (var j = 0; j < 4; j++) !function(name_bobril) {
        var onName = "on" + name_bobril;
        addEvent("!" + name_bobril, 50, function(ev, _target, node) {
            return invokeMouseOwner(onName, ev) || null != bubble(node, onName, ev);
        });
    }(pointersEventNames[j]);
    var pointersDown = newHashObj(), toBust = [], firstPointerDown = -1, firstPointerDownTime = 0, firstPointerDownX = 0, firstPointerDownY = 0, tapCanceled = !1;
    function diffLess(n1, n2, diff) {
        return Math.abs(n1 - n2) < diff;
    }
    var prevMousePath = [];
    function mouseEnterAndLeave(ev) {
        ev;
        var t = document.elementFromPoint(ev.x, ev.y), toPath = vdomPath(t), node = 0 == toPath.length ? undefined : toPath[toPath.length - 1];
        hasPointerEventsNoneB(node) && (toPath = vdomPath(t = pointerEventsNoneFix(ev.x, ev.y, t, null == node ? undefined : node)[0]));
        bubble(node, "onMouseOver", ev);
        for (var n, c, common = 0; common < prevMousePath.length && common < toPath.length && prevMousePath[common] === toPath[common]; ) common++;
        var i_bobril = prevMousePath.length;
        for (0 < i_bobril && (n = prevMousePath[i_bobril - 1]) && (c = n.component) && c.onMouseOut && c.onMouseOut(n.ctx, ev); common < i_bobril; ) (n = prevMousePath[--i_bobril]) && (c = n.component) && c.onMouseLeave && c.onMouseLeave(n.ctx, ev);
        for (;i_bobril < toPath.length; ) (n = toPath[i_bobril]) && (c = n.component) && c.onMouseEnter && c.onMouseEnter(n.ctx, ev), 
        i_bobril++;
        return prevMousePath = toPath, 0 < i_bobril && (n = prevMousePath[i_bobril - 1]) && (c = n.component) && c.onMouseIn && c.onMouseIn(n.ctx, ev), 
        !1;
    }
    function noPointersDown() {
        return 0 === Object.keys(pointersDown).length;
    }
    function bustingPointerDown(ev, _target, _node) {
        return -1 === firstPointerDown && noPointersDown() && (firstPointerDown = ev.id, 
        firstPointerDownTime = __export_now(), firstPointerDownX = ev.x, firstPointerDownY = ev.y, 
        tapCanceled = !1, mouseEnterAndLeave(ev)), pointersDown[ev.id] = ev.type, firstPointerDown !== ev.id && (tapCanceled = !0), 
        !1;
    }
    function bustingPointerMove(ev, target, node) {
        return 0 === ev.type && 0 === ev.button && null != pointersDown[ev.id] && (ev.button = 1, 
        emitEvent("!PointerUp", ev, target, node), ev.button = 0), firstPointerDown === ev.id ? (mouseEnterAndLeave(ev), 
        diffLess(firstPointerDownX, ev.x, MoveOverIsNotTap) && diffLess(firstPointerDownY, ev.y, MoveOverIsNotTap) || (tapCanceled = !0)) : noPointersDown() && mouseEnterAndLeave(ev), 
        !1;
    }
    var clickingSpreeStart = 0, clickingSpreeCount = 0;
    function shouldPreventClickingSpree(clickCount) {
        if (0 == clickingSpreeCount) return !1;
        var n = __export_now();
        return n < clickingSpreeStart + 1e3 && clickingSpreeCount <= clickCount ? (clickingSpreeStart = n, 
        clickingSpreeCount = clickCount, !0) : (clickingSpreeCount = 0, !1);
    }
    function bustingPointerUp(ev, target, node) {
        if (delete pointersDown[ev.id], firstPointerDown == ev.id && (mouseEnterAndLeave(ev), 
        firstPointerDown = -1, 1 == ev.type && !tapCanceled && __export_now() - firstPointerDownTime < TapShouldBeShorterThanMs)) {
            emitEvent("!PointerCancel", ev, target, node), shouldPreventClickingSpree(1);
            var handled = invokeMouseOwner(onClickText, ev) || null != bubble(node, onClickText, ev), delay = ieVersion() ? MaxBustDelayForIE : MaxBustDelay;
            return toBust.push([ ev.x, ev.y, __export_now() + delay, handled ? 1 : 0 ]), handled;
        }
        return !1;
    }
    function bustingPointerCancel(ev, _target, _node) {
        return delete pointersDown[ev.id], firstPointerDown == ev.id && (firstPointerDown = -1), 
        !1;
    }
    function bustingClick(ev, _target, _node) {
        for (var n = __export_now(), i_bobril = 0; i_bobril < toBust.length; i_bobril++) {
            var j_bobril = toBust[i_bobril];
            if (j_bobril[2] < n) toBust.splice(i_bobril, 1), i_bobril--; else if (diffLess(j_bobril[0], ev.clientX, BustDistance) && diffLess(j_bobril[1], ev.clientY, BustDistance)) return toBust.splice(i_bobril, 1), 
            j_bobril[3] && preventDefault(ev), !0;
        }
        return !1;
    }
    var bustingEventNames = [ "!PointerDown", "!PointerMove", "!PointerUp", "!PointerCancel", "^click" ], bustingEventHandlers = [ bustingPointerDown, bustingPointerMove, bustingPointerUp, bustingPointerCancel, bustingClick ];
    for (i = 0; i < 5; i++) addEvent(bustingEventNames[i], 3, bustingEventHandlers[i]);
    function createHandlerMouse(handlerName) {
        return function(ev, _target, node) {
            return !(firstPointerDown != ev.id && !noPointersDown()) && !(!invokeMouseOwner(handlerName, ev) && !bubble(node, handlerName, ev));
        };
    }
    var mouseHandlerNames = [ "Down", "Move", "Up", "Up" ];
    for (i = 0; i < 4; i++) addEvent(bustingEventNames[i], 80, createHandlerMouse("onMouse" + mouseHandlerNames[i]));
    function decodeButton(ev) {
        return ev.which || ev.button;
    }
    function createHandler(handlerName, allButtons) {
        return function(ev, target, node) {
            if (1 == listeningEventDeepness && ("INPUT" != target.nodeName || 0 != ev.clientX || 0 != ev.clientY) && hasPointerEventsNoneB(node = deref(target = document.elementFromPoint(ev.clientX, ev.clientY)))) {
                var fixed = pointerEventsNoneFix(ev.clientX, ev.clientY, target, node);
                target = fixed[0], node = fixed[1];
            }
            var button = decodeButton(ev) || 1;
            if (!allButtons && 1 !== button) return !1;
            var param = {
                x: ev.clientX,
                y: ev.clientY,
                button: button,
                shift: ev.shiftKey,
                ctrl: ev.ctrlKey,
                alt: ev.altKey,
                meta: ev.metaKey || !1,
                count: ev.detail || 1
            };
            return handlerName == onDoubleClickText && (param.count = 2), !!(shouldPreventClickingSpree(param.count) || invokeMouseOwner(handlerName, param) || bubble(node, handlerName, param)) && (preventDefault(ev), 
            !0);
        };
    }
    function nodeOnPoint(x, y) {
        var target = document.elementFromPoint(x, y), node = deref(target);
        hasPointerEventsNoneB(node) && (node = pointerEventsNoneFix(x, y, target, node)[1]);
        return node;
    }
    function handleSelectStart(ev, _target, node) {
        for (;node; ) {
            var s = node.style;
            if (s) {
                var us = s.userSelect;
                if ("none" === us) return preventDefault(ev), !0;
                if (us) break;
            }
            node = node.parent;
        }
        return !1;
    }
    addEvent5("selectstart", handleSelectStart), addEvent5("^click", createHandler(onClickText)), 
    addEvent5("^dblclick", createHandler(onDoubleClickText)), addEvent5("contextmenu", createHandler("onContextMenu", !0));
    var wheelSupport = ("onwheel" in document.createElement("div") ? "" : "mouse") + "wheel";
    function handleMouseWheel(ev, target, node) {
        if (hasPointerEventsNoneB(node)) {
            var fixed = pointerEventsNoneFix(ev.x, ev.y, target, node);
            target = fixed[0], node = fixed[1];
        }
        var button = ev.button + 1, buttons = ev.buttons;
        if (0 === button && buttons) for (button = 1; !(1 & buttons); ) buttons >>= 1, button++;
        var dy, dx = 0;
        "mousewheel" == wheelSupport ? (dy = -.025 * ev.wheelDelta, ev.wheelDeltaX && (dx = -.025 * ev.wheelDeltaX)) : (dx = ev.deltaX, 
        dy = ev.deltaY);
        var param = {
            dx: dx,
            dy: dy,
            x: ev.clientX,
            y: ev.clientY,
            button: button,
            shift: ev.shiftKey,
            ctrl: ev.ctrlKey,
            alt: ev.altKey,
            meta: ev.metaKey || !1,
            count: ev.detail
        };
        return !(!invokeMouseOwner("onMouseWheel", param) && !bubble(node, "onMouseWheel", param)) && (preventDefault(ev), 
        !0);
    }
    addEvent5(wheelSupport, handleMouseWheel);
    var __export_ignoreClick = function(x, y) {
        var delay = ieVersion() ? MaxBustDelayForIE : MaxBustDelay;
        toBust.push([ x, y, __export_now() + delay, 1 ]);
    }, currentActiveElement = undefined, currentFocusedNode = undefined, nodeStack = [];
    function emitOnFocusChange(inFocus) {
        var newActiveElement = document.hasFocus() || inFocus ? document.activeElement : undefined;
        if (newActiveElement !== currentActiveElement) {
            for (var newStack = vdomPath(currentActiveElement = newActiveElement), common = 0; common < nodeStack.length && common < newStack.length && nodeStack[common] === newStack[common]; ) common++;
            var n, c, i_bobril = nodeStack.length - 1;
            for (common <= i_bobril && ((n = nodeStack[i_bobril]) && (c = n.component) && c.onBlur && c.onBlur(n.ctx), 
            i_bobril--); common <= i_bobril; ) (n = nodeStack[i_bobril]) && (c = n.component) && c.onFocusOut && c.onFocusOut(n.ctx), 
            i_bobril--;
            for (i_bobril = common; i_bobril + 1 < newStack.length; ) (n = newStack[i_bobril]) && (c = n.component) && c.onFocusIn && c.onFocusIn(n.ctx), 
            i_bobril++;
            i_bobril < newStack.length && ((n = newStack[i_bobril]) && (c = n.component) && c.onFocus && c.onFocus(n.ctx), 
            i_bobril++), currentFocusedNode = 0 == (nodeStack = newStack).length ? undefined : null2undefined(nodeStack[nodeStack.length - 1]);
        }
        return !1;
    }
    function emitOnFocusChangeDelayed() {
        return setTimeout(function() {
            return emitOnFocusChange(!1);
        }, 10), !1;
    }
    function focused() {
        return currentFocusedNode;
    }
    addEvent("^focus", 50, function() {
        return emitOnFocusChange(!0);
    }), addEvent("^blur", 50, emitOnFocusChangeDelayed);
    var callbacks = [];
    function emitOnScroll(_ev, _target, node) {
        for (var info = {
            node: node
        }, i_bobril = 0; i_bobril < callbacks.length; i_bobril++) callbacks[i_bobril](info);
        return !1;
    }
    addEvent("^scroll", 10, emitOnScroll);
    var bodyCursorBackup, userSelectBackup, lastDndId = 0, dnds = [], systemDnd = null, rootId = null, shimmedStyle = {
        userSelect: ""
    };
    shimStyle(shimmedStyle);
    var shimedStyleKeys = Object.keys(shimmedStyle), userSelectPropName = shimedStyleKeys[shimedStyleKeys.length - 1], DndCtx = function(pointerId) {
        this.id = ++lastDndId, this.pointerid = pointerId, this.enabledOperations = 7, this.operation = 0, 
        this.started = !1, this.beforeDrag = !0, this.local = !0, this.system = !1, this.ended = !1, 
        this.cursor = null, this.overNode = undefined, this.targetCtx = null, this.dragView = undefined, 
        this.startX = 0, this.startY = 0, this.distanceToStart = 10, this.x = 0, this.y = 0, 
        this.deltaX = 0, this.deltaY = 0, this.totalX = 0, this.totalY = 0, this.lastX = 0, 
        this.lastY = 0, this.shift = !1, this.ctrl = !1, this.alt = !1, this.meta = !1, 
        this.data = newHashObj(), 0 <= pointerId && (pointer2Dnd[pointerId] = this), dnds.push(this);
    };
    function lazyCreateRoot() {
        if (null == rootId) {
            var dbs = document.body.style;
            bodyCursorBackup = dbs.cursor, userSelectBackup = dbs[userSelectPropName], dbs[userSelectPropName] = "none", 
            rootId = addRoot(dndRootFactory);
        }
    }
    var DndComp = {
        render: function(ctx, me) {
            var dnd = ctx.data;
            me.tag = "div", me.style = {
                position: "absolute",
                left: dnd.x,
                top: dnd.y
            }, me.children = dnd.dragView(dnd);
        }
    };
    function currentCursor() {
        var cursor = "no-drop";
        if (0 !== dnds.length) {
            var dnd = dnds[0];
            if (dnd.beforeDrag) return "";
            if (null != dnd.cursor) return dnd.cursor;
            if (dnd.system) return "";
            switch (dnd.operation) {
              case 3:
                cursor = "move";
                break;

              case 1:
                cursor = "alias";
                break;

              case 2:
                cursor = "copy";
            }
        }
        return cursor;
    }
    var DndRootComp = {
        render: function(_ctx, me) {
            for (var res = [], i_bobril = 0; i_bobril < dnds.length; i_bobril++) {
                var dnd = dnds[i_bobril];
                dnd.beforeDrag || (null == dnd.dragView || 0 == dnd.x && 0 == dnd.y || res.push({
                    key: "" + dnd.id,
                    data: dnd,
                    component: DndComp
                }));
            }
            me.tag = "div", me.style = {
                position: "fixed",
                pointerEvents: "none",
                userSelect: "none",
                left: 0,
                top: 0,
                right: 0,
                bottom: 0
            };
            var dbs = document.body.style, cur = currentCursor();
            cur && dbs.cursor !== cur && (dbs.cursor = cur), me.children = res;
        },
        onDrag: function(ctx) {
            return __export_invalidate(ctx), !1;
        }
    };
    function dndRootFactory() {
        return {
            component: DndRootComp
        };
    }
    var dndProto = DndCtx.prototype;
    dndProto.setOperation = function(operation) {
        this.operation = operation;
    }, dndProto.setDragNodeView = function(view) {
        this.dragView = view;
    }, dndProto.addData = function(type, data) {
        return this.data[type] = data, !0;
    }, dndProto.listData = function() {
        return Object.keys(this.data);
    }, dndProto.hasData = function(type) {
        return this.data[type] !== undefined;
    }, dndProto.getData = function(type) {
        return this.data[type];
    }, dndProto.setEnabledOps = function(ops) {
        this.enabledOperations = ops;
    }, dndProto.cancelDnd = function() {
        dndMoved(undefined, this), this.destroy();
    }, dndProto.destroy = function() {
        this.ended = !0, this.started && broadcast("onDragEnd", this), delete pointer2Dnd[this.pointerid];
        for (var i_bobril = 0; i_bobril < dnds.length; i_bobril++) if (dnds[i_bobril] === this) {
            dnds.splice(i_bobril, 1);
            break;
        }
        if (systemDnd === this && (systemDnd = null), 0 === dnds.length && null != rootId) {
            removeRoot(rootId), rootId = null;
            var dbs = document.body.style;
            dbs.cursor = bodyCursorBackup, dbs[userSelectPropName] = userSelectBackup;
        }
    };
    var pointer2Dnd = newHashObj();
    function handlePointerDown(ev, _target, node) {
        var dnd = pointer2Dnd[ev.id];
        if (dnd && dnd.cancelDnd(), ev.button <= 1) {
            (dnd = new DndCtx(ev.id)).startX = ev.x, dnd.startY = ev.y, dnd.lastX = ev.x, dnd.lastY = ev.y, 
            dnd.overNode = node, updateDndFromPointerEvent(dnd, ev);
            var sourceCtx = bubble(node, "onDragStart", dnd);
            if (sourceCtx) {
                var htmlNode = getDomNode(sourceCtx.me);
                if (null == htmlNode) return dnd.destroy(), !1;
                dnd.started = !0;
                var boundFn = htmlNode.getBoundingClientRect;
                if (boundFn) {
                    var rect = boundFn.call(htmlNode);
                    dnd.deltaX = rect.left - ev.x, dnd.deltaY = rect.top - ev.y;
                }
                dnd.distanceToStart <= 0 && (dnd.beforeDrag = !1, dndMoved(node, dnd)), lazyCreateRoot();
            } else dnd.destroy();
        }
        return !1;
    }
    function dndMoved(node, dnd) {
        dnd.overNode = node, dnd.targetCtx = bubble(node, "onDragOver", dnd), null == dnd.targetCtx && (dnd.operation = 0), 
        broadcast("onDrag", dnd);
    }
    function updateDndFromPointerEvent(dnd, ev) {
        dnd.shift = ev.shift, dnd.ctrl = ev.ctrl, dnd.alt = ev.alt, dnd.meta = ev.meta, 
        dnd.x = ev.x, dnd.y = ev.y;
    }
    function handlePointerMove(ev, _target, node) {
        var dnd = pointer2Dnd[ev.id];
        if (!dnd) return !1;
        if (dnd.totalX += Math.abs(ev.x - dnd.lastX), dnd.totalY += Math.abs(ev.y - dnd.lastY), 
        dnd.beforeDrag) {
            if (dnd.totalX + dnd.totalY <= dnd.distanceToStart) return dnd.lastX = ev.x, dnd.lastY = ev.y, 
            !1;
            dnd.beforeDrag = !1;
        }
        return updateDndFromPointerEvent(dnd, ev), dndMoved(node, dnd), dnd.lastX = ev.x, 
        dnd.lastY = ev.y, !0;
    }
    function handlePointerUp(ev, _target, node) {
        var dnd = pointer2Dnd[ev.id];
        if (!dnd) return !1;
        if (!dnd.beforeDrag) {
            updateDndFromPointerEvent(dnd, ev), dndMoved(node, dnd);
            var t = dnd.targetCtx;
            return t && bubble(t.me, "onDrop", dnd) ? dnd.destroy() : dnd.cancelDnd(), __export_ignoreClick(ev.x, ev.y), 
            !0;
        }
        return dnd.destroy(), !1;
    }
    function handlePointerCancel(ev, _target, _node) {
        var dnd = pointer2Dnd[ev.id];
        return dnd && (dnd.system || (dnd.beforeDrag ? dnd.destroy() : dnd.cancelDnd())), 
        !1;
    }
    function updateFromNative(dnd, ev) {
        dnd.shift = ev.shiftKey, dnd.ctrl = ev.ctrlKey, dnd.alt = ev.altKey, dnd.meta = ev.metaKey, 
        dnd.x = ev.clientX, dnd.y = ev.clientY, dnd.totalX += Math.abs(dnd.x - dnd.lastX), 
        dnd.totalY += Math.abs(dnd.y - dnd.lastY), dndMoved(nodeOnPoint(dnd.x, dnd.y), dnd), 
        dnd.lastX = dnd.x, dnd.lastY = dnd.y;
    }
    var effectAllowedTable = [ "none", "link", "copy", "copyLink", "move", "linkMove", "copyMove", "all" ];
    function handleDragStart(ev, _target, node) {
        var dnd = systemDnd;
        null != dnd && dnd.destroy();
        var activePointerIds = Object.keys(pointer2Dnd);
        if (0 < activePointerIds.length) (dnd = pointer2Dnd[activePointerIds[0]]).system = !0, 
        systemDnd = dnd; else {
            var startX = ev.clientX, startY = ev.clientY;
            (dnd = new DndCtx(-1)).system = !0, (systemDnd = dnd).x = startX, dnd.y = startY, 
            dnd.lastX = startX, dnd.lastY = startY, dnd.startX = startX, dnd.startY = startY;
            var sourceCtx = bubble(node, "onDragStart", dnd);
            if (!sourceCtx) return dnd.destroy(), !1;
            var htmlNode = getDomNode(sourceCtx.me);
            if (null == htmlNode) return dnd.destroy(), !1;
            dnd.started = !0;
            var boundFn = htmlNode.getBoundingClientRect;
            if (boundFn) {
                var rect = boundFn.call(htmlNode);
                dnd.deltaX = rect.left - startX, dnd.deltaY = rect.top - startY;
            }
            lazyCreateRoot();
        }
        dnd.beforeDrag = !1;
        var eff = effectAllowedTable[dnd.enabledOperations], dt = ev.dataTransfer;
        if (dt.effectAllowed = eff, dt.setDragImage) {
            var div = document.createElement("div");
            div.style.pointerEvents = "none", dt.setDragImage(div, 0, 0);
        } else {
            var style_bobril = ev.target.style, opacityBackup = style_bobril.opacity, widthBackup = style_bobril.width, heightBackup = style_bobril.height, paddingBackup = style_bobril.padding;
            style_bobril.opacity = "0", style_bobril.width = "0", style_bobril.height = "0", 
            style_bobril.padding = "0", window.setTimeout(function() {
                style_bobril.opacity = opacityBackup, style_bobril.width = widthBackup, style_bobril.height = heightBackup, 
                style_bobril.padding = paddingBackup;
            }, 0);
        }
        for (var data = dnd.data, dataKeys = Object.keys(data), i_bobril = 0; i_bobril < dataKeys.length; i_bobril++) try {
            var k = dataKeys[i_bobril], d = data[k];
            isString(d) || (d = JSON.stringify(d)), ev.dataTransfer.setData(k, d);
        } catch (e) {
            0;
        }
        return updateFromNative(dnd, ev), !1;
    }
    function setDropEffect(ev, op) {
        ev.dataTransfer.dropEffect = [ "none", "link", "copy", "move" ][op];
    }
    function handleDragOver(ev, _target, _node) {
        var dnd = systemDnd;
        if (null == dnd) {
            (dnd = new DndCtx(-1)).system = !0, (systemDnd = dnd).x = ev.clientX, dnd.y = ev.clientY, 
            dnd.startX = dnd.x, dnd.startY = dnd.y, dnd.local = !1;
            var dt = ev.dataTransfer, eff = 0, effectAllowed = undefined;
            try {
                effectAllowed = dt.effectAllowed;
            } catch (e) {}
            for (;eff < 7 && effectAllowedTable[eff] !== effectAllowed; eff++) ;
            dnd.enabledOperations = eff;
            var dtTypes = dt.types;
            if (dtTypes) for (var i_bobril = 0; i_bobril < dtTypes.length; i_bobril++) {
                var tt = dtTypes[i_bobril];
                "text/plain" === tt ? tt = "Text" : "text/uri-list" === tt && (tt = "Url"), dnd.data[tt] = null;
            } else dt.getData("Text") !== undefined && (dnd.data.Text = null);
        }
        return updateFromNative(dnd, ev), setDropEffect(ev, dnd.operation), 0 != dnd.operation && (preventDefault(ev), 
        !0);
    }
    function handleDrag(ev, _target, _node) {
        var x = ev.clientX, y = ev.clientY, m = getMedia();
        return null != systemDnd && (0 === x && 0 === y || x < 0 || y < 0 || x >= m.width || y >= m.height) && (systemDnd.x = 0, 
        systemDnd.y = 0, systemDnd.operation = 0, broadcast("onDrag", systemDnd)), !1;
    }
    function handleDragEnd(_ev, _target, _node) {
        return null != systemDnd && systemDnd.destroy(), !1;
    }
    function handleDrop(ev, _target, _node) {
        var dnd = systemDnd;
        if (null == dnd) return !1;
        if (dnd.x = ev.clientX, dnd.y = ev.clientY, !dnd.local) for (var dataKeys = Object.keys(dnd.data), dt = ev.dataTransfer, i_7 = 0; i_7 < dataKeys.length; i_7++) {
            var d, k = dataKeys[i_7];
            d = "Files" === k ? [].slice.call(dt.files, 0) : dt.getData(k), dnd.data[k] = d;
        }
        updateFromNative(dnd, ev);
        var t = dnd.targetCtx;
        return t && bubble(t.me, "onDrop", dnd) ? (setDropEffect(ev, dnd.operation), dnd.destroy(), 
        preventDefault(ev)) : dnd.cancelDnd(), !0;
    }
    function justPreventDefault(ev, _target, _node) {
        return preventDefault(ev), !0;
    }
    function handleDndSelectStart(ev, _target, _node) {
        return 0 !== dnds.length && (preventDefault(ev), !0);
    }
    addEvent("!PointerDown", 4, handlePointerDown), addEvent("!PointerMove", 4, handlePointerMove), 
    addEvent("!PointerUp", 4, handlePointerUp), addEvent("!PointerCancel", 4, handlePointerCancel), 
    addEvent("selectstart", 4, handleDndSelectStart), addEvent("dragstart", 5, handleDragStart), 
    addEvent("dragover", 5, handleDragOver), addEvent("dragend", 5, handleDragEnd), 
    addEvent("drag", 5, handleDrag), addEvent("drop", 5, handleDrop), addEvent("dragenter", 5, justPreventDefault), 
    addEvent("dragleave", 5, justPreventDefault);
    var __export_getDnds = function() {
        return dnds;
    }, waitingForPopHashChange = -1;
    function emitOnHashChange() {
        return 0 <= waitingForPopHashChange && clearTimeout(waitingForPopHashChange), waitingForPopHashChange = -1, 
        __export_invalidate(), !1;
    }
    addEvent("hashchange", 10, emitOnHashChange);
    newHashObj();
    var allStyles = newHashObj(), dynamicSprites = (newHashObj(), newHashObj(), []), imageCache = newHashObj(), injectedCss = "", rebuildStyles = !1, htmlStyle = null, isIE9 = 9 === ieVersion(), chainedBeforeFrame = setBeforeFrame(beforeFrame), cssSubRuleDelimiter = /\:|\ |\>/;
    function buildCssSubRule(parent) {
        var matchSplit = cssSubRuleDelimiter.exec(parent);
        if (!matchSplit) return allStyles[parent].name;
        var posSplit = matchSplit.index;
        return allStyles[parent.substring(0, posSplit)].name + parent.substring(posSplit);
    }
    function buildCssRule(parent, name_bobril) {
        var result = "";
        if (parent) if (__export_isArray(parent)) for (var i_9 = 0; i_9 < parent.length; i_9++) 0 < i_9 && (result += ","), 
        result += "." + buildCssSubRule(parent[i_9]) + "." + name_bobril; else result = "." + buildCssSubRule(parent) + "." + name_bobril; else result = "." + name_bobril;
        return result;
    }
    function flattenStyle(cur, curPseudo, style_bobril, stylePseudo) {
        if (isString(style_bobril)) {
            var externalStyle = allStyles[style_bobril];
            if (externalStyle === undefined) throw Error("Unknown style " + style_bobril);
            flattenStyle(cur, curPseudo, externalStyle.style, externalStyle.pseudo);
        } else if (isFunction(style_bobril)) style_bobril(cur, curPseudo); else if (__export_isArray(style_bobril)) for (var i_10 = 0; i_10 < style_bobril.length; i_10++) flattenStyle(cur, curPseudo, style_bobril[i_10], undefined); else if ("object" == typeof style_bobril) for (var key in style_bobril) if (Object.prototype.hasOwnProperty.call(style_bobril, key)) {
            var val = style_bobril[key];
            isFunction(val) && (val = val(cur, key)), cur[key] = val;
        }
        if (null != stylePseudo && null != curPseudo) for (var pseudoKey in stylePseudo) {
            var curPseudoVal = curPseudo[pseudoKey];
            curPseudoVal === undefined && (curPseudoVal = newHashObj(), curPseudo[pseudoKey] = curPseudoVal), 
            flattenStyle(curPseudoVal, undefined, stylePseudo[pseudoKey], undefined);
        }
    }
    var firstStyles = !1;
    function beforeFrame() {
        var _a, dbs = document.body.style;
        if (firstStyles && 150 <= uptimeMs && (dbs.opacity = "1", firstStyles = !1), rebuildStyles) {
            1 === frameCounter && "webkitAnimation" in dbs && (firstStyles = !0, dbs.opacity = "0", 
            setTimeout(__export_invalidate, 200));
            for (var i_11 = 0; i_11 < dynamicSprites.length; i_11++) {
                var dynSprite = dynamicSprites[i_11], image = imageCache[dynSprite.url];
                if (null != image) {
                    var colorStr = dynSprite.color();
                    if (colorStr !== dynSprite.lastColor) {
                        dynSprite.lastColor = colorStr, null == dynSprite.width && (dynSprite.width = image.width), 
                        null == dynSprite.height && (dynSprite.height = image.height);
                        var lastUrl = recolorAndClip(image, colorStr, dynSprite.width, dynSprite.height, dynSprite.left, dynSprite.top);
                        allStyles[dynSprite.styleId].style = {
                            backgroundImage: "url(" + lastUrl + ")",
                            width: dynSprite.width,
                            height: dynSprite.height,
                            backgroundPosition: 0
                        };
                    }
                }
            }
            var styleStr = injectedCss;
            for (var key in allStyles) {
                var ss = allStyles[key], parent_1 = ss.parent, name_1 = ss.name, ssPseudo = ss.pseudo, ssStyle = ss.style;
                if (isFunction(ssStyle) && 0 === ssStyle.length && (ssStyle = (_a = ssStyle())[0], 
                ssPseudo = _a[1]), isString(ssStyle) && null == ssPseudo) ss.realName = ssStyle; else {
                    ss.realName = name_1;
                    var style_1 = newHashObj(), flattenPseudo = newHashObj();
                    flattenStyle(undefined, flattenPseudo, undefined, ssPseudo), flattenStyle(style_1, flattenPseudo, ssStyle, undefined);
                    var extractedInlStyle = null;
                    style_1.pointerEvents && ((extractedInlStyle = newHashObj()).pointerEvents = style_1.pointerEvents), 
                    isIE9 && style_1.userSelect && (null == extractedInlStyle && (extractedInlStyle = newHashObj()), 
                    extractedInlStyle.userSelect = style_1.userSelect, delete style_1.userSelect), ss.inlStyle = extractedInlStyle, 
                    shimStyle(style_1);
                    var cssStyle = inlineStyleToCssDeclaration(style_1);
                    for (var key2 in 0 < cssStyle.length && (styleStr += (null == name_1 ? parent_1 : buildCssRule(parent_1, name_1)) + " {" + cssStyle + "}\n"), 
                    flattenPseudo) {
                        var item = flattenPseudo[key2];
                        shimStyle(item), styleStr += (null == name_1 ? parent_1 + ":" + key2 : buildCssRule(parent_1, name_1 + ":" + key2)) + " {" + inlineStyleToCssDeclaration(item) + "}\n";
                    }
                }
            }
            var styleElement = document.createElement("style");
            styleElement.type = "text/css", styleElement.styleSheet ? styleElement.styleSheet.cssText = styleStr : styleElement.appendChild(document.createTextNode(styleStr));
            var head = document.head || document.getElementsByTagName("head")[0];
            null != htmlStyle ? head.replaceChild(styleElement, htmlStyle) : head.appendChild(styleElement), 
            htmlStyle = styleElement, rebuildStyles = !1;
        }
        chainedBeforeFrame();
    }
    var uppercasePattern = /([A-Z])/g, msPattern = /^ms-/;
    function hyphenateStyle(s) {
        return "cssFloat" === s ? "float" : s.replace(uppercasePattern, "-$1").toLowerCase().replace(msPattern, "-ms-");
    }
    function inlineStyleToCssDeclaration(style_bobril) {
        var res = "";
        for (var key in style_bobril) {
            var v = style_bobril[key];
            v !== undefined && (res += hyphenateStyle(key) + ":" + ("" === v ? '""' : v) + ";");
        }
        return res = res.slice(0, -1);
    }
    function invalidateStyles() {
        rebuildStyles = !0, __export_invalidate();
    }
    var rgbaRegex = /\s*rgba\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d+|\d*\.\d+)\s*\)\s*/;
    function recolorAndClip(image, colorStr, width, height, left, top) {
        var canvas = document.createElement("canvas");
        canvas.width = width, canvas.height = height;
        var ctx = canvas.getContext("2d");
        ctx.drawImage(image, -left, -top);
        var cRed, cGreen, cBlue, cAlpha, imgData = ctx.getImageData(0, 0, width, height), imgDataData = imgData.data, rgba = rgbaRegex.exec(colorStr);
        if (rgba ? (cRed = parseInt(rgba[1], 10), cGreen = parseInt(rgba[2], 10), cBlue = parseInt(rgba[3], 10), 
        cAlpha = Math.round(255 * parseFloat(rgba[4]))) : (cRed = parseInt(colorStr.substr(1, 2), 16), 
        cGreen = parseInt(colorStr.substr(3, 2), 16), cBlue = parseInt(colorStr.substr(5, 2), 16), 
        cAlpha = parseInt(colorStr.substr(7, 2), 16) || 255), 255 === cAlpha) for (var i_bobril = 0; i_bobril < imgDataData.length; i_bobril += 4) {
            (red = imgDataData[i_bobril]) === imgDataData[i_bobril + 1] && red === imgDataData[i_bobril + 2] && (128 === red || imgDataData[i_bobril + 3] < 255 && 112 < red) && (imgDataData[i_bobril] = cRed, 
            imgDataData[i_bobril + 1] = cGreen, imgDataData[i_bobril + 2] = cBlue);
        } else for (i_bobril = 0; i_bobril < imgDataData.length; i_bobril += 4) {
            var red = imgDataData[i_bobril], alpha = imgDataData[i_bobril + 3];
            red === imgDataData[i_bobril + 1] && red === imgDataData[i_bobril + 2] && (128 === red || alpha < 255 && 112 < red) && (255 === alpha ? (imgDataData[i_bobril] = cRed, 
            imgDataData[i_bobril + 1] = cGreen, imgDataData[i_bobril + 2] = cBlue, imgDataData[i_bobril + 3] = cAlpha) : (alpha *= 1 / 255, 
            imgDataData[i_bobril] = Math.round(cRed * alpha), imgDataData[i_bobril + 1] = Math.round(cGreen * alpha), 
            imgDataData[i_bobril + 2] = Math.round(cBlue * alpha), imgDataData[i_bobril + 3] = Math.round(cAlpha * alpha)));
        }
        return ctx.putImageData(imgData, 0, 0), canvas.toDataURL();
    }
    window.bobrilBPath;
    function createVirtualComponent(component) {
        return function(data, children) {
            return children !== undefined && (null == data && (data = {}), data.children = children), 
            {
                data: data,
                component: component
            };
        };
    }
    window.b || (window.b = {
        deref: deref,
        getRoots: getRoots,
        setInvalidate: setInvalidate,
        invalidateStyles: invalidateStyles,
        ignoreShouldChange: ignoreShouldChange,
        setAfterFrame: setAfterFrame,
        setBeforeFrame: setBeforeFrame,
        getDnds: __export_getDnds,
        setBeforeInit: setBeforeInit
    });
    init(function() {
        return "hello";
    });
}();