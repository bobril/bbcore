import { CSSInlineStyles } from "./cssTypes";

import { afterFrameCallback, beforeFrameCallback, beforeRenderCallback, reallyBeforeFrameCallback, RenderPhase } from "./frameCallbacks";

import { isString, isNumber, isObject, isFunction, isArray } from "./isFunc";

import { assert, createTextNode, hOP, is, newHashObj, noop } from "./localHelpers";

const hasPostInitDom = 1;

const hasPostUpdateDom = 2;

const hasPostUpdateDomEverytime = 4;

const hasEvents = 8;

const hasCaptureEvents = 16;

const hasUseEffect = 32;

export class BobrilCtx {
    constructor(data, me) {
        this.data = data;
        this.me = me;
        this.cfg = undefined;
        this.refs = undefined;
        this.disposables = undefined;
        this.$hookFlags = 0;
        this.$hooks = undefined;
        this.$bobxCtx = undefined;
    }
}

const emptyObject = {};

if (DEBUG) Object.freeze(emptyObject);

function createEl(name) {
    return document.createElement(name);
}

export function assertNever(switchValue) {
    throw new Error("Switch is not exhaustive for value: " + JSON.stringify(switchValue));
}

export let assign = Object.assign;

export function flatten(a) {
    if (!isArray(a)) {
        if (a == undefined || a === false || a === true) return [];
        return [ a ];
    }
    a = a.slice(0);
    let aLen = a.length;
    for (let i = 0; i < aLen; ) {
        let item = a[i];
        if (isArray(item)) {
            a.splice.apply(a, [ i, 1 ].concat(item));
            aLen = a.length;
            continue;
        }
        if (item == undefined || item === false || item === true) {
            a.splice(i, 1);
            aLen--;
            continue;
        }
        i++;
    }
    return a;
}

export function swallowPromise(promise) {
    promise.catch(reason => {
        console.error("Uncaught exception from swallowPromise", reason);
    });
}

var inSvg = false;

var inNotFocusable = false;

var updateCall = [];

var updateInstance = [];

var effectInstance = [];

export function ieVersion() {
    return document.documentMode;
}

const focusableTag = /^input$|^select$|^textarea$|^button$/;

const tabindexStr = "tabindex";

function isNaturallyFocusable(tag, attrs) {
    if (tag == undefined) return false;
    if (tag === "input" && attrs && attrs["disabled"]) return false;
    if (focusableTag.test(tag)) return true;
    if (tag === "a" && attrs != null && attrs.href != null) return true;
    return false;
}

function updateElement(n, el, newAttrs, oldAttrs, notFocusable) {
    var attrName, newAttr, oldAttr, valueOldAttr, valueNewAttr;
    let wasTabindex = false;
    if (newAttrs != null) for (attrName in newAttrs) {
        newAttr = newAttrs[attrName];
        oldAttr = oldAttrs[attrName];
        if (notFocusable && attrName === tabindexStr) {
            newAttr = -1;
            wasTabindex = true;
        } else if (attrName === tValue && !inSvg) {
            if (isFunction(newAttr)) {
                oldAttrs[bValue] = newAttr;
                newAttr = newAttr();
            }
            valueOldAttr = oldAttr;
            valueNewAttr = newAttr;
            oldAttrs[attrName] = newAttr;
            continue;
        }
        if (oldAttr !== newAttr) {
            oldAttrs[attrName] = newAttr;
            if (inSvg) {
                if (attrName === "href") el.setAttributeNS("http://www.w3.org/1999/xlink", "href", newAttr); else el.setAttribute(attrName, newAttr);
            } else if (attrName in el && !(attrName === "list" || attrName === "form")) {
                el[attrName] = newAttr;
            } else el.setAttribute(attrName, newAttr);
        }
    }
    if (notFocusable && !wasTabindex && isNaturallyFocusable(n.tag, newAttrs)) {
        el.setAttribute(tabindexStr, "-1");
        oldAttrs[tabindexStr] = -1;
    }
    if (newAttrs == undefined) {
        for (attrName in oldAttrs) {
            if (oldAttrs[attrName] !== undefined) {
                if (notFocusable && attrName === tabindexStr) continue;
                if (attrName === bValue) continue;
                oldAttrs[attrName] = undefined;
                el.removeAttribute(attrName);
            }
        }
    } else {
        for (attrName in oldAttrs) {
            if (oldAttrs[attrName] !== undefined && !(attrName in newAttrs)) {
                if (notFocusable && attrName === tabindexStr) continue;
                if (attrName === bValue) continue;
                oldAttrs[attrName] = undefined;
                el.removeAttribute(attrName);
            }
        }
    }
    if (valueNewAttr !== undefined) {
        setValueAttribute(el, n, valueNewAttr, valueOldAttr);
    }
    return oldAttrs;
}

function setValueAttribute(el, node, newValue, oldValue) {
    var tagName = el.tagName;
    var isSelect = tagName === "SELECT";
    var isInput = tagName === "INPUT" || tagName === "TEXTAREA";
    if (!isInput && !isSelect) {
        if (newValue !== oldValue) el[tValue] = newValue;
        return;
    }
    if (node.ctx === undefined) {
        node.ctx = new BobrilCtx(undefined, node);
        node.component = emptyObject;
    }
    if (oldValue === undefined) {
        node.ctx[bValue] = newValue;
    }
    var isMultiSelect = isSelect && el.multiple;
    var emitDiff = false;
    if (isMultiSelect) {
        var options = el.options;
        var currentMulti = selectedArray(options);
        if (!stringArrayEqual(newValue, currentMulti)) {
            if (oldValue === undefined || stringArrayEqual(currentMulti, oldValue) || !stringArrayEqual(newValue, node.ctx[bValue])) {
                for (var j = 0; j < options.length; j++) {
                    options[j].selected = stringArrayContains(newValue, options[j].value);
                }
                currentMulti = selectedArray(options);
                if (stringArrayEqual(currentMulti, newValue)) {
                    emitDiff = true;
                }
            } else {
                emitDiff = true;
            }
        }
    } else if (isInput || isSelect) {
        if (isInput && isCheckboxLike(el)) {
            var currentChecked = el.checked;
            if (newValue !== currentChecked) {
                if (oldValue === undefined || currentChecked === oldValue || newValue !== node.ctx[bValue]) {
                    el.checked = newValue;
                } else {
                    emitDiff = true;
                }
            }
        } else {
            var isCombobox = isSelect && el.size < 2;
            var currentValue = el[tValue];
            if (newValue !== currentValue) {
                if (oldValue === undefined || currentValue === oldValue || newValue !== node.ctx[bValue]) {
                    if (isSelect) {
                        if (newValue === "") {
                            el.selectedIndex = isCombobox ? 0 : -1;
                        } else {
                            el[tValue] = newValue;
                        }
                        if (newValue !== "" || isCombobox) {
                            currentValue = el[tValue];
                            if (newValue !== currentValue) {
                                emitDiff = true;
                            }
                        }
                    } else {
                        el[tValue] = newValue;
                    }
                } else {
                    emitDiff = true;
                }
            }
        }
    }
    if (emitDiff) {
        emitOnChange(undefined, el, node);
    } else {
        node.ctx[bValue] = newValue;
    }
}

function pushInitCallback(c) {
    var cc = c.component;
    if (cc) {
        let fn = cc[postInitDom];
        if (fn) {
            updateCall.push(fn);
            updateInstance.push(c);
        }
        let flags = getHookFlags(c);
        if (flags & hasPostInitDom) {
            updateCall.push(hookPostInitDom);
            updateInstance.push(c);
        }
        if (flags & hasUseEffect) {
            effectInstance.push(c);
        }
    } else {
        var sctx = c.ctxStyle;
        if (sctx) {
            const flags = sctx.$hookFlags | 0;
            if (flags & hasPostInitDom) {
                updateCall.push(hookPostInitDom);
                updateInstance.push(c);
            }
            if (flags & hasUseEffect) {
                effectInstance.push(c);
            }
        }
    }
}

function getHookFlags(c) {
    let flags = c.ctx.$hookFlags | 0;
    if (c.ctxStyle != undefined) flags = c.ctxStyle.$hookFlags | flags;
    return flags;
}

function pushUpdateCallback(c) {
    var cc = c.component;
    if (cc) {
        let fn = cc[postUpdateDom];
        if (fn) {
            updateCall.push(fn);
            updateInstance.push(c);
        }
        let flags = getHookFlags(c);
        if (flags & hasPostUpdateDom) {
            updateCall.push(hookPostUpdateDom);
            updateInstance.push(c);
        }
        fn = cc[postUpdateDomEverytime];
        if (fn) {
            updateCall.push(fn);
            updateInstance.push(c);
        }
        if (flags & hasPostUpdateDomEverytime) {
            updateCall.push(hookPostUpdateDomEverytime);
            updateInstance.push(c);
        }
        if (flags & hasUseEffect) {
            effectInstance.push(c);
        }
    } else {
        var sctx = c.ctxStyle;
        if (sctx) {
            const flags = sctx.$hookFlags | 0;
            if (flags & hasPostUpdateDom) {
                updateCall.push(hookPostUpdateDom);
                updateInstance.push(c);
            }
            if (flags & hasPostUpdateDomEverytime) {
                updateCall.push(hookPostUpdateDomEverytime);
                updateInstance.push(c);
            }
            if (flags & hasUseEffect) {
                effectInstance.push(c);
            }
        }
    }
}

function pushUpdateEverytimeCallback(c) {
    var cc = c.component;
    if (cc) {
        let fn = cc[postUpdateDomEverytime];
        if (fn) {
            updateCall.push(fn);
            updateInstance.push(c);
        }
        if (getHookFlags(c) & hasPostUpdateDomEverytime) {
            updateCall.push(hookPostUpdateDomEverytime);
            updateInstance.push(c);
        }
    } else {
        var sctx = c.ctxStyle;
        if (sctx) {
            const flags = sctx.$hookFlags | 0;
            if (flags & hasPostUpdateDomEverytime) {
                updateCall.push(hookPostUpdateDomEverytime);
                updateInstance.push(c);
            }
        }
    }
}

function findCfg(parent) {
    var cfg;
    while (parent) {
        cfg = parent.cfg;
        if (cfg !== undefined) break;
        if (parent.ctx !== undefined && parent.component !== emptyObject) {
            cfg = parent.ctx.cfg;
            break;
        }
        parent = parent.parent;
    }
    return cfg;
}

function setRef(ref, value) {
    if (ref === undefined) return;
    if ("current" in ref) {
        ref.current = value;
    } else if (isFunction(ref)) {
        ref(value);
    } else if (isArray(ref)) {
        const ctx = ref[0];
        let refs = ctx.refs;
        if (refs === undefined) {
            refs = newHashObj();
            ctx.refs = refs;
        }
        refs[ref[1]] = value;
    }
}

function unsetRef(ref, value) {
    if (ref === undefined) return;
    if ("current" in ref) {
        if (ref.current == value) ref.current = undefined;
    } else if (isFunction(ref)) {
        ref(undefined, value);
    } else if (isArray(ref)) {
        const ctx = ref[0];
        let refs = ctx.refs;
        if (refs === undefined) {
            refs = newHashObj();
            ctx.refs = refs;
        }
        if (refs[ref[1]] == value) refs[ref[1]] = undefined;
    }
}

let focusRootStack = [];

let focusRootTop = null;

export function registerFocusRoot(ctx) {
    focusRootStack.push(ctx.me);
    addDisposable(ctx, unregisterFocusRoot);
    ignoreShouldChange();
}

export function unregisterFocusRoot(ctx) {
    let idx = focusRootStack.indexOf(ctx.me);
    if (idx !== -1) {
        focusRootStack.splice(idx, 1);
        ignoreShouldChange();
    }
}

let createNodeStyle;

let updateNodeStyle;

let style;

export function internalSetCssInJsCallbacks(create, update, s) {
    createNodeStyle = create;
    updateNodeStyle = update;
    style = s;
}

let currentCtx;

let hookId = -1;

export function getCurrentCtx() {
    return currentCtx;
}

export function setCurrentCtx(ctx) {
    currentCtx = ctx;
}

let measureFullComponentDuration = false;

let measureComponentMethods = false;

export function setMeasureConfiguration(conf) {
    measureFullComponentDuration = conf.measureFullComponentDuration;
    measureComponentMethods = conf.measureComponentMethods;
}

