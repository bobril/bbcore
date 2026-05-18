"use strict";
// @target: es2015
// @lib: es6
var NS;
(function (NS) {
    NS.x = Symbol();
    class NotTransformed {
        static { NS.x; }
    }
})(NS || (NS = {}));
