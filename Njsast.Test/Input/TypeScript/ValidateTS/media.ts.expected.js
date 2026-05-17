"use strict";
exports.BobrilDeviceCategory = void 0;

exports.accDeviceBreaks = accDeviceBreaks;

exports.getMedia = getMedia;

const core_1 = require("./core");

var BobrilDeviceCategory;

(function(BobrilDeviceCategory) {
    BobrilDeviceCategory[BobrilDeviceCategory["Mobile"] = 0] = "Mobile";
    BobrilDeviceCategory[BobrilDeviceCategory["Tablet"] = 1] = "Tablet";
    BobrilDeviceCategory[BobrilDeviceCategory["Desktop"] = 2] = "Desktop";
    BobrilDeviceCategory[BobrilDeviceCategory["LargeDesktop"] = 3] = "LargeDesktop";
})(BobrilDeviceCategory || (exports.BobrilDeviceCategory = BobrilDeviceCategory = {}));

var media = null;

var breaks = [ [ 414, 800, 900 ], [ 736, 1280, 1440 ] ];

function emitOnMediaChange() {
    media = null;
    core_1.invalidate();
    return false;
}

var events = [ "resize", "orientationchange" ];

for (var i = 0; i < events.length; i++) core_1.addEvent(events[i], 10, emitOnMediaChange);

function accDeviceBreaks(newBreaks) {
    if (newBreaks != null) {
        breaks = newBreaks;
        emitOnMediaChange();
    }
    return breaks;
}

var viewport = window.document.documentElement;

var isAndroid = /Android/i.test(navigator.userAgent);

var weirdPortrait;

function getMedia() {
    if (media == undefined) {
        var w = viewport.clientWidth;
        var h = viewport.clientHeight;
        var o = window.orientation;
        var p = h >= w;
        if (o == undefined) o = p ? 0 : 90; else o = +o;
        if (isAndroid) {
            let op = Math.abs(o) % 180 === 90;
            if (weirdPortrait == undefined) {
                weirdPortrait = op === p;
            } else {
                p = op === weirdPortrait;
            }
        }
        var device = 0;
        while (w > breaks[+!p][device]) device++;
        media = {
            width: w,
            height: h,
            orientation: o,
            deviceCategory: device,
            portrait: p,
            dppx: window.devicePixelRatio
        };
    }
    return media;
}