export function createNode(n, parentNode, createInto, createBefore) {
    var c = {
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
    };
    var backupInSvg = inSvg;
    var backupInNotFocusable = inNotFocusable;
    var component = c.component;
    var el;
    if (DEBUG && component && measureFullComponentDuration) {
        var componentStartMark = window.performance.mark(`create ${frameCounter} ${++visitedComponentCounter}`);
    }
    setRef(c.ref, c);
    if (component) {
        var ctx;
        if (component.ctxClass) {
            ctx = new component.ctxClass(c.data || {}, c);
            if (ctx.data === undefined) ctx.data = c.data || {};
            if (ctx.me === undefined) ctx.me = c;
        } else {
            ctx = new BobrilCtx(c.data || {}, c);
        }
        ctx.cfg = n.cfg === undefined ? findCfg(parentNode) : n.cfg;
        c.ctx = ctx;
        currentCtx = ctx;
        if (component.init) {
            if (DEBUG && measureComponentMethods) {
                var startMark = window.performance.mark(`${component.id} [init]`);
            }
            component.init(ctx, c);
            if (DEBUG && measureComponentMethods) endMeasure(startMark);
        }
        if (beforeRenderCallback !== noop) beforeRenderCallback(n, RenderPhase.Create);
        if (component.render) {
            hookId = 0;
            if (DEBUG && measureComponentMethods) {
                var startMark = window.performance.mark(`${component.id} [render]`);
            }
            component.render(ctx, c);
            if (DEBUG && measureComponentMethods) endMeasure(startMark);
            hookId = -1;
        }
        currentCtx = undefined;
    } else {
        if (DEBUG) Object.freeze(n);
    }
    var tag = c.tag;
    if (tag === "-") {
        c.tag = undefined;
        c.children = undefined;
        if (DEBUG && component && measureFullComponentDuration) endMeasure(componentStartMark, `${component.id} create`);
        return c;
    } else if (tag === "@") {
        createInto = c.data;
        createBefore = null;
        tag = undefined;
    }
    var children = c.children;
    var inSvgForeignObject = false;
    if (isNumber(children)) {
        children = "" + children;
        c.children = children;
    }
    if (tag === undefined) {
        if (isString(children)) {
            el = createTextNode(children);
            c.element = el;
            domNode2node.set(el, c);
            createInto.insertBefore(el, createBefore);
        } else {
            createChildren(c, createInto, createBefore);
        }
        if (component) {
            if (component.postRender) {
                if (DEBUG && measureComponentMethods) {
                    var startMark = window.performance.mark(`${component.id} [postRender]`);
                }
                component.postRender(c.ctx, c);
                if (DEBUG && measureComponentMethods) endMeasure(startMark);
            }
            pushInitCallback(c);
        }
        if (DEBUG && component && measureFullComponentDuration) endMeasure(componentStartMark, `${component.id} create`);
        return c;
    }
    if (tag === "/") {
        var htmlText = children;
        if (htmlText === "") {} else if (createBefore == undefined) {
            var before = createInto.lastChild;
            createInto.insertAdjacentHTML("beforeend", htmlText);
            c.element = [];
            if (before) {
                before = before.nextSibling;
            } else {
                before = createInto.firstChild;
            }
            while (before) {
                domNode2node.set(before, c);
                c.element.push(before);
                before = before.nextSibling;
            }
        } else {
            el = createBefore;
            var elPrev = createBefore.previousSibling;
            var removeEl = false;
            var parent = createInto;
            if (!el.insertAdjacentHTML) {
                el = parent.insertBefore(createEl("i"), el);
                removeEl = true;
            }
            el.insertAdjacentHTML("beforebegin", htmlText);
            if (elPrev) {
                elPrev = elPrev.nextSibling;
            } else {
                elPrev = parent.firstChild;
            }
            var newElements = [];
            while (elPrev !== el) {
                domNode2node.set(elPrev, c);
                newElements.push(elPrev);
                elPrev = elPrev.nextSibling;
            }
            c.element = newElements;
            if (removeEl) {
                parent.removeChild(el);
            }
        }
        if (component) {
            if (component.postRender) {
                if (DEBUG && measureComponentMethods) {
                    var startMark = window.performance.mark(`${component.id} [postRender]`);
                }
                component.postRender(c.ctx, c);
                if (DEBUG && measureComponentMethods) endMeasure(startMark);
            }
            pushInitCallback(c);
        }
        if (DEBUG && component && measureFullComponentDuration) endMeasure(componentStartMark, `${component.id} create`);
        return c;
    }
    if (inSvg || tag === "svg") {
        el = document.createElementNS("http://www.w3.org/2000/svg", tag);
        inSvgForeignObject = tag === "foreignObject";
        inSvg = !inSvgForeignObject;
    } else {
        el = createEl(tag);
    }
    createInto.insertBefore(el, createBefore);
    domNode2node.set(el, c);
    c.element = el;
    createChildren(c, el, null);
    if (component) {
        if (component.postRender) {
            if (DEBUG && measureComponentMethods) {
                var startMark = window.performance.mark(`${component.id} [postRender]`);
            }
            component.postRender(c.ctx, c);
            if (DEBUG && measureComponentMethods) endMeasure(startMark);
        }
    }
    if (inNotFocusable && focusRootTop === c) inNotFocusable = false;
    if (inSvgForeignObject) inSvg = true;
    let [newClassName, newStyle, newAttrs] = enrichNode(c, c);
    if (newAttrs || inNotFocusable) c.attrs = updateElement(c, el, newAttrs, {}, inNotFocusable);
    createNodeStyle(el, newStyle, newClassName, c, inSvg);
    inSvg = backupInSvg;
    inNotFocusable = backupInNotFocusable;
    pushInitCallback(c);
    if (DEBUG && component && measureFullComponentDuration) endMeasure(componentStartMark, `${component.id} create`);
    return c;
}

export function applyDynamicStyle(factory, c) {
    var ctxStyle = c.ctxStyle;
    var backupCtx = currentCtx;
    if (ctxStyle === undefined) {
        ctxStyle = new BobrilCtx(factory, c);
        c.ctxStyle = ctxStyle;
        currentCtx = ctxStyle;
        if (beforeRenderCallback !== noop) beforeRenderCallback(ctxStyle, RenderPhase.Create);
    } else {
        currentCtx = ctxStyle;
        if (beforeRenderCallback !== noop) beforeRenderCallback(ctxStyle, RenderPhase.Update);
    }
    hookId = 0;
    var s = factory();
    hookId = -1;
    currentCtx = backupCtx;
    return style({}, s);
}

export function destroyDynamicStyle(c) {
    let ctxStyle = c.ctxStyle;
    if (ctxStyle !== undefined) {
        currentCtx = ctxStyle;
        if (beforeRenderCallback !== noop) beforeRenderCallback(ctxStyle, RenderPhase.Destroy);
        let disposables = ctxStyle.disposables;
        if (isArray(disposables)) {
            for (let i = disposables.length; i-- > 0; ) {
                let d = disposables[i];
                if (isFunction(d)) d(ctxStyle); else d.dispose();
            }
        }
        currentCtx = undefined;
    }
}

export function setKeysInClassNames(value) {
    if (isFunction(value)) {
        enrichNode = value;
    } else if (value) {
        enrichNode = ((c, n) => {
            var add = "";
            do {
                var k = c.key;
                if (k) add = " " + k + add;
                c = c.parent;
            } while (c != undefined && c.element == undefined);
            if (!add.length) add = n.className; else {
                if (n.className) add = n.className + add; else add = add.slice(1);
            }
            return [ add, n.style, n.attrs ];
        });
    } else {
        enrichNode = ((_, n) => {
            return [ n.className, n.style, n.attrs ];
        });
    }
}

let enrichNode;

setKeysInClassNames();

function normalizeNode(n) {
    if (n === false || n === true || n === null) return undefined;
    if (isString(n)) {
        return {
            children: n
        };
    }
    if (isNumber(n)) {
        return {
            children: "" + n
        };
    }
    return n;
}

function createChildren(c, createInto, createBefore) {
    var ch = c.children;
    if (isString(ch)) {
        createInto.textContent = ch;
        return;
    }
    let res = [];
    flattenVdomChildren(res, ch);
    for (let i = 0; i < res.length; i++) {
        res[i] = createNode(res[i], c, createInto, createBefore);
    }
    c.children = res;
}

function destroyNode(c) {
    unsetRef(c.ref, c);
    let ch = c.children;
    if (isArray(ch)) {
        for (let i = 0, l = ch.length; i < l; i++) {
            destroyNode(ch[i]);
        }
    }
    let component = c.component;
    if (component) {
        let ctx = c.ctx;
        currentCtx = ctx;
        if (beforeRenderCallback !== noop) beforeRenderCallback(c, RenderPhase.Destroy);
        if (component.destroy) component.destroy(ctx, c, c.element);
        let disposables = ctx.disposables;
        if (isArray(disposables)) {
            for (let i = disposables.length; i-- > 0; ) {
                let d = disposables[i];
                if (isFunction(d)) d(ctx); else d.dispose();
            }
        }
        currentCtx = undefined;
    }
    destroyDynamicStyle(c);
    if (c.tag === "@") {
        removeNodeRecursive(c);
    }
}

export function addDisposable(ctx, disposable) {
    let disposables = ctx.disposables;
    if (disposables == undefined) {
        disposables = [];
        ctx.disposables = disposables;
    }
    disposables.push(disposable);
}

export function isDisposable(val) {
    return isObject(val) && val["dispose"];
}

function removeNodeRecursive(c) {
    var el = c.element;
    if (isArray(el)) {
        var pa = el[0].parentNode;
        if (pa) {
            for (let i = 0; i < el.length; i++) {
                pa.removeChild(el[i]);
            }
        }
    } else if (el != null) {
        let p = el.parentNode;
        if (p) p.removeChild(el);
    } else {
        var ch = c.children;
        if (isArray(ch)) {
            for (var i = 0, l = ch.length; i < l; i++) {
                removeNodeRecursive(ch[i]);
            }
        }
    }
}

function removeNode(c) {
    destroyNode(c);
    removeNodeRecursive(c);
}

var roots = newHashObj();

var domNode2node = new WeakMap();

export function vdomPath(n) {
    var res = [];
    while (n != undefined) {
        var bn = domNode2node.get(n);
        if (bn !== undefined) {
            do {
                res.push(bn);
                bn = bn.parent;
            } while (bn !== undefined);
            res.reverse();
            return res;
        }
        n = n.parentNode;
    }
    return res;
}

export function deref(n) {
    while (n != undefined) {
        var bn = domNode2node.get(n);
        if (bn !== undefined) {
            return bn;
        }
        n = n.parentNode;
    }
    return undefined;
}

function finishUpdateNode(n, c, component) {
    if (component) {
        if (component.postRender) {
            currentCtx = c.ctx;
            component.postRender(currentCtx, n, c);
            currentCtx = undefined;
        }
    }
    c.data = n.data;
    pushUpdateCallback(c);
}

function finishUpdateNodeWithoutChange(c, createInto, createBefore) {
    currentCtx = undefined;
    if (isArray(c.children)) {
        const backupInSvg = inSvg;
        const backupInNotFocusable = inNotFocusable;
        if (c.tag === "svg") {
            inSvg = true;
        } else if (inSvg && c.tag === "foreignObject") inSvg = false;
        if (inNotFocusable && focusRootTop === c) inNotFocusable = false;
        selectedUpdate(c.children, c.element || createInto, c.element != null ? null : createBefore);
        inSvg = backupInSvg;
        inNotFocusable = backupInNotFocusable;
    }
    pushUpdateEverytimeCallback(c);
}

