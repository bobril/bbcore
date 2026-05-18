// @target: es2015
"use strict";
var package;
(function (package) {
})(package || (package = {}));
var foo;
(function (foo) {
    foo[foo["public"] = 0] = "public";
    foo[foo["private"] = 1] = "private";
    foo[foo["pacakge"] = 2] = "pacakge";
})(foo || (foo = {}));
var private;
(function (private) {
    private[private["public"] = 0] = "public";
    private[private["private"] = 1] = "private";
    private[private["pacakge"] = 2] = "pacakge";
})(private || (private = {}));
var bar;
(function (bar) {
    bar[bar["public"] = 0] = "public";
    bar[bar["private"] = 1] = "private";
    bar[bar["pacakge"] = 2] = "pacakge";
})(bar || (bar = {}));
