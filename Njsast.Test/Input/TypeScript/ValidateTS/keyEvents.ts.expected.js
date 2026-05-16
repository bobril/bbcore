import { addEvent, bubble, IBobrilCacheNode, IEventParam, preventDefault } from "./core";

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
    if (bubble(node, "onKeyDown", param)) {
        preventDefault(ev);
        return true;
    }
    return false;
}

function emitOnKeyUp(ev, _target, node) {
    if (!node) return false;
    var param = buildParam(ev);
    if (bubble(node, "onKeyUp", param)) {
        preventDefault(ev);
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
    if (bubble(node, "onKeyPress", param)) {
        preventDefault(ev);
        return true;
    }
    return false;
}

addEvent("keydown", 50, emitOnKeyDown);

addEvent("keyup", 50, emitOnKeyUp);

addEvent("keypress", 50, emitOnKeyPress);