export function updateNode(n, c, createInto, createBefore, deepness, inSelectedUpdate) {
    var component = n.component;
    var bigChange = false;
    var ctx = c.ctx;
    if (DEBUG && component && measureFullComponentDuration) {
        var componentStartMark = window.performance.mark(`update ${frameCounter} ${++visitedComponentCounter}`);
    }
    if (component != null && ctx != null) {
        let locallyInvalidated = false;
        if (ctx[ctxInvalidated] >= frameCounter) {
            deepness = Math.max(deepness, ctx[ctxDeepness]);
            locallyInvalidated = true;
        }
        if (component.id !== c.component.id) {
            bigChange = true;
        } else {
            currentCtx = ctx;
            if (n.cfg !== undefined) ctx.cfg = n.cfg; else ctx.cfg = findCfg(c.parent);
            if (component.shouldChange) if (!ignoringShouldChange && !locallyInvalidated) {
                if (DEBUG && measureComponentMethods) {
                    var startMark = window.performance.mark(`${component.id} [shouldChange]`);
                }
                const shouldChange = component.shouldChange(ctx, n, c);
                if (DEBUG && measureComponentMethods) endMeasure(startMark);
                if (!shouldChange) {
                    finishUpdateNodeWithoutChange(c, createInto, createBefore);
                    if (DEBUG && measureFullComponentDuration) endMeasure(componentStartMark, `${component.id} update`);
                    return c;
                }
            }
            ctx.data = n.data || {};
            c.component = component;
            if (beforeRenderCallback !== noop) beforeRenderCallback(n, inSelectedUpdate ? RenderPhase.LocalUpdate : RenderPhase.Update);
            if (component.render) {
                c.orig = n;
                n = assign({}, n);
                c.cfg = undefined;
                if (n.cfg !== undefined) n.cfg = undefined;
                hookId = 0;
                if (DEBUG && measureComponentMethods) {
                    var startMark = window.performance.mark(`${component.id} [render]`);
                }
                component.render(ctx, n, c);
                if (DEBUG && measureComponentMethods) endMeasure(startMark);
                hookId = -1;
                if (n.cfg !== undefined) {
                    if (c.cfg === undefined) c.cfg = n.cfg; else assign(c.cfg, n.cfg);
                }
            }
            currentCtx = undefined;
        }
    } else {
        if (c.orig === n && !ignoringShouldChange) {
            finishUpdateNodeWithoutChange(c, createInto, createBefore);
            if (DEBUG && component && measureFullComponentDuration) endMeasure(componentStartMark, `${component.id} update`);
            return c;
        }
        c.orig = n;
        if (DEBUG) Object.freeze(n);
    }
    var newChildren = n.children;
    var cachedChildren = c.children;
    var tag = n.tag;
    if (tag === "-") {
        finishUpdateNodeWithoutChange(c, createInto, createBefore);
        if (DEBUG && component && measureFullComponentDuration) endMeasure(componentStartMark, `${component.id} update`);
        return c;
    }
    const backupInSvg = inSvg;
    const backupInNotFocusable = inNotFocusable;
    if (isNumber(newChildren)) {
        newChildren = "" + newChildren;
    }
    if (bigChange || component != undefined && ctx == undefined || component == undefined && ctx != undefined && ctx.me.component !== emptyObject) {} else if (tag === "/") {
        if (c.tag === "/" && cachedChildren === newChildren) {
            finishUpdateNode(n, c, component);
            if (DEBUG && component && measureFullComponentDuration) endMeasure(componentStartMark, `${component.id} update`);
            return c;
        }
    } else if (tag === c.tag) {
        if (tag === "@") {
            if (n.data !== c.data) {
                var r = createNode(n, c.parent, n.data, null);
                removeNode(c);
                if (DEBUG && component && measureFullComponentDuration) endMeasure(componentStartMark, `${component.id} update`);
                return r;
            }
            createInto = n.data;
            createBefore = getLastDomNode(c);
            if (createBefore != null) createBefore = createBefore.nextSibling;
            tag = undefined;
        }
        if (tag === undefined) {
            if (isString(newChildren) && isString(cachedChildren)) {
                if (newChildren !== cachedChildren) {
                    var el = c.element;
                    el.textContent = newChildren;
                    c.children = newChildren;
                }
            } else {
                if (inNotFocusable && focusRootTop === c) inNotFocusable = false;
                if (deepness <= 0) {
                    if (isArray(cachedChildren)) selectedUpdate(c.children, createInto, createBefore);
                } else {
                    c.children = updateChildren(createInto, newChildren, cachedChildren, c, createBefore, deepness - 1);
                }
                inSvg = backupInSvg;
                inNotFocusable = backupInNotFocusable;
            }
            finishUpdateNode(n, c, component);
            if (DEBUG && component && measureFullComponentDuration) endMeasure(componentStartMark, `${component.id} update`);
            return c;
        } else {
            var inSvgForeignObject = false;
            if (tag === "svg") {
                inSvg = true;
            } else if (inSvg && tag === "foreignObject") {
                inSvgForeignObject = true;
                inSvg = false;
            }
            if (inNotFocusable && focusRootTop === c) inNotFocusable = false;
            var el = c.element;
            if (isString(newChildren) && !isArray(cachedChildren)) {
                if (newChildren !== cachedChildren) {
                    el.textContent = newChildren;
                    cachedChildren = newChildren;
                }
            } else {
                if (deepness <= 0) {
                    if (isArray(cachedChildren)) selectedUpdate(c.children, el, null);
                } else {
                    cachedChildren = updateChildren(el, newChildren, cachedChildren, c, null, deepness - 1);
                }
            }
            c.children = cachedChildren;
            if (inSvgForeignObject) inSvg = true;
            finishUpdateNode(n, c, component);
            let [newClassName, newStyle, newAttrs] = enrichNode(c, n);
            if (c.attrs || newAttrs || inNotFocusable) c.attrs = updateElement(c, el, newAttrs, c.attrs || {}, inNotFocusable);
            updateNodeStyle(el, newStyle, newClassName, c, inSvg);
            inSvg = backupInSvg;
            inNotFocusable = backupInNotFocusable;
            if (DEBUG && component && measureFullComponentDuration) endMeasure(componentStartMark, `${component.id} update`);
            return c;
        }
    }
    var insertBefore = getDomNode(c);
    var parEl = c.element;
    if (isArray(parEl)) parEl = parEl[0];
    if (parEl != undefined) parEl = parEl.parentNode;
    if (parEl == undefined) {
        parEl = createInto;
        if (insertBefore != undefined && insertBefore.parentNode != parEl) insertBefore = createBefore;
    }
    if (insertBefore == undefined) insertBefore = createBefore;
    var r = createNode(n, c.parent, parEl, insertBefore);
    removeNode(c);
    if (DEBUG && component && measureFullComponentDuration) endMeasure(componentStartMark, `${component.id} update`);
    return r;
}

export function getDomNode(c) {
    if (c === undefined || c.tag == "@") return null;
    var el = c.element;
    if (el != null) {
        if (isArray(el)) return el[0];
        return el;
    }
    var ch = c.children;
    if (!isArray(ch)) return null;
    for (var i = 0; i < ch.length; i++) {
        el = getDomNode(ch[i]);
        if (el) return el;
    }
    return null;
}

export function getLastDomNode(c) {
    if (c === undefined) return null;
    var el = c.element;
    if (el != null) {
        if (isArray(el)) return el[el.length - 1];
        return el;
    }
    var ch = c.children;
    if (!isArray(ch)) return null;
    for (var i = ch.length; i-- > 0; ) {
        el = getLastDomNode(ch[i]);
        if (el) return el;
    }
    return null;
}

function findNextNode(a, i, len, def) {
    while (++i < len) {
        var ai = a[i];
        if (ai == undefined) continue;
        var n = getDomNode(ai);
        if (n != null) return n;
    }
    return def;
}

export function callPostCallbacks() {
    var count = updateInstance.length;
    for (var i = 0; i < count; i++) {
        var n = updateInstance[i];
        currentCtx = n.ctx;
        if (currentCtx) {
            if (DEBUG && measureComponentMethods) {
                var startMark = window.performance.mark(`${n.component.id} [post*]`);
            }
            updateCall[i].call(n.component, currentCtx, n, n.element);
            if (DEBUG && measureComponentMethods) endMeasure(startMark);
        }
        currentCtx = n.ctxStyle;
        if (currentCtx) {
            if (DEBUG && measureComponentMethods) {
                var startMark = window.performance.mark(`${n.component.id} style [post*]`);
            }
            updateCall[i].call(n.component, currentCtx, n, n.element);
            if (DEBUG && measureComponentMethods) endMeasure(startMark);
        }
    }
    currentCtx = undefined;
    updateCall = [];
    updateInstance = [];
}

export function callEffects() {
    var count = effectInstance.length;
    for (var i = 0; i < count; i++) {
        var n = effectInstance[i];
        currentCtx = n.ctx;
        if (currentCtx) {
            if (DEBUG && measureComponentMethods) {
                var startMark = window.performance.mark(`${n.component.id} [effect*]`);
            }
            const hooks = currentCtx.$hooks;
            const len = hooks.length;
            for (let i = 0; i < len; i++) {
                const hook = hooks[i];
                const fn = hook.useEffect;
                if (fn !== undefined) {
                    fn.call(hook, currentCtx);
                }
            }
            if (DEBUG && measureComponentMethods) endMeasure(startMark);
        }
        currentCtx = n.ctxStyle;
        if (currentCtx) {
            if (DEBUG && measureComponentMethods) {
                var startMark = window.performance.mark(`${n.component.id} style [effect*]`);
            }
            const hooks = currentCtx.$hooks;
            const len = hooks.length;
            for (let i = 0; i < len; i++) {
                const hook = hooks[i];
                const fn = hook.useEffect;
                if (fn !== undefined) {
                    fn.call(hook, currentCtx);
                }
            }
            if (DEBUG && measureComponentMethods) endMeasure(startMark);
        }
    }
    currentCtx = undefined;
    effectInstance = [];
}

function updateNodeInUpdateChildren(newNode, cachedChildren, cachedIndex, cachedLength, createBefore, element, deepness) {
    cachedChildren[cachedIndex] = updateNode(newNode, cachedChildren[cachedIndex], element, findNextNode(cachedChildren, cachedIndex, cachedLength, createBefore), deepness);
}

function reorderInUpdateChildrenRec(c, element, before) {
    var el = c.element;
    if (el != null) {
        if (isArray(el)) {
            for (var i = 0; i < el.length; i++) {
                element.insertBefore(el[i], before);
            }
        } else element.insertBefore(el, before);
        return;
    }
    var ch = c.children;
    if (!isArray(ch)) return;
    for (var i = 0; i < ch.length; i++) {
        reorderInUpdateChildrenRec(ch[i], element, before);
    }
}

function reorderInUpdateChildren(cachedChildren, cachedIndex, cachedLength, createBefore, element) {
    var before = findNextNode(cachedChildren, cachedIndex, cachedLength, createBefore);
    var cur = cachedChildren[cachedIndex];
    var what = getDomNode(cur);
    if (what != null && what !== before) {
        reorderInUpdateChildrenRec(cur, element, before);
    }
}

function reorderAndUpdateNodeInUpdateChildren(newNode, cachedChildren, cachedIndex, cachedLength, createBefore, element, deepness) {
    var before = findNextNode(cachedChildren, cachedIndex, cachedLength, createBefore);
    var cur = cachedChildren[cachedIndex];
    var what = getDomNode(cur);
    if (what != null && what !== before) {
        reorderInUpdateChildrenRec(cur, element, before);
    }
    cachedChildren[cachedIndex] = updateNode(newNode, cur, element, before, deepness);
}

function recursiveFlattenVdomChildren(res, children) {
    if (children == undefined) return;
    if (isArray(children)) {
        for (let i = 0; i < children.length; i++) {
            recursiveFlattenVdomChildren(res, children[i]);
        }
    } else {
        let item = normalizeNode(children);
        if (item !== undefined) res.push(item);
    }
}

function flattenVdomChildren(res, children) {
    recursiveFlattenVdomChildren(res, children);
    if (DEBUG) {
        var set = new Set();
        for (let i = 0; i < res.length; i++) {
            const key = res[i].key;
            if (key == undefined) continue;
            if (set.has(key)) {
                console.warn("Duplicate Bobril node key " + key);
            }
            set.add(key);
        }
    }
}

export function updateChildren(element, newChildren, cachedChildren, parentNode, createBefore, deepness) {
    if (cachedChildren == undefined) cachedChildren = [];
    if (!isArray(cachedChildren)) {
        if (element.firstChild) element.removeChild(element.firstChild);
        cachedChildren = [];
    }
    let newCh = [];
    flattenVdomChildren(newCh, newChildren);
    return updateChildrenCore(element, newCh, cachedChildren, parentNode, createBefore, deepness);
}

