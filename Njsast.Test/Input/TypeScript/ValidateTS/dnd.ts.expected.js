"use strict";
exports.getDnds = exports.DndEnabledOps = exports.DndOp = void 0;

exports.anyActiveDnd = anyActiveDnd;

const core_1 = require("./core");

const cssInJs_1 = require("./cssInJs");

const isFunc_1 = require("./isFunc");

const localHelpers_1 = require("./localHelpers");

const media_1 = require("./media");

const mouseEvents_1 = require("./mouseEvents");

var DndOp;

(function(DndOp) {
    DndOp[DndOp["None"] = 0] = "None";
    DndOp[DndOp["Link"] = 1] = "Link";
    DndOp[DndOp["Copy"] = 2] = "Copy";
    DndOp[DndOp["Move"] = 3] = "Move";
})(DndOp || (exports.DndOp = DndOp = {}));

const dropEffectsAllowedTable = [ "none", "link", "copy", "move" ];

var DndEnabledOps;

(function(DndEnabledOps) {
    DndEnabledOps[DndEnabledOps["None"] = 0] = "None";
    DndEnabledOps[DndEnabledOps["Link"] = 1] = "Link";
    DndEnabledOps[DndEnabledOps["Copy"] = 2] = "Copy";
    DndEnabledOps[DndEnabledOps["LinkCopy"] = 3] = "LinkCopy";
    DndEnabledOps[DndEnabledOps["Move"] = 4] = "Move";
    DndEnabledOps[DndEnabledOps["MoveLink"] = 5] = "MoveLink";
    DndEnabledOps[DndEnabledOps["MoveCopy"] = 6] = "MoveCopy";
    DndEnabledOps[DndEnabledOps["MoveCopyLink"] = 7] = "MoveCopyLink";
})(DndEnabledOps || (exports.DndEnabledOps = DndEnabledOps = {}));

var effectAllowedTable = [ "none", "link", "copy", "copyLink", "move", "linkMove", "copyMove", "all" ];

var lastDndId = 0;

var dnds = [];

var systemDnd = null;

var rootId = null;

var DndCtx = function(pointerId) {
    this.id = ++lastDndId;
    this.pointerid = pointerId;
    this.enabledOperations = DndEnabledOps.MoveCopyLink;
    this.operation = DndOp.None;
    this.started = false;
    this.beforeDrag = true;
    this.local = true;
    this.system = false;
    this.ended = false;
    this.cursor = null;
    this.overNode = undefined;
    this.targetCtx = null;
    this.dragView = undefined;
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
    this.shift = false;
    this.ctrl = false;
    this.alt = false;
    this.meta = false;
    this.data = localHelpers_1.newHashObj();
    if (pointerId >= 0) pointer2Dnd[pointerId] = this;
    dnds.push(this);
};

const draggingStyle = "b-dragging";

let lazyDefineStyle = true;

function lazyCreateRoot() {
    if (rootId == undefined) {
        if (lazyDefineStyle) {
            cssInJs_1.selectorStyleDef("html." + draggingStyle + " *", {
                cursor: "inherit !important",
                userSelect: "none !important"
            });
            lazyDefineStyle = false;
        }
        var dd = document.documentElement;
        dd.classList.add(draggingStyle);
        rootId = core_1.addRoot(dndRootFactory);
    }
}

var DndComp = {
    render(ctx, me) {
        var dnd = ctx.data;
        me.tag = "div";
        me.style = {
            position: "absolute",
            left: dnd.x,
            top: dnd.y
        };
        me.children = dnd.dragView(dnd);
    }
};

function currentCursor() {
    let cursor = "no-drop";
    if (dnds.length !== 0) {
        let dnd = dnds[0];
        if (dnd.beforeDrag) return "";
        if (dnd.cursor != null) return dnd.cursor;
        if (dnd.system) return "";
        switch (dnd.operation) {
          case DndOp.Move:
            cursor = "move";
            break;

          case DndOp.Link:
            cursor = "alias";
            break;

          case DndOp.Copy:
            cursor = "copy";
            break;
        }
    }
    return cursor;
}

