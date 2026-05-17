"use strict";
const core_1 = require("./core");

const NormalizerKeyMap = {
    Up: "ArrowUp",
    Down: "ArrowDown",
    Left: "ArrowLeft",
    Right: "ArrowRight",
    Del: "Delete",
    Crsel: "CrSel",
    Exsel: "ExSel",
    Esc: "Escape",
    Apps: "ContextMenu",
    OS: "Meta",
    Win: "Meta",
    Scroll: "ScrollLock",
    Spacebar: " ",
    Nonconvert: "NonConvert",
    Decimal: ".",
    Separator: ",",
    Multiply: "*",
    Add: "+",
    Divide: "/",
    Subtract: "-",
    MediaNextTrack: "MediaTrackNext",
    MediaPreviousTrack: "MediaTrackPrevious",
    MediaFastForward: "FastFwd",
    Live: "TV",
    Zoom: "ZoomToggle",
    SelectMedia: "LaunchMediaPlayer",
    MediaSelect: "LaunchMediaPlayer",
    VolumeUp: "AudioVolumeUp",
    VolumeDown: "AudioVolumeDown",
    VolumeMute: "AudioVolumeMute"
};

function buildParam(ev) {
    return {
        target: undefined,
        shift: ev.shiftKey,
        ctrl: ev.ctrlKey,
        alt: ev.altKey,
        meta: ev.metaKey || false,
        which: ev.which || ev.keyCode,
        key: NormalizerKeyMap[ev.key] || ev.key
    };
}

function emitOnKeyDown(ev, _target, node) {
    if (!node) return false;
    var param = buildParam(ev);
    if (core_1.bubble(node, "onKeyDown", param)) {
        core_1.preventDefault(ev);
        return true;
    }
    return false;
}

function emitOnKeyUp(ev, _target, node) {
    if (!node) return false;
    var param = buildParam(ev);
    if (core_1.bubble(node, "onKeyUp", param)) {
        core_1.preventDefault(ev);
        return true;
    }
    return false;
}

function emitOnKeyPress(ev, _target, node) {
    if (!node) return false;
    if (ev.which === 0 || ev.altKey) return false;
    var param = {
        charCode: ev.which || ev.keyCode
    };
    if (core_1.bubble(node, "onKeyPress", param)) {
        core_1.preventDefault(ev);
        return true;
    }
    return false;
}

core_1.addEvent("keydown", 50, emitOnKeyDown);

core_1.addEvent("keyup", 50, emitOnKeyUp);

core_1.addEvent("keypress", 50, emitOnKeyPress);