function updateChildrenCore(element, newChildren, cachedChildren, parentNode, createBefore, deepness) {
    let newEnd = newChildren.length;
    var cachedLength = cachedChildren.length;
    let cachedEnd = cachedLength;
    let newIndex = 0;
    let cachedIndex = 0;
    while (newIndex < newEnd && cachedIndex < cachedEnd) {
        if (newChildren[newIndex].key === cachedChildren[cachedIndex].key) {
            updateNodeInUpdateChildren(newChildren[newIndex], cachedChildren, cachedIndex, cachedLength, createBefore, element, deepness);
            newIndex++;
            cachedIndex++;
            continue;
        }
        while (true) {
            if (newChildren[newEnd - 1].key === cachedChildren[cachedEnd - 1].key) {
                newEnd--;
                cachedEnd--;
                updateNodeInUpdateChildren(newChildren[newEnd], cachedChildren, cachedEnd, cachedLength, createBefore, element, deepness);
                if (newIndex < newEnd && cachedIndex < cachedEnd) continue;
            }
            break;
        }
        if (newIndex < newEnd && cachedIndex < cachedEnd) {
            if (newChildren[newIndex].key === cachedChildren[cachedEnd - 1].key && newChildren[newEnd - 1].key === cachedChildren[cachedIndex].key) {
                var temp = cachedChildren[cachedIndex];
                cachedChildren[cachedIndex] = cachedChildren[cachedEnd - 1];
                cachedChildren[cachedEnd - 1] = temp;
                reorderAndUpdateNodeInUpdateChildren(newChildren[newIndex], cachedChildren, cachedIndex, cachedLength, createBefore, element, deepness);
                newIndex++;
                cachedIndex++;
                newEnd--;
                cachedEnd--;
                reorderAndUpdateNodeInUpdateChildren(newChildren[newEnd], cachedChildren, cachedEnd, cachedLength, createBefore, element, deepness);
                continue;
            }
        }
        break;
    }
    if (cachedIndex === cachedEnd) {
        if (newIndex === newEnd) {
            return cachedChildren;
        }
        while (newIndex < newEnd) {
            cachedChildren.splice(cachedIndex, 0, createNode(newChildren[newIndex], parentNode, element, findNextNode(cachedChildren, cachedIndex - 1, cachedLength, createBefore)));
            cachedIndex++;
            cachedEnd++;
            cachedLength++;
            newIndex++;
        }
        return cachedChildren;
    }
    if (newIndex === newEnd) {
        while (cachedIndex < cachedEnd) {
            cachedEnd--;
            removeNode(cachedChildren[cachedEnd]);
            cachedChildren.splice(cachedEnd, 1);
        }
        return cachedChildren;
    }
    var cachedKeys = newHashObj();
    var newKeys = newHashObj();
    var key;
    var node;
    var backupNewIndex = newIndex;
    var backupCachedIndex = cachedIndex;
    var deltaKeyless = 0;
    for (;cachedIndex < cachedEnd; cachedIndex++) {
        node = cachedChildren[cachedIndex];
        key = node.key;
        if (key != null) {
            assert(!(key in cachedKeys));
            cachedKeys[key] = cachedIndex;
        } else deltaKeyless--;
    }
    var keyLess = -deltaKeyless - deltaKeyless;
    for (;newIndex < newEnd; newIndex++) {
        node = newChildren[newIndex];
        key = node.key;
        if (key != null) {
            assert(!(key in newKeys));
            newKeys[key] = newIndex;
        } else deltaKeyless++;
    }
    keyLess += deltaKeyless;
    var delta = 0;
    newIndex = backupNewIndex;
    cachedIndex = backupCachedIndex;
    var cachedKey;
    while (cachedIndex < cachedEnd && newIndex < newEnd) {
        if (cachedChildren[cachedIndex] === null) {
            cachedChildren.splice(cachedIndex, 1);
            cachedEnd--;
            cachedLength--;
            delta--;
            continue;
        }
        cachedKey = cachedChildren[cachedIndex].key;
        if (cachedKey == undefined) {
            cachedIndex++;
            continue;
        }
        key = newChildren[newIndex].key;
        if (key == undefined) {
            newIndex++;
            while (newIndex < newEnd) {
                key = newChildren[newIndex].key;
                if (key != undefined) break;
                newIndex++;
            }
            if (key == undefined) break;
        }
        var akPos = cachedKeys[key];
        if (akPos === undefined) {
            cachedChildren.splice(cachedIndex, 0, createNode(newChildren[newIndex], parentNode, element, findNextNode(cachedChildren, cachedIndex - 1, cachedLength, createBefore)));
            delta++;
            newIndex++;
            cachedIndex++;
            cachedEnd++;
            cachedLength++;
            continue;
        }
        if (!(cachedKey in newKeys)) {
            removeNode(cachedChildren[cachedIndex]);
            cachedChildren.splice(cachedIndex, 1);
            delta--;
            cachedEnd--;
            cachedLength--;
            continue;
        }
        if (cachedIndex === akPos + delta) {
            updateNodeInUpdateChildren(newChildren[newIndex], cachedChildren, cachedIndex, cachedLength, createBefore, element, deepness);
            newIndex++;
            cachedIndex++;
        } else {
            cachedChildren.splice(cachedIndex, 0, cachedChildren[akPos + delta]);
            delta++;
            cachedChildren[akPos + delta] = null;
            reorderAndUpdateNodeInUpdateChildren(newChildren[newIndex], cachedChildren, cachedIndex, cachedLength, createBefore, element, deepness);
            cachedIndex++;
            cachedEnd++;
            cachedLength++;
            newIndex++;
        }
    }
    while (cachedIndex < cachedEnd) {
        if (cachedChildren[cachedIndex] === null) {
            cachedChildren.splice(cachedIndex, 1);
            cachedEnd--;
            cachedLength--;
            continue;
        }
        if (cachedChildren[cachedIndex].key != null) {
            removeNode(cachedChildren[cachedIndex]);
            cachedChildren.splice(cachedIndex, 1);
            cachedEnd--;
            cachedLength--;
            continue;
        }
        cachedIndex++;
    }
    while (newIndex < newEnd) {
        key = newChildren[newIndex].key;
        if (key != null) {
            cachedChildren.splice(cachedIndex, 0, createNode(newChildren[newIndex], parentNode, element, findNextNode(cachedChildren, cachedIndex - 1, cachedLength, createBefore)));
            cachedEnd++;
            cachedLength++;
            delta++;
            cachedIndex++;
        }
        newIndex++;
    }
    if (!keyLess) return cachedChildren;
    keyLess = keyLess - Math.abs(deltaKeyless) >> 1;
    newIndex = backupNewIndex;
    cachedIndex = backupCachedIndex;
    while (newIndex < newEnd) {
        if (cachedIndex < cachedEnd) {
            cachedKey = cachedChildren[cachedIndex].key;
            if (cachedKey != null) {
                cachedIndex++;
                continue;
            }
        }
        key = newChildren[newIndex].key;
        if (newIndex < cachedEnd && key === cachedChildren[newIndex].key) {
            if (key != null) {
                newIndex++;
                continue;
            }
            updateNodeInUpdateChildren(newChildren[newIndex], cachedChildren, newIndex, cachedLength, createBefore, element, deepness);
            keyLess--;
            newIndex++;
            cachedIndex = newIndex;
            continue;
        }
        if (key != null) {
            assert(newIndex === cachedIndex);
            if (keyLess === 0 && deltaKeyless < 0) {
                while (true) {
                    removeNode(cachedChildren[cachedIndex]);
                    cachedChildren.splice(cachedIndex, 1);
                    cachedEnd--;
                    cachedLength--;
                    deltaKeyless++;
                    assert(cachedIndex !== cachedEnd, "there still need to exist key node");
                    if (cachedChildren[cachedIndex].key != null) break;
                }
                continue;
            }
            while (cachedChildren[cachedIndex].key == undefined) cachedIndex++;
            assert(key === cachedChildren[cachedIndex].key);
            cachedChildren.splice(newIndex, 0, cachedChildren[cachedIndex]);
            cachedChildren.splice(cachedIndex + 1, 1);
            reorderInUpdateChildren(cachedChildren, newIndex, cachedLength, createBefore, element);
            newIndex++;
            cachedIndex = newIndex;
            continue;
        }
        if (cachedIndex < cachedEnd) {
            cachedChildren.splice(newIndex, 0, cachedChildren[cachedIndex]);
            cachedChildren.splice(cachedIndex + 1, 1);
            reorderAndUpdateNodeInUpdateChildren(newChildren[newIndex], cachedChildren, newIndex, cachedLength, createBefore, element, deepness);
            keyLess--;
            newIndex++;
            cachedIndex++;
        } else {
            cachedChildren.splice(newIndex, 0, createNode(newChildren[newIndex], parentNode, element, findNextNode(cachedChildren, newIndex - 1, cachedLength, createBefore)));
            cachedEnd++;
            cachedLength++;
            newIndex++;
            cachedIndex++;
        }
    }
    while (cachedEnd > newIndex) {
        cachedEnd--;
        removeNode(cachedChildren[cachedEnd]);
        cachedChildren.splice(cachedEnd, 1);
    }
    return cachedChildren;
}

var hasNativeRaf = false;

var nativeRaf = window.requestAnimationFrame;

if (nativeRaf) {
    nativeRaf(param => {
        if (param === +param) hasNativeRaf = true;
    });
}

const setTimeout = window.setTimeout;

export const now = Date.now || (() => new Date().getTime());

var startTime = now();

var lastTickTime = 0;

function requestAnimationFrame(callback) {
    if (hasNativeRaf) {
        nativeRaf(callback);
    } else {
        var delay = 50 / 3 + lastTickTime - now();
        if (delay < 0) delay = 0;
        setTimeout(() => {
            lastTickTime = now();
            callback(lastTickTime - startTime);
        }, delay);
    }
}

var ctxInvalidated = "$invalidated";

var ctxDeepness = "$deepness";

var fullRecreateRequested = true;

var scheduled = false;

var isInvalidated = true;

let initializing = true;

var uptimeMs = 0;

var frameCounter = 0;

var lastFrameDurationMs = 0;

var renderFrameBegin = 0;

var regEvents = newHashObj();

var registryEvents;

export function addEvent(name, priority, callback) {
    if (registryEvents == undefined) registryEvents = newHashObj();
    var list = registryEvents[name] || [];
    list.push({
        priority,
        callback
    });
    registryEvents[name] = list;
}

export function emitEvent(name, ev, target, node) {
    var events = regEvents[name];
    if (events) for (var i = 0; i < events.length; i++) {
        if (events[i](ev, target, node)) return true;
    }
    return false;
}

var isPassiveEventHandlerSupported = false;

try {
    var options = Object.defineProperty({}, "passive", {
        get: function() {
            isPassiveEventHandlerSupported = true;
        }
    });
    window.addEventListener("blur", options, options);
    window.removeEventListener("blur", options, options);
} catch (err) {
    isPassiveEventHandlerSupported = false;
}

var listeningEventDeepness = 0;

function addListener(el, name, nonbody) {
    if (name[0] == "!") return;
    var capture = name[0] == "^";
    var eventName = name;
    if (name[0] == "@") {
        if (nonbody) return;
        eventName = name.slice(1);
        el = document;
    }
    if (capture) {
        if (nonbody) return;
        eventName = name.slice(1);
    }
    function enhanceEvent(ev) {
        var t = ev.target || el;
        var n = deref(t);
        listeningEventDeepness++;
        emitEvent(name, ev, t, n);
        listeningEventDeepness--;
        if (listeningEventDeepness == 0 && deferSyncUpdateRequested) syncUpdate();
    }
    if (!nonbody) {
        if ("on" + eventName in window) el = window;
    }
    el.addEventListener(eventName, enhanceEvent, isPassiveEventHandlerSupported ? {
        capture,
        passive: false
    } : capture);
}

function initEvents() {
    if (registryEvents === undefined) return;
    var eventNames = Object.keys(registryEvents);
    for (var j = 0; j < eventNames.length; j++) {
        var eventName = eventNames[j];
        var arr = registryEvents[eventName];
        arr = arr.sort((a, b) => a.priority - b.priority);
        regEvents[eventName] = arr.map(v => v.callback);
    }
    registryEvents = undefined;
    var body = document.body;
    for (var i = 0; i < eventNames.length; i++) {
        addListener(body, eventNames[i], false);
    }
}

export function addEventListeners(el) {
    var eventNames = Object.keys(regEvents);
    for (var i = 0; i < eventNames.length; i++) {
        addListener(el, eventNames[i], true);
    }
}

function selectedUpdate(cache, element, createBefore) {
    var len = cache.length;
    for (var i = 0; i < len; i++) {
        var node = cache[i];
        var ctx = node.ctx;
        if (ctx != null && ctx[ctxInvalidated] >= frameCounter) {
            cache[i] = updateNode(node.orig, node, element, findNextNode(cache, i, len, createBefore), ctx[ctxDeepness], true);
        } else {
            ctx = node.ctxStyle;
            if (ctx != null && ctx[ctxInvalidated] >= frameCounter) {
                updateNodeStyle(node.element, ctx.data, undefined, node, inSvg);
            }
            if (isArray(node.children)) {
                var backupInSvg = inSvg;
                var backupInNotFocusable = inNotFocusable;
                if (inNotFocusable && focusRootTop === node) inNotFocusable = false;
                if (node.tag === "svg") inSvg = true; else if (inSvg && node.tag === "foreignObject") inSvg = false;
                var thisElement = node.element;
                if (thisElement != undefined) {
                    selectedUpdate(node.children, thisElement, null);
                } else {
                    selectedUpdate(node.children, element, findNextNode(cache, i, len, createBefore));
                }
                pushUpdateEverytimeCallback(node);
                inSvg = backupInSvg;
                inNotFocusable = backupInNotFocusable;
            }
        }
    }
}

function isLogicalParent(parent, child, rootIds) {
    while (child != null) {
        if (parent === child) return true;
        let p = child.parent;
        if (p == undefined) {
            for (var i = 0; i < rootIds.length; i++) {
                var r = roots[rootIds[i]];
                if (!r) continue;
                if (r.n === child) {
                    p = r.p;
                    break;
                }
            }
        }
        child = p;
    }
    return false;
}

var deferSyncUpdateRequested = false;

export function syncUpdate() {
    deferSyncUpdateRequested = false;
    internalUpdate(now() - startTime);
}

export function deferSyncUpdate() {
    if (listeningEventDeepness > 0) {
        deferSyncUpdateRequested = true;
        return;
    }
    syncUpdate();
}

function update(time) {
    scheduled = false;
    internalUpdate(time);
}

var rootIds;

const RootComponent = createVirtualComponent({
    render(ctx, me) {
        const r = ctx.data;
        let c = r.f(r);
        if (c === undefined) {
            me.tag = "-";
        } else {
            me.children = c;
        }
    }
});

let visitedComponentCounter = 0;

