"use strict";
exports.afterFrameCallback = exports.reallyBeforeFrameCallback = exports.beforeFrameCallback = exports.beforeRenderCallback = exports.RenderPhase = void 0;

exports.setBeforeRender = setBeforeRender;

exports.setBeforeFrame = setBeforeFrame;

exports.setReallyBeforeFrame = setReallyBeforeFrame;

exports.setAfterFrame = setAfterFrame;

const localHelpers_1 = require("./localHelpers");

var RenderPhase;

(function(RenderPhase) {
    RenderPhase[RenderPhase["Create"] = 0] = "Create";
    RenderPhase[RenderPhase["Update"] = 1] = "Update";
    RenderPhase[RenderPhase["LocalUpdate"] = 2] = "LocalUpdate";
    RenderPhase[RenderPhase["Destroy"] = 3] = "Destroy";
})(RenderPhase || (exports.RenderPhase = RenderPhase = {}));

exports.beforeRenderCallback = localHelpers_1.noop;

exports.beforeFrameCallback = localHelpers_1.noop;

exports.reallyBeforeFrameCallback = localHelpers_1.noop;

exports.afterFrameCallback = localHelpers_1.noop;

function setBeforeRender(callback) {
    var res = exports.beforeRenderCallback;
    exports.beforeRenderCallback = callback;
    return res;
}

function setBeforeFrame(callback) {
    var res = exports.beforeFrameCallback;
    exports.beforeFrameCallback = callback;
    return res;
}

function setReallyBeforeFrame(callback) {
    var res = exports.reallyBeforeFrameCallback;
    exports.reallyBeforeFrameCallback = callback;
    return res;
}

function setAfterFrame(callback) {
    var res = exports.afterFrameCallback;
    exports.afterFrameCallback = callback;
    return res;
}

