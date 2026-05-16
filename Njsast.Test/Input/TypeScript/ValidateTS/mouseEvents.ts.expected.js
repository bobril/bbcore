import { addEvent, bubble, callWithCurrentCtxWithEvents, deref, emitEvent, EventNames, IBobrilCacheNode, IBobrilComponent, IEventParam, ieVersion, now, preventDefault, CommonUseIsHook, buildUseIsHook } from "./core";

import { isBoolean } from "./isFunc";

import { newHashObj } from "./localHelpers";

export var BobrilPointerType;

(function(BobrilPointerType) {
    BobrilPointerType[BobrilPointerType["Mouse"] = 0] = "Mouse";
    BobrilPointerType[BobrilPointerType["Touch"] = 1] = "Touch";
    BobrilPointerType[BobrilPointerType["Pen"] = 2] = "Pen";
})(BobrilPointerType || (BobrilPointerType = {}));

const MoveOverIsNotTap = 13;

const TapShouldBeShorterThanMs = 750;

const MaxBustDelay = 500;

const MaxBustDelayForIE = 800;

const BustDistance = 50;

let ownerCtx = null;

let invokingOwner;

const onClickText = "onClick";

const onDoubleClickText = "onDoubleClick";

export function isMouseOwner(ctx) {
    return ownerCtx === ctx;
}

export function isMouseOwnerEvent() {
    return invokingOwner;
}

export function registerMouseOwner(ctx) {
    ownerCtx = ctx;
}

export function releaseMouseOwner() {
    ownerCtx = null;
}

function invokeMouseOwner(handlerName, param) {
    if (ownerCtx == undefined) {
        return false;
    }
    var c = ownerCtx.me.component;
    var handler = c[handlerName];
    if (!handler) {
        return false;
    }
    invokingOwner = true;
    var stop = callWithCurrentCtxWithEvents(() => handler.call(c, ownerCtx, param), ownerCtx);
    invokingOwner = false;
    return stop;
}

function addEvent5(name, callback) {
    addEvent(name, 5, callback);
}

var pointersEventNames = [ "PointerDown", "PointerMove", "PointerUp", "PointerCancel" ];

var i;

function type2Bobril(t) {
    if (t === "mouse" || t === 4) return BobrilPointerType.Mouse;
    if (t === "pen" || t === 3) return BobrilPointerType.Pen;
    return BobrilPointerType.Touch;
}

function buildHandlerPointer(name) {
    return function handlePointerDown(ev, target, node) {
        target = ev.target;
        node = deref(target);
        let button = ev.button + 1;
        let type = type2Bobril(ev.pointerType);
        let buttons = ev.buttons;
        if (button === 0 && type === BobrilPointerType.Mouse && buttons) {
            button = 1;
            while (!(buttons & 1)) {
                buttons = buttons >> 1;
                button++;
            }
        }
        var param = {
            target: node,
            id: ev.pointerId,
            cancelable: normalizeCancelable(ev),
            type,
            x: ev.clientX,
            y: ev.clientY,
            button,
            shift: ev.shiftKey,
            ctrl: ev.ctrlKey,
            alt: ev.altKey,
            meta: ev.metaKey || false,
            count: ev.detail
        };
        if (emitEvent("!" + name, param, target, node)) {
            preventDefault(ev);
            return true;
        }
        return false;
    };
}

function buildHandlerTouch(name) {
    return function handlePointerDown(ev, target, node) {
        var preventDef = false;
        for (var i = 0; i < ev.changedTouches.length; i++) {
            var t = ev.changedTouches[i];
            target = t.target;
            node = deref(target);
            var param = {
                target: node,
                id: t.identifier + 2,
                cancelable: normalizeCancelable(ev),
                type: BobrilPointerType.Touch,
                x: t.clientX,
                y: t.clientY,
                button: 1,
                shift: ev.shiftKey,
                ctrl: ev.ctrlKey,
                alt: ev.altKey,
                meta: ev.metaKey || false,
                count: ev.detail
            };
            if (emitEvent("!" + name, param, target, node)) preventDef = true;
        }
        if (preventDef) {
            preventDefault(ev);
            return true;
        }
        return false;
    };
}