function internalUpdate(time) {
    visitedComponentCounter = 0;
    isInvalidated = false;
    renderFrameBegin = now();
    initEvents();
    reallyBeforeFrameCallback();
    frameCounter++;
    ignoringShouldChange = nextIgnoreShouldChange;
    nextIgnoreShouldChange = false;
    uptimeMs = time;
    beforeFrameCallback();
    var fullRefresh = false;
    if (fullRecreateRequested) {
        fullRecreateRequested = false;
        fullRefresh = true;
    }
    listeningEventDeepness++;
    if (DEBUG && (measureComponentMethods || measureFullComponentDuration)) {
        var renderStartMark = window.performance.mark(`render ${frameCounter}`);
    }
    for (let repeat = 0; repeat < 2; repeat++) {
        focusRootTop = focusRootStack.length === 0 ? null : focusRootStack[focusRootStack.length - 1];
        inNotFocusable = false;
        rootIds = Object.keys(roots);
        for (var i = 0; i < rootIds.length; i++) {
            var r = roots[rootIds[i]];
            if (!r) continue;
            var rc = r.n;
            var insertBefore = null;
            for (var j = i + 1; j < rootIds.length; j++) {
                let rafter = roots[rootIds[j]];
                if (rafter === undefined) continue;
                insertBefore = getDomNode(rafter.n);
                if (insertBefore != null) break;
            }
            if (focusRootTop) inNotFocusable = !isLogicalParent(focusRootTop, r.p, rootIds);
            if (r.e === undefined) r.e = document.body;
            if (rc) {
                if (fullRefresh || rc.ctx[ctxInvalidated] >= frameCounter) {
                    let node = RootComponent(r);
                    updateNode(node, rc, r.e, insertBefore, fullRefresh ? 1e6 : rc.ctx[ctxDeepness]);
                } else {
                    if (isArray(r.c)) selectedUpdate(r.c, r.e, insertBefore);
                }
            } else {
                let node = RootComponent(r);
                rc = createNode(node, undefined, r.e, insertBefore);
                r.n = rc;
            }
            r.c = rc.children;
        }
        rootIds = undefined;
        callPostCallbacks();
        if (!deferSyncUpdateRequested) break;
    }
    callEffects();
    deferSyncUpdateRequested = false;
    listeningEventDeepness--;
    let r0 = roots["0"];
    afterFrameCallback(r0 ? r0.c : null);
    if (DEBUG && (measureComponentMethods || measureFullComponentDuration)) endMeasure(renderStartMark, "render");
    lastFrameDurationMs = now() - renderFrameBegin;
}

function endMeasure(startMark, measureName) {
    window.performance.measure(measureName ?? startMark.name, {
        start: startMark.name,
        end: window.performance.mark(startMark.name + "-end").name
    });
}

var nextIgnoreShouldChange = false;

var ignoringShouldChange = false;

export function ignoreShouldChange() {
    nextIgnoreShouldChange = true;
    invalidate();
}

export function setInvalidate(inv) {
    let prev = invalidate;
    invalidate = inv;
    return prev;
}

export var invalidate = (ctx, deepness) => {
    if (ctx != null) {
        if (deepness == undefined) deepness = 1e6;
        if (ctx[ctxInvalidated] !== frameCounter + 1) {
            ctx[ctxInvalidated] = frameCounter + 1;
            ctx[ctxDeepness] = deepness;
        } else {
            if (deepness > ctx[ctxDeepness]) ctx[ctxDeepness] = deepness;
        }
    } else {
        fullRecreateRequested = true;
    }
    isInvalidated = true;
    if (scheduled || initializing) return;
    scheduled = true;
    requestAnimationFrame(update);
};

var defaultElementRoot;

export function setDefaultElementRoot(element) {
    defaultElementRoot = element;
}

var lastRootId = 0;

export function addRoot(factory, element, parent) {
    lastRootId++;
    var rootId = "" + lastRootId;
    roots[rootId] = {
        f: factory,
        e: element ?? defaultElementRoot,
        c: [],
        p: parent,
        n: undefined
    };
    if (rootIds != null) {
        rootIds.push(rootId);
    } else {
        firstInvalidate();
    }
    return rootId;
}

export function removeRoot(id) {
    var root = roots[id];
    if (!root) return;
    if (root.n) removeNode(root.n);
    delete roots[id];
}

export function updateRoot(id, factory) {
    assert(rootIds != null, "updateRoot could be called only from render");
    var root = roots[id];
    assert(root != null);
    if (factory != null) root.f = factory;
    let rootNode = root.n;
    if (rootNode == undefined) return;
    let ctx = rootNode.ctx;
    ctx[ctxInvalidated] = frameCounter;
    ctx[ctxDeepness] = 1e6;
}

export function getRoots() {
    return roots;
}

function finishInitialize() {
    initializing = false;
    invalidate();
}

var beforeInit = finishInitialize;

function firstInvalidate() {
    initializing = true;
    beforeInit();
    beforeInit = finishInitialize;
}

export function init(factory, element) {
    assert(rootIds == undefined, "init should not be called from render");
    removeRoot("0");
    isInvalidated = true;
    roots["0"] = {
        f: factory,
        e: element,
        c: [],
        p: undefined,
        n: undefined
    };
    firstInvalidate();
}

export function setBeforeInit(callback) {
    let prevBeforeInit = beforeInit;
    beforeInit = (() => {
        callback(prevBeforeInit);
    });
}

let afterDomLoaded;

document.addEventListener("DOMContentLoaded", () => {
    if (isFunction(afterDomLoaded)) {
        afterDomLoaded();
    }
    afterDomLoaded = true;
});

setBeforeInit(cb => {
    if (afterDomLoaded === true) {
        cb();
    } else {
        afterDomLoaded = cb;
    }
});

let currentCtxWithEvents;

export function callWithCurrentCtxWithEvents(call, ctx) {
    var backup = currentCtxWithEvents;
    currentCtxWithEvents = ctx;
    try {
        return call();
    } finally {
        currentCtxWithEvents = backup;
    }
}

export function bubble(node, name, param) {
    if (param == undefined) {
        param = {
            target: node
        };
    } else if (isObject(param) && param.target == undefined) {
        param.target = node;
    }
    let res = captureBroadcast(name, param);
    if (res != undefined) return res;
    const prevCtx = currentCtxWithEvents;
    while (node) {
        var c = node.component;
        var ctx = node.ctxStyle;
        if (ctx) {
            currentCtxWithEvents = ctx;
            if (((ctx.$hookFlags | 0) & hasEvents) === hasEvents) {
                var hooks = ctx.$hooks;
                for (var i = 0, l = hooks.length; i < l; i++) {
                    var h = hooks[i];
                    if (h instanceof EventsHook) {
                        var m = h.events[name];
                        if (m !== undefined) {
                            const eventResult = +m.call(ctx, param);
                            if (eventResult == EventResult.HandledPreventDefault) {
                                currentCtxWithEvents = prevCtx;
                                return ctx;
                            }
                            if (eventResult == EventResult.HandledButRunDefault) {
                                currentCtxWithEvents = prevCtx;
                                return undefined;
                            }
                            if (eventResult == EventResult.NotHandledPreventDefault) {
                                res = ctx;
                            }
                        }
                    }
                }
            }
        }
        if (c) {
            ctx = node.ctx;
            currentCtxWithEvents = ctx;
            if (((ctx.$hookFlags | 0) & hasEvents) === hasEvents) {
                var hooks = ctx.$hooks;
                for (var i = 0, l = hooks.length; i < l; i++) {
                    var h = hooks[i];
                    if (h instanceof EventsHook) {
                        var m = h.events[name];
                        if (m !== undefined) {
                            const eventResult = +m.call(ctx, param);
                            if (eventResult == EventResult.HandledPreventDefault) {
                                currentCtxWithEvents = prevCtx;
                                return ctx;
                            }
                            if (eventResult == EventResult.HandledButRunDefault) {
                                currentCtxWithEvents = prevCtx;
                                return undefined;
                            }
                            if (eventResult == EventResult.NotHandledPreventDefault) {
                                res = ctx;
                            }
                        }
                    }
                }
            }
            var m = c[name];
            if (m) {
                const eventResult = +m.call(c, ctx, param);
                if (eventResult == EventResult.HandledPreventDefault) {
                    currentCtxWithEvents = prevCtx;
                    return ctx;
                }
                if (eventResult == EventResult.HandledButRunDefault) {
                    currentCtxWithEvents = prevCtx;
                    return undefined;
                }
                if (eventResult == EventResult.NotHandledPreventDefault) {
                    res = ctx;
                }
            }
            m = c.handleGenericEvent;
            if (m) {
                const eventResult = +m.call(c, ctx, name, param);
                if (eventResult == EventResult.HandledPreventDefault) {
                    currentCtxWithEvents = prevCtx;
                    return ctx;
                }
                if (eventResult == EventResult.HandledButRunDefault) {
                    currentCtxWithEvents = prevCtx;
                    return undefined;
                }
                if (eventResult == EventResult.NotHandledPreventDefault) {
                    res = ctx;
                }
            }
            m = c.shouldStopBubble;
            if (m) {
                if (m.call(c, ctx, name, param)) break;
            }
        }
        node = node.parent;
    }
    currentCtxWithEvents = prevCtx;
    return res;
}

function broadcastEventToNode(node, name, param) {
    if (!node) return undefined;
    let res;
    var c = node.component;
    if (c) {
        var ctx = node.ctx;
        var prevCtx = currentCtxWithEvents;
        currentCtxWithEvents = ctx;
        if (((ctx.$hookFlags | 0) & hasEvents) === hasEvents) {
            var hooks = ctx.$hooks;
            for (var i = 0, l = hooks.length; i < l; i++) {
                var h = hooks[i];
                if (h instanceof EventsHook) {
                    var m = h.events[name];
                    if (m !== undefined) {
                        const eventResult = +m.call(ctx, param);
                        if (eventResult == EventResult.HandledPreventDefault) {
                            currentCtxWithEvents = prevCtx;
                            return ctx;
                        }
                        if (eventResult == EventResult.HandledButRunDefault) {
                            currentCtxWithEvents = prevCtx;
                            return undefined;
                        }
                        if (eventResult == EventResult.NotHandledPreventDefault) {
                            res = ctx;
                        }
                    }
                }
            }
        }
        var m = c[name];
        if (m) {
            const eventResult = +m.call(c, ctx, param);
            if (eventResult == EventResult.HandledPreventDefault) {
                currentCtxWithEvents = prevCtx;
                return ctx;
            }
            if (eventResult == EventResult.HandledButRunDefault) {
                currentCtxWithEvents = prevCtx;
                return undefined;
            }
            if (eventResult == EventResult.NotHandledPreventDefault) {
                res = ctx;
            }
        }
        m = c.shouldStopBroadcast;
        if (m) {
            if (m.call(c, ctx, name, param)) {
                currentCtxWithEvents = prevCtx;
                return res;
            }
        }
        currentCtxWithEvents = prevCtx;
    }
    var ch = node.children;
    if (isArray(ch)) {
        for (var i = 0; i < ch.length; i++) {
            var res2 = broadcastEventToNode(ch[i], name, param);
            if (res2 != undefined) return res2;
        }
    }
    return res;
}

function broadcastCapturedEventToNode(node, name, param) {
    if (!node) return undefined;
    let res;
    var c = node.component;
    var ctx = node.ctxStyle;
    if (ctx) {
        if ((ctx.$hookFlags & hasCaptureEvents) === hasCaptureEvents) {
            var hooks = ctx.$hooks;
            var prevCtx = currentCtxWithEvents;
            currentCtxWithEvents = ctx;
            for (var i = 0, l = hooks.length; i < l; i++) {
                var h = hooks[i];
                if (h instanceof CaptureEventsHook) {
                    var m = h.events[name];
                    if (m !== undefined) {
                        const eventResult = +m.call(ctx, param);
                        if (eventResult == EventResult.HandledPreventDefault) {
                            currentCtxWithEvents = prevCtx;
                            return ctx;
                        }
                        if (eventResult == EventResult.HandledButRunDefault) {
                            currentCtxWithEvents = prevCtx;
                            return undefined;
                        }
                        if (eventResult == EventResult.NotHandledPreventDefault) {
                            res = ctx;
                        }
                    }
                }
            }
            currentCtxWithEvents = prevCtx;
        }
    }
    if (c) {
        ctx = node.ctx;
        if ((ctx.$hookFlags & hasCaptureEvents) === hasCaptureEvents) {
            var hooks = ctx.$hooks;
            var prevCtx = currentCtxWithEvents;
            currentCtxWithEvents = ctx;
            for (var i = 0, l = hooks.length; i < l; i++) {
                var h = hooks[i];
                if (h instanceof CaptureEventsHook) {
                    var m = h.events[name];
                    if (m !== undefined) {
                        const eventResult = +m.call(ctx, param);
                        if (eventResult == EventResult.HandledPreventDefault) {
                            currentCtxWithEvents = prevCtx;
                            return ctx;
                        }
                        if (eventResult == EventResult.HandledButRunDefault) {
                            currentCtxWithEvents = prevCtx;
                            return undefined;
                        }
                        if (eventResult == EventResult.NotHandledPreventDefault) {
                            res = ctx;
                        }
                    }
                }
            }
            currentCtxWithEvents = prevCtx;
        }
    }
    var ch = node.children;
    if (isArray(ch)) {
        for (var i = 0, l = ch.length; i < l; i++) {
            var res2 = broadcastCapturedEventToNode(ch[i], name, param);
            if (res2 != undefined) return res2;
        }
    }
    return res;
}

