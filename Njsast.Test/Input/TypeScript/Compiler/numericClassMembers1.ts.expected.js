"use strict";
// @target: es2015
class C234 {
    constructor() {
        this[0] = 1;
        this[0.0] = 2;
    }
}
class C235 {
    constructor() {
        this[0.0] = 1;
        this['0'] = 2;
    }
}
class C236 {
    constructor() {
        this['0.0'] = 1;
        this['0'] = 2;
    }
}