var DndRootComp = {
    render(_ctx, me) {
        var res = [];
        for (var i = 0; i < dnds.length; i++) {
            var dnd = dnds[i];
            if (dnd.beforeDrag) continue;
            if (dnd.dragView != null && (dnd.x != 0 || dnd.y != 0)) {
                res.push({
                    key: "" + dnd.id,
                    data: dnd,
                    component: DndComp
                });
            }
        }
        me.tag = "div";
        me.style = {
            position: "fixed",
            zIndex: 1e9,
            pointerEvents: "none",
            userSelect: "none",
            left: 0,
            top: 0,
            right: 0,
            bottom: 0
        };
        let dds = document.documentElement.style;
        let cur = currentCursor();
        if (cur) {
            if (dds.cursor !== cur) dds.setProperty("cursor", cur, "important");
        } else {
            dds.setProperty("cursor", "");
        }
        me.children = res;
    },
    onDrag(ctx) {
        core_1.invalidate(ctx);
        return false;
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
};

dndProto.setDragNodeView = function(view) {
    this.dragView = view;
};

dndProto.addData = function(type, data) {
    this.data[type] = data;
    return true;
};

dndProto.listData = function() {
    return Object.keys(this.data);
};

dndProto.hasData = function(type) {
    return this.data[type] !== undefined;
};

dndProto.getData = function(type) {
    return this.data[type];
};

dndProto.setEnabledOps = function(ops) {
    this.enabledOperations = ops;
};

dndProto.cancelDnd = function() {
    dndMoved(undefined, this);
    this.destroy();
};

dndProto.destroy = function() {
    this.ended = true;
    if (this.started) core_1.broadcast("onDragEnd", this);
    delete pointer2Dnd[this.pointerid];
    for (var i = 0; i < dnds.length; i++) {
        if (dnds[i] === this) {
            dnds.splice(i, 1);
            break;
        }
    }
    if (systemDnd === this) {
        systemDnd = null;
    }
    if (dnds.length === 0 && rootId != null) {
        core_1.removeRoot(rootId);
        rootId = null;
        var dd = document.documentElement;
        dd.classList.remove(draggingStyle);
        dd.style.setProperty("cursor", "");
    }
};

var pointer2Dnd = localHelpers_1.newHashObj();

function handlePointerDown(ev, _target, node) {
    var dnd = pointer2Dnd[ev.id];
    if (dnd) {
        dnd.cancelDnd();
    }
    if (ev.button <= 1) {
        dnd = new DndCtx(ev.id);
        dnd.startX = ev.x;
        dnd.startY = ev.y;
        dnd.lastX = ev.x;
        dnd.lastY = ev.y;
        dnd.overNode = node;
        updateDndFromPointerEvent(dnd, ev);
        var sourceCtx = core_1.bubble(node, "onDragStart", dnd);
        if (sourceCtx) {
            var htmlNode = core_1.getDomNode(sourceCtx.me);
            if (htmlNode == undefined) {
                dnd.destroy();
                return false;
            }
            dnd.started = true;
            var boundFn = htmlNode.getBoundingClientRect;
            if (boundFn) {
                var rect = boundFn.call(htmlNode);
                dnd.deltaX = rect.left - ev.x;
                dnd.deltaY = rect.top - ev.y;
            }
            if (dnd.distanceToStart <= 0) {
                dnd.beforeDrag = false;
                dndMoved(node, dnd);
            }
            lazyCreateRoot();
        } else {
            dnd.destroy();
        }
    }
    return false;
}

function dndMoved(node, dnd) {
    dnd.overNode = node;
    dnd.targetCtx = core_1.bubble(node, "onDragOver", dnd);
    if (dnd.targetCtx == undefined) {
        dnd.operation = DndOp.None;
    }
    core_1.broadcast("onDrag", dnd);
}

function updateDndFromPointerEvent(dnd, ev) {
    dnd.shift = ev.shift;
    dnd.ctrl = ev.ctrl;
    dnd.alt = ev.alt;
    dnd.meta = ev.meta;
    dnd.x = ev.x;
    dnd.y = ev.y;
}

function handlePointerMove(ev, _target, node) {
    var dnd = pointer2Dnd[ev.id];
    if (!dnd) return false;
    dnd.totalX += Math.abs(ev.x - dnd.lastX);
    dnd.totalY += Math.abs(ev.y - dnd.lastY);
    if (dnd.beforeDrag) {
        if (dnd.totalX + dnd.totalY <= dnd.distanceToStart) {
            dnd.lastX = ev.x;
            dnd.lastY = ev.y;
            return false;
        }
        dnd.beforeDrag = false;
    }
    updateDndFromPointerEvent(dnd, ev);
    dndMoved(node, dnd);
    dnd.lastX = ev.x;
    dnd.lastY = ev.y;
    return true;
}

function handlePointerUp(ev, _target, node) {
    var dnd = pointer2Dnd[ev.id];
    if (!dnd) return false;
    if (!dnd.beforeDrag) {
        updateDndFromPointerEvent(dnd, ev);
        dndMoved(node, dnd);
        var t = dnd.targetCtx;
        if (t && core_1.bubble(t.me, "onDrop", dnd)) {
            dnd.destroy();
        } else {
            dnd.cancelDnd();
        }
        mouseEvents_1.ignoreClick(ev.x, ev.y);
        return true;
    }
    dnd.destroy();
    return false;
}

function handlePointerCancel(ev, _target, _node) {
    var dnd = pointer2Dnd[ev.id];
    if (!dnd) return false;
    if (dnd.system) return false;
    if (!dnd.beforeDrag) {
        dnd.cancelDnd();
    } else {
        dnd.destroy();
    }
    return false;
}

function updateFromNative(dnd, ev) {
    dnd.shift = ev.shiftKey;
    dnd.ctrl = ev.ctrlKey;
    dnd.alt = ev.altKey;
    dnd.meta = ev.metaKey;
    dnd.x = ev.clientX;
    dnd.y = ev.clientY;
    dnd.totalX += Math.abs(dnd.x - dnd.lastX);
    dnd.totalY += Math.abs(dnd.y - dnd.lastY);
    var node = mouseEvents_1.nodeOnPoint(dnd.x, dnd.y);
    dndMoved(node, dnd);
    dnd.lastX = dnd.x;
    dnd.lastY = dnd.y;
}

function handleDragStart(ev, _target, node) {
    var dnd = systemDnd;
    if (dnd != null) {
        dnd.destroy();
    }
    var activePointerIds = Object.keys(pointer2Dnd);
    if (activePointerIds.length > 0) {
        dnd = pointer2Dnd[activePointerIds[0]];
        dnd.system = true;
        systemDnd = dnd;
    } else {
        var startX = ev.clientX, startY = ev.clientY;
        dnd = new DndCtx(-1);
        dnd.system = true;
        systemDnd = dnd;
        dnd.x = startX;
        dnd.y = startY;
        dnd.lastX = startX;
        dnd.lastY = startY;
        dnd.startX = startX;
        dnd.startY = startY;
        var sourceCtx = core_1.bubble(node, "onDragStart", dnd);
        if (sourceCtx) {
            var htmlNode = core_1.getDomNode(sourceCtx.me);
            if (htmlNode == undefined) {
                dnd.destroy();
                return false;
            }
            dnd.started = true;
            var boundFn = htmlNode.getBoundingClientRect;
            if (boundFn) {
                var rect = boundFn.call(htmlNode);
                dnd.deltaX = rect.left - startX;
                dnd.deltaY = rect.top - startY;
            }
            lazyCreateRoot();
        } else {
            dnd.destroy();
            return false;
        }
    }
    dnd.beforeDrag = false;
    var eff = effectAllowedTable[dnd.enabledOperations];
    var dt = ev.dataTransfer;
    dt.effectAllowed = eff;
    if (dt.setDragImage) {
        var div = document.createElement("div");
        div.style.pointerEvents = "none";
        dt.setDragImage(div, 0, 0);
    } else {
        var style = ev.target.style;
        var opacityBackup = style.opacity;
        var widthBackup = style.width;
        var heightBackup = style.height;
        var paddingBackup = style.padding;
        style.opacity = "0";
        style.width = "0";
        style.height = "0";
        style.padding = "0";
        setTimeout(() => {
            style.opacity = opacityBackup;
            style.width = widthBackup;
            style.height = heightBackup;
            style.padding = paddingBackup;
        }, 0);
    }
    var data = dnd.data;
    var dataKeys = Object.keys(data);
    for (var i = 0; i < dataKeys.length; i++) {
        try {
            var k = dataKeys[i];
            var d = data[k];
            if (!isFunc_1.isString(d)) d = JSON.stringify(d);
            ev.dataTransfer.setData(k, d);
        } catch (e) {
            if (DEBUG) if (window.console) console.log("Cannot set dnd data to " + dataKeys[i]);
        }
    }
    updateFromNative(dnd, ev);
    return false;
}

function setDropEffect(ev, op) {
    ev.dataTransfer.dropEffect = dropEffectsAllowedTable[op];
}

function handleDragOver(ev, _target, _node) {
    var dnd = systemDnd;
    if (dnd == undefined) {
        dnd = new DndCtx(-1);
        dnd.system = true;
        systemDnd = dnd;
        dnd.x = ev.clientX;
        dnd.y = ev.clientY;
        dnd.startX = dnd.x;
        dnd.startY = dnd.y;
        dnd.local = false;
        var dt = ev.dataTransfer;
        var eff = 0;
        var effectAllowed = undefined;
        try {
            effectAllowed = dt.effectAllowed;
        } catch (e) {}
        for (;eff < 7; eff++) {
            if (effectAllowedTable[eff] === effectAllowed) break;
        }
        dnd.enabledOperations = eff;
        var dtTypes = dt.types;
        if (dtTypes) {
            for (var i = 0; i < dtTypes.length; i++) {
                var tt = dtTypes[i];
                if (tt === "text/plain") tt = "Text"; else if (tt === "text/uri-list") tt = "Url";
                dnd.data[tt] = null;
            }
        } else {
            if (dt.getData("Text") !== undefined) dnd.data["Text"] = null;
        }
    }
    updateFromNative(dnd, ev);
    setDropEffect(ev, dnd.operation);
    if (dnd.operation != DndOp.None) {
        core_1.preventDefault(ev);
        return true;
    }
    return false;
}

function handleDrag(ev, _target, _node) {
    var x = ev.clientX;
    var y = ev.clientY;
    var m = media_1.getMedia();
    if (systemDnd != null && (x === 0 && y === 0 || x < 0 || y < 0 || x >= m.width || y >= m.height)) {
        systemDnd.x = 0;
        systemDnd.y = 0;
        systemDnd.operation = DndOp.None;
        core_1.broadcast("onDrag", systemDnd);
    }
    return true;
}

function handleDragEnd(_ev, _target, _node) {
    if (systemDnd != null) {
        systemDnd.destroy();
    }
    return false;
}

function handleDrop(ev, _target, _node) {
    var dnd = systemDnd;
    if (dnd == undefined) return false;
    dnd.x = ev.clientX;
    dnd.y = ev.clientY;
    if (!dnd.local) {
        var dataKeys = Object.keys(dnd.data);
        var dt = ev.dataTransfer;
        for (let i = 0; i < dataKeys.length; i++) {
            var k = dataKeys[i];
            var d;
            if (k === "Files") {
                d = [].slice.call(dt.files, 0);
            } else {
                d = dt.getData(k);
            }
            dnd.data[k] = d;
        }
    }
    updateFromNative(dnd, ev);
    var t = dnd.targetCtx;
    if (t && core_1.bubble(t.me, "onDrop", dnd)) {
        setDropEffect(ev, dnd.operation);
        dnd.destroy();
        core_1.preventDefault(ev);
    } else {
        dnd.cancelDnd();
    }
    return true;
}

function justPreventDefault(ev, _target, _node) {
    core_1.preventDefault(ev);
    return true;
}

function handleDndSelectStart(ev, _target, _node) {
    if (dnds.length === 0) return false;
    core_1.preventDefault(ev);
    return true;
}

function anyActiveDnd() {
    for (let i = 0; i < dnds.length; i++) {
        let dnd = dnds[i];
        if (dnd.beforeDrag) continue;
        return dnd;
    }
    return undefined;
}

core_1.addEvent("!PointerDown", 4, handlePointerDown);

core_1.addEvent("!PointerMove", 4, handlePointerMove);

core_1.addEvent(mouseEvents_1.pointerRevalidateEventName, 4, handlePointerMove);

core_1.addEvent("!PointerUp", 4, handlePointerUp);

core_1.addEvent("!PointerCancel", 4, handlePointerCancel);

core_1.addEvent("selectstart", 4, handleDndSelectStart);

core_1.addEvent("dragstart", 5, handleDragStart);

core_1.addEvent("dragover", 5, handleDragOver);

core_1.addEvent("dragend", 5, handleDragEnd);

core_1.addEvent("drag", 5, handleDrag);

core_1.addEvent("drop", 5, handleDrop);

core_1.addEvent("dragenter", 5, justPreventDefault);

core_1.addEvent("dragleave", 5, justPreventDefault);

const getDnds = () => dnds;

exports.getDnds = getDnds;