export function captureBroadcast(name, param) {
    var k = Object.keys(roots);
    for (var i = 0; i < k.length; i++) {
        var ch = roots[k[i]].n;
        if (ch != null) {
            var res = broadcastCapturedEventToNode(ch, name, param);
            if (res != null) return res;
        }
    }
    return undefined;
}

export function broadcast(name, param) {
    var res = captureBroadcast(name, param);
    if (res != null) return res;
    var k = Object.keys(roots);
    for (var i = 0; i < k.length; i++) {
        var ch = roots[k[i]].n;
        if (ch != null) {
            res = broadcastEventToNode(ch, name, param);
            if (res != null) return res;
        }
    }
    return undefined;
}

export function runMethodFrom(ctx, methodId, param) {
    var done = false;
    if (DEBUG && ctx == undefined) throw new Error("runMethodFrom ctx is undefined");
    var currentRoot = ctx.me;
    var previousRoot;
    while (currentRoot != undefined) {
        var children = currentRoot.children;
        if (isArray(children)) loopChildNodes(children);
        if (done) return true;
        var comp = currentRoot.component;
        if (comp && comp.runMethod) {
            if (callWithCurrentCtxWithEvents(() => comp.runMethod(currentCtxWithEvents, methodId, param), currentRoot.ctx)) return true;
        }
        previousRoot = currentRoot;
        currentRoot = currentRoot.parent;
    }
    function loopChildNodes(children) {
        for (var i = children.length - 1; i >= 0; i--) {
            var child = children[i];
            if (child === previousRoot) continue;
            isArray(child.children) && loopChildNodes(child.children);
            if (done) return;
            var comp = child.component;
            if (comp && comp.runMethod) {
                if (callWithCurrentCtxWithEvents(() => comp.runMethod(currentCtxWithEvents, methodId, param), child.ctx)) {
                    done = true;
                    return;
                }
            }
        }
    }
    return done;
}

export function getCurrentCtxWithEvents() {
    if (currentCtxWithEvents != undefined) return currentCtxWithEvents;
    return currentCtx;
}

export function tryRunMethod(methodId, param) {
    return runMethodFrom(getCurrentCtxWithEvents(), methodId, param);
}

export function runMethod(methodId, param) {
    if (!runMethodFrom(getCurrentCtxWithEvents(), methodId, param)) throw Error("runMethod didn't found " + methodId);
}

let lastMethodId = 0;

export function allocateMethodId() {
    return lastMethodId++;
}

function merge(f1, f2) {
    return function(...params) {
        var result = f1.apply(this, params);
        if (result) return result;
        return f2.apply(this, params);
    };
}

function mergeComponents(c1, c2) {
    let res = Object.create(c1);
    res.super = c1;
    for (var i in c2) {
        if (!(i in emptyObject)) {
            var m = c2[i];
            var origM = c1[i];
            if (i === "id") {
                res[i] = (origM != null ? origM : "") + "/" + m;
            } else if (isFunction(m) && origM != null && isFunction(origM)) {
                res[i] = merge(origM, m);
            } else {
                res[i] = m;
            }
        }
    }
    return res;
}

function overrideComponents(originalComponent, overridingComponent) {
    let res = Object.create(originalComponent);
    res.super = originalComponent;
    for (let i in overridingComponent) {
        if (!(i in emptyObject)) {
            let m = overridingComponent[i];
            let origM = originalComponent[i];
            if (i === "id") {
                res[i] = (origM != null ? origM : "") + "/" + m;
            } else {
                res[i] = m;
            }
        }
    }
    return res;
}

export function preEnhance(node, methods) {
    var comp = node.component;
    if (!comp) {
        node.component = methods;
        return node;
    }
    node.component = mergeComponents(methods, comp);
    return node;
}

export function postEnhance(node, methods) {
    var comp = node.component;
    if (!comp) {
        node.component = methods;
        return node;
    }
    node.component = mergeComponents(comp, methods);
    return node;
}

export function preventDefault(event) {
    event.preventDefault();
    event.stopPropagation();
}

function cloneNodeArray(a) {
    a = a.slice(0);
    for (var i = 0; i < a.length; i++) {
        var n = a[i];
        if (isArray(n)) {
            a[i] = cloneNodeArray(n);
        } else if (isObject(n)) {
            a[i] = cloneNode(n);
        }
    }
    return a;
}

export function cloneNode(node) {
    var r = assign({}, node);
    if (r.attrs) {
        r.attrs = assign({}, r.attrs);
    }
    var style = r.style;
    if (isObject(style) && !isFunction(style)) {
        r.style = assign({}, style);
    }
    var ch = r.children;
    if (ch) {
        if (isArray(ch)) {
            r.children = cloneNodeArray(ch);
        } else if (isObject(ch)) {
            r.children = cloneNode(ch);
        }
    }
    return r;
}

export function uptime() {
    return uptimeMs;
}

export function lastFrameDuration() {
    return lastFrameDurationMs;
}

export function frame() {
    return frameCounter;
}

export function invalidated() {
    return isInvalidated;
}

var bValue = "b$value";

var bSelectionStart = "b$selStart";

var bSelectionEnd = "b$selEnd";

var tValue = "value";

function isCheckboxLike(el) {
    var t = el.type;
    return t === "checkbox" || t === "radio";
}

function stringArrayEqual(a1, a2) {
    if (a1 === a2) return true;
    if (a1 == undefined || a2 == undefined) return false;
    var l = a1.length;
    if (l !== a2.length) return false;
    for (var j = 0; j < l; j++) {
        if (a1[j] !== a2[j]) return false;
    }
    return true;
}

function stringArrayContains(a, v) {
    if (a == undefined) return false;
    for (var j = 0; j < a.length; j++) {
        if (a[j] === v) return true;
    }
    return false;
}

function selectedArray(options) {
    var res = [];
    for (var j = 0; j < options.length; j++) {
        if (options[j].selected) res.push(options[j].value);
    }
    return res;
}

function emitOnChange(ev, target, node) {
    if (target && target.nodeName === "OPTION") {
        target = document.activeElement;
        node = deref(target);
    }
    if (!node) {
        return false;
    }
    if (node.ctx === undefined) {
        node.ctx = new BobrilCtx(undefined, node);
        node.component = emptyObject;
    }
    var ctx = node.ctx;
    var tagName = target.tagName;
    var isSelect = tagName === "SELECT";
    var isMultiSelect = isSelect && target.multiple;
    if (isMultiSelect) {
        var vs = selectedArray(target.options);
        if (!stringArrayEqual(ctx[bValue], vs)) {
            ctx[bValue] = vs;
            emitOnInput(node, vs);
        }
    } else if (isCheckboxLike(target)) {
        if (ev && ev.type === "change") {
            setTimeout(() => {
                emitOnChange(undefined, target, node);
            }, 10);
            return false;
        }
        if (target.type === "radio") {
            var radios = document.getElementsByName(target.name);
            for (var j = 0; j < radios.length; j++) {
                var radio = radios[j];
                var radioNode = deref(radio);
                if (!radioNode) continue;
                var radioCtx = radioNode.ctx;
                var vrb = radio.checked;
                if (radioCtx[bValue] !== vrb) {
                    radioCtx[bValue] = vrb;
                    emitOnInput(radioNode, vrb);
                }
            }
        } else {
            var vb = target.checked;
            if (ctx[bValue] !== vb) {
                ctx[bValue] = vb;
                emitOnInput(node, vb);
            }
        }
    } else {
        var v = target.value;
        if (ctx[bValue] !== v) {
            ctx[bValue] = v;
            emitOnInput(node, v);
        }
        let sStart = target.selectionStart;
        let sEnd = target.selectionEnd;
        let sDir = target.selectionDirection;
        let swap = false;
        let oStart = ctx[bSelectionStart];
        if (sDir == undefined) {
            if (sEnd === oStart) swap = true;
        } else if (sDir === "backward") {
            swap = true;
        }
        if (swap) {
            let s = sStart;
            sStart = sEnd;
            sEnd = s;
        }
        emitOnSelectionChange(node, sStart, sEnd);
    }
    return false;
}

function emitOnInput(node, value) {
    var prevCtx = currentCtxWithEvents;
    var ctx = node.ctx;
    var component = node.component;
    currentCtxWithEvents = ctx;
    const hasProp = node.attrs && node.attrs[bValue];
    if (isFunction(hasProp)) hasProp(value);
    const hasOnChange = component && component.onChange;
    if (isFunction(hasOnChange)) hasOnChange(ctx, value);
    currentCtxWithEvents = prevCtx;
    bubble(node, "onInput", {
        target: node,
        value
    });
}

function emitOnSelectionChange(node, start, end) {
    let c = node.component;
    let ctx = node.ctx;
    if (c && (ctx[bSelectionStart] !== start || ctx[bSelectionEnd] !== end)) {
        ctx[bSelectionStart] = start;
        ctx[bSelectionEnd] = end;
        bubble(node, "onSelectionChange", {
            target: node,
            startPosition: start,
            endPosition: end
        });
    }
}

export function select(node, start, end = start) {
    node.element.setSelectionRange(Math.min(start, end), Math.max(start, end), start > end ? "backward" : "forward");
    emitOnSelectionChange(node, start, end);
}

function emitOnMouseChange(ev, _target, _node) {
    let f = focused();
    if (f) emitOnChange(ev, f.element, f);
    return false;
}

var events = [ "input", "cut", "paste", "keydown", "keypress", "keyup", "click", "change" ];

for (var i = 0; i < events.length; i++) addEvent(events[i], 10, emitOnChange);

var mouseEvents = [ "!PointerDown", "!PointerMove", "!PointerUp", "!PointerCancel" ];

for (var i = 0; i < mouseEvents.length; i++) addEvent(mouseEvents[i], 2, emitOnMouseChange);

let currentActiveElement = undefined;

let currentFocusedNode = undefined;

let nodeStack = [];

let focusChangeRunning = false;

const focusedHookSet = new Set();

export let useIsFocused = buildUseIsHook(focusedHookSet);

function emitOnFocusChange(inFocus) {
    if (focusChangeRunning) return false;
    focusChangeRunning = true;
    while (true) {
        const newActiveElement = document.hasFocus() || inFocus ? document.activeElement : undefined;
        if (newActiveElement === currentActiveElement) break;
        currentActiveElement = newActiveElement;
        var newStack = vdomPath(currentActiveElement);
        var common = 0;
        while (common < nodeStack.length && common < newStack.length && nodeStack[common] === newStack[common]) common++;
        var i = nodeStack.length - 1;
        var n;
        var c;
        if (i >= common) {
            n = nodeStack[i];
            bubble(n, "onBlur");
            i--;
        }
        while (i >= common) {
            n = nodeStack[i];
            c = n.component;
            if (c && c.onFocusOut) c.onFocusOut(n.ctx);
            i--;
        }
        i = common;
        while (i + 1 < newStack.length) {
            n = newStack[i];
            c = n.component;
            if (c && c.onFocusIn) c.onFocusIn(n.ctx);
            i++;
        }
        if (i < newStack.length) {
            n = newStack[i];
            bubble(n, "onFocus");
        }
        nodeStack = newStack;
        currentFocusedNode = nodeStack.length == 0 ? undefined : nodeStack[nodeStack.length - 1];
        focusedHookSet.forEach(v => v.update(newStack));
    }
    focusChangeRunning = false;
    return false;
}

function emitOnFocusChangeDelayed() {
    setTimeout(() => emitOnFocusChange(false), 10);
    return false;
}

addEvent("^focus", 50, () => emitOnFocusChange(true));

addEvent("^blur", 50, emitOnFocusChangeDelayed);

export function focused() {
    return currentFocusedNode;
}

export function focus(node, backwards) {
    if (node == undefined) return false;
    if (isString(node)) return false;
    var style = node.style;
    if (style != undefined) {
        if (style.visibility === "hidden") return false;
        if (style.display === "none") return false;
    }
    var attrs = node.attrs;
    if (attrs != undefined) {
        var ti = attrs.tabindex;
        if (ti !== undefined || isNaturallyFocusable(node.tag, attrs)) {
            var el = node.element;
            el.focus();
            emitOnFocusChange(false);
            return true;
        }
    }
    var children = node.children;
    if (isArray(children)) {
        for (var i = 0; i < children.length; i++) {
            if (focus(children[backwards ? children.length - 1 - i : i], backwards)) return true;
        }
        return false;
    }
    return false;
}

var callbacks = [];

function emitOnScroll(_ev, _target, node) {
    let info = {
        node
    };
    for (var i = 0; i < callbacks.length; i++) {
        callbacks[i](info);
    }
    captureBroadcast("onScroll", info);
    return false;
}

addEvent("^scroll", 10, emitOnScroll);

export function addOnScroll(callback) {
    callbacks.push(callback);
}

export function removeOnScroll(callback) {
    for (var i = 0; i < callbacks.length; i++) {
        if (callbacks[i] === callback) {
            callbacks.splice(i, 1);
            return;
        }
    }
}

const isHtml = /^(?:html)$/i;

const isScrollOrAuto = /^(?:auto)$|^(?:scroll)$/i;

