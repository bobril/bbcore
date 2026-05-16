import { IBobrilCacheChildren, IBobrilNode } from "./core";

import { noop } from "./localHelpers";

export var RenderPhase;

(function(RenderPhase) {
    RenderPhase[RenderPhase["Create"] = 0] = "Create";
    RenderPhase[RenderPhase["Update"] = 1] = "Update";
    RenderPhase[RenderPhase["LocalUpdate"] = 2] = "LocalUpdate";
    RenderPhase[RenderPhase["Destroy"] = 3] = "Destroy";
})(RenderPhase || (RenderPhase = {}));

export var beforeRenderCallback = noop;

export var beforeFrameCallback = noop;

export var reallyBeforeFrameCallback = noop;

export var afterFrameCallback = noop;

export function setBeforeRender(callback) {
    var res = beforeRenderCallback;
    beforeRenderCallback = callback;
    return res;
}

export function setBeforeFrame(callback) {
    var res = beforeFrameCallback;
    beforeFrameCallback = callback;
    return res;
}

export function setReallyBeforeFrame(callback) {
    var res = reallyBeforeFrameCallback;
    reallyBeforeFrameCallback = callback;
    return res;
}

export function setAfterFrame(callback) {
    var res = afterFrameCallback;
    afterFrameCallback = callback;
    return res;
}