function buildHandlerMouse(name) {
    return function handlePointer(ev, target, node) {
        target = ev.target;
        node = deref(target);
        var param = {
            target: node,
            id: 1,
            type: BobrilPointerType.Mouse,
            cancelable: normalizeCancelable(ev),
            x: ev.clientX,
            y: ev.clientY,
            button: decodeButton(ev),
            shift: ev.shiftKey,
            ctrl: ev.ctrlKey,
            alt: ev.altKey,
            meta: ev.metaKey || false,
            count: ev.detail
        };
        if (emitEvent("!" + name, param, target, node)) {
            preventDefault(ev);
            return true;
        }
        return false;
    };
}

function listenMouse() {
    addEvent5("mousedown", buildHandlerMouse(pointersEventNames[0]));
    addEvent5("mousemove", buildHandlerMouse(pointersEventNames[1]));
    addEvent5("mouseup", buildHandlerMouse(pointersEventNames[2]));
}

if (window.ontouchstart !== undefined) {
    addEvent5("touchstart", buildHandlerTouch(pointersEventNames[0]));
    addEvent5("touchmove", buildHandlerTouch(pointersEventNames[1]));
    addEvent5("touchend", buildHandlerTouch(pointersEventNames[2]));
    addEvent5("touchcancel", buildHandlerTouch(pointersEventNames[3]));
    listenMouse();
} else if (window.onpointerdown !== undefined) {
    for (i = 0; i < 4; i++) {
        var name = pointersEventNames[i];
        addEvent5(name.toLowerCase(), buildHandlerPointer(name));
    }
} else {
    listenMouse();
}

for (var j = 0; j < 4; j++) {
    (name => {
        var onName = "on" + name;
        addEvent("!" + name, 50, (ev, _target, node) => {
            return invokeMouseOwner(onName, ev) || bubble(node, onName, ev) != undefined;
        });
    })(pointersEventNames[j]);
}

var pointersDown = newHashObj();

var toBust = [];

var firstPointerDown = -1;

var firstPointerDownTime = 0;

var firstPointerDownX = 0;

var firstPointerDownY = 0;

var tapCanceled = false;

var lastMouseEv;

function diffLess(n1, n2, diff) {
    return Math.abs(n1 - n2) < diff;
}

var prevMousePath = [];

export const pointerRevalidateEventName = "!PointerRevalidate";

export function revalidateMouseIn() {
    if (lastMouseEv) {
        emitEvent(pointerRevalidateEventName, lastMouseEv, undefined, lastMouseEv.target);
    }
}

addEvent(pointerRevalidateEventName, 3, mouseEnterAndLeave);

function vdomPathFromCacheNode(n) {
    var res = [];
    while (n != undefined) {
        res.push(n);
        n = n.parent;
    }
    return res.reverse();
}

const mouseOverHookSet = new Set();

export let useIsMouseOver = buildUseIsHook(mouseOverHookSet);

function mouseEnterAndLeave(ev) {
    lastMouseEv = ev;
    var node = ev.target;
    var toPath = vdomPathFromCacheNode(node);
    mouseOverHookSet.forEach(v => v.update(toPath));
    bubble(node, "onMouseOver", ev);
    var common = 0;
    while (common < prevMousePath.length && common < toPath.length && prevMousePath[common] === toPath[common]) common++;
    var n;
    var c;
    var i = prevMousePath.length;
    if (i > 0 && (i > common || i != toPath.length)) {
        n = prevMousePath[i - 1];
        if (n) {
            c = n.component;
            if (c && c.onMouseOut) c.onMouseOut(n.ctx, ev);
        }
    }
    while (i > common) {
        i--;
        n = prevMousePath[i];
        if (n) {
            c = n.component;
            if (c && c.onMouseLeave) c.onMouseLeave(n.ctx, ev);
        }
    }
    while (i < toPath.length) {
        n = toPath[i];
        if (n) {
            c = n.component;
            if (c && c.onMouseEnter) c.onMouseEnter(n.ctx, ev);
        }
        i++;
    }
    prevMousePath = toPath;
    if (i > 0 && (i > common || i != prevMousePath.length)) {
        n = prevMousePath[i - 1];
        if (n) {
            c = n.component;
            if (c && c.onMouseIn) c.onMouseIn(n.ctx, ev);
        }
    }
    return false;
}

