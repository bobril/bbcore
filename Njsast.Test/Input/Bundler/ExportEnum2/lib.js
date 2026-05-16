"use strict";
exports.ok = exports.Opts = void 0;
var Opts;
(function (Opts) {
    Opts[Opts["Start"] = 0] = "Start";
    Opts[Opts["Stop"] = 1] = "Stop";
})(Opts = exports.Opts || (exports.Opts = {}));
function ok() {
    console.log(Opts.Stop);
}
exports.ok = ok;
