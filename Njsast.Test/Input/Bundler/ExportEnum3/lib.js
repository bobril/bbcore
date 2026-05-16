"use strict";
exports.Opts = exports.ok = void 0;
function ok() {
    console.log(Opts.Stop);
}
exports.ok = ok;
var Opts;
(function (Opts) {
    Opts[Opts["Start"] = 0] = "Start";
    Opts[Opts["Stop"] = 1] = "Stop";
})(Opts || (exports.Opts = Opts = {}));