function noPointersDown() {
    return Object.keys(pointersDown).length === 0;
}

function bustingPointerDown(ev, _target, _node) {
    if (firstPointerDown === -1 && noPointersDown()) {
        firstPointerDown = ev.id;
        firstPointerDownTime = now();
        firstPointerDownX = ev.x;
        firstPointerDownY = ev.y;
        tapCanceled = false;
        mouseEnterAndLeave(ev);
    }
    pointersDown[ev.id] = ev.type;
    if (firstPointerDown !== ev.id) {
        tapCanceled = true;
    }
    return false;
}

function bustingPointerMove(ev, target, node) {
    if (ev.type === BobrilPointerType.Mouse && ev.button === 0 && pointersDown[ev.id] != null) {
        ev.button = 1;
        emitEvent("!PointerUp", ev, target, node);
        ev.button = 0;
    }
    if (firstPointerDown === ev.id) {
        mouseEnterAndLeave(ev);
        if (!diffLess(firstPointerDownX, ev.x, MoveOverIsNotTap) || !diffLess(firstPointerDownY, ev.y, MoveOverIsNotTap)) tapCanceled = true;
    } else if (noPointersDown()) {
        mouseEnterAndLeave(ev);
    }
    return false;
}

let clickingSpreeStart = 0;

let clickingSpreeCount = 0;

function shouldPreventClickingSpree(clickCount) {
    if (clickingSpreeCount == 0) return false;
    let n = now();
    if (n < clickingSpreeStart + 1e3 && clickCount >= clickingSpreeCount) {
        clickingSpreeStart = n;
        clickingSpreeCount = clickCount;
        return true;
    }
    clickingSpreeCount = 0;
    return false;
}

export function preventClickingSpree() {
    clickingSpreeCount = 2;
    clickingSpreeStart = now();
}

function bustingPointerUp(ev, target, node) {
    delete pointersDown[ev.id];
    if (firstPointerDown == ev.id) {
        mouseEnterAndLeave(ev);
        firstPointerDown = -1;
        if (ev.type == BobrilPointerType.Touch && !tapCanceled) {
            if (now() - firstPointerDownTime < TapShouldBeShorterThanMs) {
                emitEvent("!PointerCancel", ev, target, node);
                shouldPreventClickingSpree(1);
                var handled = invokeMouseOwner(onClickText, ev) || bubble(node, onClickText, ev) != null;
                var delay = ieVersion() ? MaxBustDelayForIE : MaxBustDelay;
                toBust.push([ ev.x, ev.y, now() + delay, handled ? 1 : 0 ]);
                return handled;
            }
        } else if (tapCanceled) {
            ignoreClick(ev.x, ev.y);
        }
    }
    return false;
}

function bustingPointerCancel(ev, _target, _node) {
    delete pointersDown[ev.id];
    if (firstPointerDown == ev.id) {
        firstPointerDown = -1;
    }
    return false;
}

function bustingClick(ev, _target, _node) {
    var n = now();
    for (var i = 0; i < toBust.length; i++) {
        var j = toBust[i];
        if (j[2] < n) {
            toBust.splice(i, 1);
            i--;
            continue;
        }
        if (diffLess(j[0], ev.clientX, BustDistance) && diffLess(j[1], ev.clientY, BustDistance)) {
            toBust.splice(i, 1);
            if (j[3]) preventDefault(ev);
            return true;
        }
    }
    return false;
}

var bustingEventNames = [ "!PointerDown", "!PointerMove", "!PointerUp", "!PointerCancel", "click" ];

var bustingEventHandlers = [ bustingPointerDown, bustingPointerMove, bustingPointerUp, bustingPointerCancel, bustingClick ];

for (var i = 0; i < 5; i++) {
    addEvent(bustingEventNames[i], 3, bustingEventHandlers[i]);
}

