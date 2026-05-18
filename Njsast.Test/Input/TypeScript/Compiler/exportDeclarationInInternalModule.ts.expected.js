"use strict";
// @target: es5, es2015
// @module: commonjs
// @declaration: true
class Bbb {
}
class Aaa extends Bbb {
}
(function (Aaa) {
    class SomeType {
    }
    Aaa.SomeType = SomeType;
})(Aaa || (Aaa = {}));
(function (Bbb) {
    class SomeType {
    }
    Bbb.SomeType = SomeType;
})(Bbb || (Bbb = {}));
var a;
