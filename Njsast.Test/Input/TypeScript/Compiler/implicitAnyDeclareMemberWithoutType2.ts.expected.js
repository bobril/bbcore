"use strict";
// @target: es2015
// @noimplicitany: true
// this should be an error
class C {
    constructor(c1, c2, c3) {
        this.x = null; // error at "x"
    } // error at "c1, c2"
    funcOfC(f1, f2, f3) { } // error at "f1,f2"
}
