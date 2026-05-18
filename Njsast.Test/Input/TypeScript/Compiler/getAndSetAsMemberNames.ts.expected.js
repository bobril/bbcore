"use strict";
// @target: es2015
// @strict: false
class C1 {
    constructor() {
        this.get = 1;
    }
}
class C2 {
}
class C3 {
    set(x) {
        return x + 1;
    }
}
class C4 {
    constructor() {
        this.get = true;
    }
}
class C5 {
    constructor() {
        this.set = function () { return true; };
    }
    get() { return true; }
    set t(x) { }
}