function createHandlerMouse(handlerName) {
    return (ev, _target, node) => {
        if (firstPointerDown != ev.id && !noPointersDown()) return false;
        if (invokeMouseOwner(handlerName, ev) || bubble(node, handlerName, ev)) {
            return true;
        }
        return false;
    };
}

var mouseHandlerNames = [ "Down", "Move", "Up", "Up" ];

for (var i = 0; i < 4; i++) {
    addEvent(bustingEventNames[i], 80, createHandlerMouse("onMouse" + mouseHandlerNames[i]));
}

function decodeButton(ev) {
    return ev.which || ev.button;
}

function normalizeCancelable(ev) {
    var c = ev.cancelable;
    return !isBoolean(c) || c;
}

function createHandler(handlerName, allButtons) {
    return (ev, _target, node) => {
        let button = decodeButton(ev) || 1;
        if (!allButtons && button !== 1) return false;
        let param = {
            target: node,
            x: ev.clientX,
            y: ev.clientY,
            button,
            cancelable: normalizeCancelable(ev),
            shift: ev.shiftKey,
            ctrl: ev.ctrlKey,
            alt: ev.altKey,
            meta: ev.metaKey || false,
            count: ev.detail || 1
        };
        if (handlerName == onDoubleClickText) param.count = 2;
        if (shouldPreventClickingSpree(param.count) || invokeMouseOwner(handlerName, param) || bubble(node, handlerName, param)) {
            preventDefault(ev);
            return true;
        }
        return false;
    };
}

export function nodeOnPoint(x, y) {
    return deref(document.elementFromPoint(x, y));
}

addEvent5("click", createHandler(onClickText));

addEvent5("dblclick", createHandler(onDoubleClickText));

addEvent5("contextmenu", createHandler("onContextMenu", true));

function handleMouseWheel(ev, _target, node) {
    let button = ev.button + 1;
    let buttons = ev.buttons;
    if (button === 0 && buttons) {
        button = 1;
        while (!(buttons & 1)) {
            buttons = buttons >> 1;
            button++;
        }
    }
    let dx = ev.deltaX;
    let dy = ev.deltaY;
    var param = {
        target: node,
        dx,
        dy,
        x: ev.clientX,
        y: ev.clientY,
        cancelable: normalizeCancelable(ev),
        button,
        shift: ev.shiftKey,
        ctrl: ev.ctrlKey,
        alt: ev.altKey,
        meta: ev.metaKey || false,
        count: ev.detail
    };
    if (invokeMouseOwner("onMouseWheel", param) || bubble(node, "onMouseWheel", param)) {
        preventDefault(ev);
        return true;
    }
    return false;
}

addEvent5("wheel", handleMouseWheel);

export const pointersDownCount = () => Object.keys(pointersDown).length;

export const firstPointerDownId = () => firstPointerDown;

export const ignoreClick = (x, y) => {
    var delay = ieVersion() ? MaxBustDelayForIE : MaxBustDelay;
    toBust.push([ x, y, now() + delay, 1 ]);
};

let lastInteractionWasKeyboard = false;

const inputTypesWithPermanentFocusVisible = /te(l|xt)|search|url|email|password|number|month|week|(date)?(time)?(-local)?/i;

function hasAlwaysFocusVisible(element) {
    if (element == null) {
        return false;
    }
    if (element.tagName == "INPUT" && inputTypesWithPermanentFocusVisible.test(element.type) && !element.readOnly) {
        return true;
    }
    if (element.tagName == "TEXTAREA" && !element.readOnly) {
        return true;
    }
    return element.isContentEditable;
}

addEvent(bustingEventNames[0], 2, () => {
    lastInteractionWasKeyboard = false;
    return false;
});

addEvent("keydown", 2, ev => {
    if (!ev.metaKey && !ev.altKey && !ev.ctrlKey) {
        lastInteractionWasKeyboard = true;
    }
    return false;
});

export function shouldBeFocusVisible() {
    return lastInteractionWasKeyboard || hasAlwaysFocusVisible(document.activeElement);
}