export function isScrollable(el) {
    var styles = window.getComputedStyle(el);
    var res = [ true, true ];
    if (!isHtml.test(el.nodeName)) {
        res[0] = isScrollOrAuto.test(styles.overflowX);
        res[1] = isScrollOrAuto.test(styles.overflowY);
    }
    res[0] = res[0] && el.scrollWidth > el.clientWidth;
    res[1] = res[1] && el.scrollHeight > el.clientHeight;
    return res;
}

export function getWindowScroll() {
    var left = window.pageXOffset;
    var top = window.pageYOffset;
    return [ left, top ];
}

export function nodePagePos(node) {
    let rect = getDomNode(node).getBoundingClientRect();
    let res = getWindowScroll();
    res[0] += rect.left;
    res[1] += rect.top;
    return res;
}

class CSSMatrix {
    constructor(data) {
        this.data = data;
    }
    static fromString(s) {
        var c = s.match(/matrix3?d?\(([^\)]+)\)/i)[1].split(",");
        if (c.length === 6) {
            c = [ c[0], c[1], "0", "0", c[2], c[3], "0", "0", "0", "0", "1", "0", c[4], c[5], "0", "1" ];
        }
        return new CSSMatrix([ parseFloat(c[0]), parseFloat(c[4]), parseFloat(c[8]), parseFloat(c[12]), parseFloat(c[1]), parseFloat(c[5]), parseFloat(c[9]), parseFloat(c[13]), parseFloat(c[2]), parseFloat(c[6]), parseFloat(c[10]), parseFloat(c[14]), parseFloat(c[3]), parseFloat(c[7]), parseFloat(c[11]), parseFloat(c[15]) ]);
    }
    static identity() {
        return new CSSMatrix([ 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 ]);
    }
    multiply(m) {
        var a = this.data;
        var b = m.data;
        return new CSSMatrix([ a[0] * b[0] + a[1] * b[4] + a[2] * b[8] + a[3] * b[12], a[0] * b[1] + a[1] * b[5] + a[2] * b[9] + a[3] * b[13], a[0] * b[2] + a[1] * b[6] + a[2] * b[10] + a[3] * b[14], a[0] * b[3] + a[1] * b[7] + a[2] * b[11] + a[3] * b[15], a[4] * b[0] + a[5] * b[4] + a[6] * b[8] + a[7] * b[12], a[4] * b[1] + a[5] * b[5] + a[6] * b[9] + a[7] * b[13], a[4] * b[2] + a[5] * b[6] + a[6] * b[10] + a[7] * b[14], a[4] * b[3] + a[5] * b[7] + a[6] * b[11] + a[7] * b[15], a[8] * b[0] + a[9] * b[4] + a[10] * b[8] + a[11] * b[12], a[8] * b[1] + a[9] * b[5] + a[10] * b[9] + a[11] * b[13], a[8] * b[2] + a[9] * b[6] + a[10] * b[10] + a[11] * b[14], a[8] * b[3] + a[9] * b[7] + a[10] * b[11] + a[11] * b[15], a[12] * b[0] + a[13] * b[4] + a[14] * b[8] + a[15] * b[12], a[12] * b[1] + a[13] * b[5] + a[14] * b[9] + a[15] * b[13], a[12] * b[2] + a[13] * b[6] + a[14] * b[10] + a[15] * b[14], a[12] * b[3] + a[13] * b[7] + a[14] * b[11] + a[15] * b[15] ]);
    }
    translate(tx, ty, tz) {
        var z = new CSSMatrix([ 1, 0, 0, tx, 0, 1, 0, ty, 0, 0, 1, tz, 0, 0, 0, 1 ]);
        return this.multiply(z);
    }
    inverse() {
        var m = this.data;
        var a = m[0];
        var b = m[1];
        var c = m[2];
        var d = m[4];
        var e = m[5];
        var f = m[6];
        var g = m[8];
        var h = m[9];
        var k = m[10];
        var A = e * k - f * h;
        var B = f * g - d * k;
        var C = d * h - e * g;
        var D = c * h - b * k;
        var E = a * k - c * g;
        var F = b * g - a * h;
        var G = b * f - c * e;
        var H = c * d - a * f;
        var K = a * e - b * d;
        var det = a * A + b * B + c * C;
        var X = new CSSMatrix([ A / det, D / det, G / det, 0, B / det, E / det, H / det, 0, C / det, F / det, K / det, 0, 0, 0, 0, 1 ]);
        var Y = new CSSMatrix([ 1, 0, 0, -m[3], 0, 1, 0, -m[7], 0, 0, 1, -m[11], 0, 0, 0, 1 ]);
        return X.multiply(Y);
    }
    transformPoint(x, y) {
        var m = this.data;
        return [ m[0] * x + m[1] * y + m[3], m[4] * x + m[5] * y + m[7] ];
    }
}

function getTransformationMatrix(element) {
    var identity = CSSMatrix.identity();
    var transformationMatrix = identity;
    var x = element;
    var doc = x.ownerDocument.documentElement;
    while (x != undefined && x !== doc && x.nodeType != 1) x = x.parentNode;
    while (x != undefined && x !== doc) {
        var computedStyle = window.getComputedStyle(x, undefined);
        var c = CSSMatrix.fromString((computedStyle.transform || computedStyle.OTransform || computedStyle.WebkitTransform || computedStyle.msTransform || computedStyle.MozTransform || "none").replace(/^none$/, "matrix(1,0,0,1,0,0)"));
        transformationMatrix = c.multiply(transformationMatrix);
        x = x.parentNode;
    }
    var w;
    var h;
    if ((element.nodeName + "").toLowerCase() === "svg") {
        var cs = getComputedStyle(element, undefined);
        w = parseFloat(cs.getPropertyValue("width")) || 0;
        h = parseFloat(cs.getPropertyValue("height")) || 0;
    } else {
        w = element.offsetWidth;
        h = element.offsetHeight;
    }
    var i = 4;
    var left = +Infinity;
    var top = +Infinity;
    while (--i >= 0) {
        var p = transformationMatrix.transformPoint(i === 0 || i === 1 ? 0 : w, i === 0 || i === 3 ? 0 : h);
        if (p[0] < left) {
            left = p[0];
        }
        if (p[1] < top) {
            top = p[1];
        }
    }
    var rect = element.getBoundingClientRect();
    transformationMatrix = identity.translate(rect.left - left, rect.top - top, 0).multiply(transformationMatrix);
    return transformationMatrix;
}

export function convertPointFromClientToNode(node, pageX, pageY) {
    let element = getDomNode(node);
    if (element == undefined) element = document.body;
    return getTransformationMatrix(element).inverse().transformPoint(pageX, pageY);
}

export let asset = window["bobrilBAsset"] || function(path) {
    return path;
};

export function setAsset(fn) {
    asset = fn;
}

export function withKey(content, key) {
    if (isObject(content) && !isArray(content)) {
        content.key = key;
        return content;
    }
    return {
        key,
        children: content
    };
}

export function withRef(node, ctx, name) {
    node.ref = [ ctx, name ];
    return node;
}

export function extendCfg(ctx, propertyName, value) {
    var c = ctx.me.cfg;
    if (c !== undefined) {
        c[propertyName] = value;
    } else {
        c = Object.assign({}, ctx.cfg);
        c[propertyName] = value;
        ctx.me.cfg = c;
    }
}

export function createVirtualComponent(component) {
    return (data, children) => {
        if (children !== undefined) {
            if (data == undefined) data = {};
            data.children = children;
        }
        return {
            data,
            component
        };
    };
}

export function createOverridingComponent(original, after) {
    const originalComponent = original().component;
    const overriding = overrideComponents(originalComponent, after);
    return createVirtualComponent(overriding);
}

export function createComponent(component) {
    const originalRender = component.render;
    if (originalRender) {
        component.render = function(ctx, me, oldMe) {
            me.tag = "div";
            return originalRender.call(component, ctx, me, oldMe);
        };
    } else {
        component.render = ((_ctx, me) => {
            me.tag = "div";
        });
    }
    return createVirtualComponent(component);
}

export function createDerivedComponent(original, after) {
    const originalComponent = original().component;
    const merged = mergeComponents(originalComponent, after);
    return createVirtualComponent(merged);
}

export function prop(value, onChange) {
    return val => {
        if (val !== undefined) {
            if (onChange !== undefined) onChange(val, value);
            value = val;
        }
        return value;
    };
}

export function propi(value) {
    return val => {
        if (val !== undefined) {
            value = val;
            invalidate();
        }
        return value;
    };
}

export function propa(prop) {
    return val => {
        if (val !== undefined) {
            if (typeof val === "object" && isFunction(val.then)) {
                val.then(v => {
                    prop(v);
                }, err => {
                    if (window["console"] && console.error) console.error(err);
                });
            } else {
                return prop(val);
            }
        }
        return prop();
    };
}

export function propim(value, ctx, onChange) {
    return val => {
        if (val !== undefined && !is(val, value)) {
            const oldVal = val;
            value = val;
            if (onChange !== undefined) onChange(val, oldVal);
            invalidate(ctx);
        }
        return value;
    };
}

export function debounceProp(from, delay = 500) {
    let current = from();
    let lastSet = current;
    let timer;
    function clearTimer() {
        if (timer !== undefined) {
            clearTimeout(timer);
            timer = undefined;
        }
    }
    return value => {
        if (value === undefined) {
            let origin = from();
            if (origin === lastSet) return current;
            current = origin;
            lastSet = origin;
            clearTimer();
            return origin;
        } else {
            clearTimer();
            if (current === value) {
                lastSet = value;
                from(value);
            } else {
                current = value;
                timer = setTimeout(() => {
                    lastSet = current;
                    from(current);
                    timer = undefined;
                }, delay);
            }
            return value;
        }
    };
}

export function getValue(value) {
    if (isFunction(value)) {
        return value();
    }
    return value;
}

export function emitChange(data, value) {
    if (isFunction(data.value)) {
        data.value(value);
    }
    if (data.onChange !== undefined) {
        data.onChange(value);
    }
}

export function shallowEqual(a, b) {
    if (is(a, b)) {
        return true;
    }
    if (!isObject(a) || !isObject(b)) {
        return false;
    }
    const kA = Object.keys(a);
    const kB = Object.keys(b);
    if (kA.length !== kB.length) {
        return false;
    }
    for (let i = 0; i < kA.length; i++) {
        if (!hOP.call(b, kA[i]) || !is(a[kA[i]], b[kA[i]])) {
            return false;
        }
    }
    return true;
}

const jsxFactoryCache = new Map();

function getStringPropertyDescriptors(obj) {
    var props = new Map();
    do {
        Object.getOwnPropertyNames(obj).forEach(function(prop) {
            if (!this.has(prop)) this.set(prop, Object.getOwnPropertyDescriptor(obj, prop));
        }, props);
    } while (obj = Object.getPrototypeOf(obj));
    return props;
}

export function getChildrenOfElement(node) {
    if (node.children != undefined) return node.children;
    return node.data?.children;
}

export function getPropsOfElement(node) {
    if (node.tag != undefined) {
        let res = Object.assign({}, node.attrs);
        if (node.style != undefined) res["style"] = node.style;
        if (node.className != undefined) res["className"] = node.className;
        if (node.key != undefined) res["key"] = node.key;
        if (node.ref != undefined) res["ref"] = node.ref;
        if (node.children != undefined) res["children"] = node.children;
        Object.assign(res, node.component);
        return res;
    }
    if (node.component != undefined) {
        let res = Object.assign({}, node.data);
        if (node.key != undefined) res["key"] = node.key;
        if (node.ref != undefined) res["ref"] = node.ref;
        return res;
    }
    return {};
}

export function isValidElement(value) {
    if (!isObject(value)) return false;
    return isString(value["tag"]) || isObject(value["component"]);
}

export function isComponent(what, component) {
    if (!isObject(what)) return false;
    if (isString(component)) {
        return what.tag === component;
    }
    return what.component?.src === component;
}

const jsxSimpleProps = new Set("key className component data children".split(" "));

export function createElement(name, props) {
    let children;
    const argumentsCount = arguments.length - 2;
    if (argumentsCount === 0) {} else if (argumentsCount === 1) {
        children = arguments[2];
    } else {
        children = new Array(argumentsCount);
        for (let i = 0; i < argumentsCount; i++) {
            children[i] = arguments[i + 2];
        }
    }
    if (isString(name)) {
        var res = argumentsCount === 0 ? {
            tag: name
        } : {
            tag: name,
            children
        };
        if (props == undefined) {
            return res;
        }
        var attrs;
        var component;
        for (var n in props) {
            if (!hOP.call(props, n)) continue;
            var propValue = props[n];
            if (jsxSimpleProps.has(n)) {
                res[n] = propValue;
            } else if (n === "style") {
                if (isFunction(propValue)) {
                    res[n] = propValue;
                } else {
                    style(res, propValue);
                }
            } else if (n === "ref") {
                if (isString(propValue)) {
                    assert(getCurrentCtx() != undefined);
                    res.ref = [ getCurrentCtx(), propValue ];
                } else res.ref = propValue;
            } else if (n.startsWith("on") && isFunction(propValue)) {
                if (component == undefined) {
                    component = newHashObj();
                    res.component = component;
                }
                component[n] = propValue.call.bind(propValue);
                continue;
            } else {
                if (attrs == undefined) {
                    attrs = newHashObj();
                    res.attrs = attrs;
                }
                attrs[n] = propValue;
            }
        }
        return res;
    } else {
        let res;
        let factory = jsxFactoryCache.get(name);
        if (factory === undefined) {
            factory = createFactory(name);
            jsxFactoryCache.set(name, factory);
        }
        if (argumentsCount == 0) {
            res = factory(props);
        } else {
            if (factory.length == 1) {
                if (props == undefined) props = {
                    children
                }; else props.children = children;
                res = factory(props);
            } else {
                res = factory(props, children);
            }
        }
        if (res == undefined) return res;
        if (props != undefined) {
            if (props.ref !== undefined) {
                res.ref = props.ref;
                delete props.ref;
            }
            if (props.key !== undefined) {
                res.key = props.key;
                delete props.key;
            }
        }
        return res;
    }
}

export function cloneElement(element, props) {
    if (element == undefined) return element;
    let res = Object.assign({}, element);
    if (element.tag != undefined) {
        var attrs = element.attrs;
        if (attrs != undefined) {
            attrs = Object.assign({}, attrs);
            res.attrs = attrs;
        }
        var component = element.component;
        if (component != undefined) {
            component = Object.assign({}, component);
            res.component = component;
        }
        for (var n in props) {
            if (!hOP.call(props, n)) continue;
            var propValue = props[n];
            if (jsxSimpleProps.has(n)) {
                res[n] = propValue;
            } else if (n === "style") {
                if (isFunction(propValue)) {
                    res[n] = propValue;
                } else {
                    if (isObject(res.style)) {
                        res.style = Object.assign({}, res.style);
                    }
                    style(res, propValue);
                }
            } else if (n === "ref") {
                if (isString(propValue)) {
                    assert(getCurrentCtx() != undefined);
                    res.ref = [ getCurrentCtx(), propValue ];
                } else res.ref = propValue;
            } else if (n.startsWith("on") && isFunction(propValue)) {
                if (component == undefined) {
                    component = newHashObj();
                    res.component = component;
                }
                component[n] = propValue.call.bind(propValue);
                continue;
            } else {
                if (attrs == undefined) {
                    attrs = newHashObj();
                    res.attrs = attrs;
                }
                attrs[n] = propValue;
            }
        }
    } else {
        if (props != undefined) {
            if (props.ref !== undefined) {
                res.ref = props.ref;
                delete props.ref;
            }
            if (props.key !== undefined) {
                res.key = props.key;
                delete props.key;
            }
        }
        res.data = Object.assign({}, element.data, props);
    }
    return res;
}

export const skipRender = {
    tag: "-"
};

export function Fragment(data) {
    return {
        children: data.children
    };
}

export function FragmentWithEvents(data) {
    var res = {
        children: data.children
    };
    var component;
    for (var n in data) {
        if (!hOP.call(data, n)) continue;
        var propValue = data[n];
        if (n.startsWith("on") && isFunction(propValue)) {
            component ??= newHashObj();
            res.component = component;
            component[n] = propValue.call.bind(propValue);
        }
    }
    return res;
}

export function Portal(data) {
    return {
        tag: "@",
        data: data.element ?? defaultElementRoot ?? document.body,
        children: data.children
    };
}

export var EventResult;

(function(EventResult) {
    EventResult[EventResult["NotHandled"] = 0] = "NotHandled";
    EventResult[EventResult["HandledPreventDefault"] = 1] = "HandledPreventDefault";
    EventResult[EventResult["HandledButRunDefault"] = 2] = "HandledButRunDefault";
    EventResult[EventResult["NotHandledPreventDefault"] = 3] = "NotHandledPreventDefault";
})(EventResult || (EventResult = {}));

export class Component extends BobrilCtx {
    constructor(data, me) {
        super(data, me);
    }
}

export class PureComponent extends Component {
    shouldChange(newData, oldData) {
        return !shallowEqual(newData, oldData);
    }
}

function forwardRender(m) {
    return (ctx, me, _oldMe) => {
        var res = m.call(ctx, ctx.data);
        if (res === skipRender) {
            me.tag = "-";
            return;
        }
        var resComponent = res?.component?.src;
        if (resComponent === Fragment) {
            res = res.data?.children;
        }
        me.children = res;
    };
}

function forwardInit(m) {
    return ctx => {
        m.call(ctx, ctx.data);
    };
}

function forwardShouldChange(m) {
    return (ctx, me, oldMe) => {
        return m.call(ctx, me.data, oldMe.data);
    };
}

function forwardMe(m) {
    return m.call.bind(m);
}

function combineWithForwardMe(component, name, func) {
    const existing = component[name];
    if (existing != undefined) {
        component[name] = ((ctx, me) => {
            existing(ctx, me);
            func.call(ctx, me);
        });
    } else {
        component[name] = forwardMe(func);
    }
}

const postInitDom = "postInitDom";

const postUpdateDom = "postUpdateDom";

const postUpdateDomEverytime = "postUpdateDomEverytime";

const methodsWithMeParam = [ "destroy", postInitDom, postUpdateDom, postUpdateDomEverytime ];

export function component(component, name) {
    const bobrilComponent = {};
    if (component.prototype instanceof Component) {
        const proto = component.prototype;
        const protoStatic = proto.constructor;
        bobrilComponent.id = getId(name, protoStatic);
        const protoMap = getStringPropertyDescriptors(proto);
        protoMap.forEach((descriptor, key) => {
            const value = descriptor.value;
            if (value == undefined) return;
            let set = undefined;
            if (key === "render") {
                set = forwardRender(value);
            } else if (key === "init") {
                set = forwardInit(value);
            } else if (key === "shouldChange") {
                set = forwardShouldChange(value);
            } else if (methodsWithMeParam.indexOf(key) >= 0) {
                combineWithForwardMe(bobrilComponent, key, value);
            } else if (key === "postRenderDom") {
                combineWithForwardMe(bobrilComponent, methodsWithMeParam[1], value);
                combineWithForwardMe(bobrilComponent, methodsWithMeParam[2], value);
            } else if (isFunction(value) && /^(?:canDeactivate$|on[A-Z])/.test(key)) {
                set = forwardMe(value);
            }
            if (set !== undefined) {
                bobrilComponent[key] = set;
            }
        });
        bobrilComponent.ctxClass = component;
        bobrilComponent.canActivate = protoStatic.canActivate;
    } else {
        bobrilComponent.id = getId(name, component);
        bobrilComponent.render = forwardRender(component);
    }
    bobrilComponent.src = component;
    return data => {
        return {
            data,
            component: bobrilComponent
        };
    };
}

function getId(name, classOrFunction) {
    return name || classOrFunction.id || classOrFunction.name + "_" + allocateMethodId();
}

function createFactory(comp) {
    if (comp.prototype instanceof Component) {
        return component(comp);
    } else if (comp.length == 2) {
        return comp;
    } else {
        return component(comp);
    }
}

function checkCurrentRenderCtx() {
    assert(currentCtx != undefined && hookId >= 0, "Hooks could be used only in Render method");
}

export function _getHooks() {
    checkCurrentRenderCtx();
    let hooks = currentCtx.$hooks;
    if (hooks === undefined) {
        hooks = [];
        currentCtx.$hooks = hooks;
    }
    return hooks;
}

export function _allocHook() {
    return hookId++;
}

function setStateHookFunction(value) {
    if (isFunction(value)) {
        value = value(this[0]);
    }
    if (!is(value, this[0])) {
        this[0] = value;
        invalidate(this[2]);
    }
}

function useStateIterator() {
    var i = 0;
    var self = this;
    return {
        next: function() {
            return {
                value: self[i++],
                done: false
            };
        }
    };
}

export function useState(initValue) {
    const myHookId = hookId++;
    const hooks = _getHooks();
    const ctx = currentCtx;
    let hook = hooks[myHookId];
    if (hook === undefined) {
        if (isFunction(initValue)) {
            initValue = initValue();
        }
        hook = ((...value) => {
            if (value.length == 1 && !is(value[0], hook[0])) {
                hook[0] = value[0];
                invalidate(hook[2]);
            }
            return hook[0];
        });
        hook[0] = initValue;
        hook[1] = setStateHookFunction.bind(hook);
        hook[2] = ctx;
        hook[Symbol.iterator] = useStateIterator;
        hooks[myHookId] = hook;
    }
    return hook;
}

export function useReducer(reducer, initializerArg, initializer) {}

class DepsChangeDetector {
    detectChange(deps) {
        let changed = false;
        if (deps != undefined) {
            const lastDeps = this.deps;
            if (lastDeps == undefined) {
                changed = true;
            } else {
                const depsLen = deps.length;
                if (depsLen != lastDeps.length) changed = true; else {
                    for (let i = 0; i < depsLen; i++) {
                        if (!is(deps[i], lastDeps[i])) {
                            changed = true;
                            break;
                        }
                    }
                }
            }
        } else changed = true;
        this.deps = deps;
        return changed;
    }
}

class MemoHook extends DepsChangeDetector {
    memoize(factory, deps) {
        if (this.detectChange(deps)) {
            this.current = factory();
        }
        return this.current;
    }
}

export function useMemo(factory, deps) {
    const myHookId = hookId++;
    const hooks = _getHooks();
    let hook = hooks[myHookId];
    if (hook === undefined) {
        hook = new MemoHook();
        hooks[myHookId] = hook;
    }
    return hook.memoize(factory, deps);
}

export function useCallback(callback, deps) {
    return useMemo(() => callback, deps);
}

class CommonEffectHook extends DepsChangeDetector {
    constructor(...args) {
        super(...args);
        this.shouldRun = false;
    }
    update(callback, deps) {
        this.callback = callback;
        if (this.detectChange(deps)) {
            this.doRun();
        }
    }
    doRun() {
        this.shouldRun = true;
    }
    run() {
        const c = this.callback;
        if (c != undefined) {
            this.dispose();
            this.lastDisposer = c();
        }
    }
    dispose() {
        this.callback = undefined;
        if (isFunction(this.lastDisposer)) this.lastDisposer();
        this.lastDisposer = undefined;
    }
}

class EffectHook extends CommonEffectHook {
    useEffect() {
        if (this.shouldRun) {
            this.shouldRun = false;
            this.run();
        }
    }
}

export function useEffect(callback, deps) {
    const myHookId = hookId++;
    const hooks = _getHooks();
    let hook = hooks[myHookId];
    if (hook === undefined) {
        currentCtx.$hookFlags |= hasUseEffect;
        hook = new EffectHook();
        addDisposable(currentCtx, hook);
        hooks[myHookId] = hook;
    }
    hook.update(callback, deps);
}

class LayoutEffectHook extends CommonEffectHook {
    postInitDom(ctx) {
        this[postUpdateDomEverytime].call(this, ctx);
    }
    postUpdateDomEverytime(ctx) {
        if (this.shouldRun) {
            this.shouldRun = false;
            this.run();
            if (ctx[ctxInvalidated] > frameCounter) {
                deferSyncUpdate();
            }
        }
    }
}

export function useLayoutEffect(callback, deps) {
    const myHookId = hookId++;
    const hooks = _getHooks();
    let hook = hooks[myHookId];
    if (hook === undefined) {
        currentCtx.$hookFlags |= hasPostInitDom | hasPostUpdateDomEverytime;
        hook = new LayoutEffectHook();
        addDisposable(currentCtx, hook);
        hooks[myHookId] = hook;
    }
    hook.update(callback, deps);
}

class EventsHook {}

export function useEvents(events) {
    const myHookId = hookId++;
    const hooks = _getHooks();
    let hook = hooks[myHookId];
    if (hook === undefined) {
        currentCtx.$hookFlags |= hasEvents;
        hook = new EventsHook();
        hooks[myHookId] = hook;
    } else {
        assert(hook instanceof EventsHook);
    }
    hook.events = events;
}

class CaptureEventsHook {}

export function useCaptureEvents(events) {
    const myHookId = hookId++;
    const hooks = _getHooks();
    let hook = hooks[myHookId];
    if (hook === undefined) {
        currentCtx.$hookFlags |= hasCaptureEvents;
        hook = new CaptureEventsHook();
        hooks[myHookId] = hook;
    } else {
        assert(hook instanceof CaptureEventsHook);
    }
    hook.events = events;
}

export class CommonUseIsHook {
    constructor(owner, ctx) {
        this.Value = false;
        this._owner = owner;
        this._ctx = ctx;
        owner.add(this);
        addDisposable(ctx, this);
    }
    update(path) {
        let newValue = path.indexOf(this._ctx.me) >= 0;
        if (this.Value == newValue) return;
        this.Value = newValue;
        invalidate(this._ctx);
    }
    dispose() {
        this._owner.delete(this);
    }
}

export function buildUseIsHook(owner) {
    return () => {
        const myHookId = hookId++;
        const hooks = _getHooks();
        let hook = hooks[myHookId];
        if (hook === undefined) {
            hook = new CommonUseIsHook(owner, currentCtx);
            hooks[myHookId] = hook;
        }
        return hook.Value;
    };
}

