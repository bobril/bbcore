"use strict";
function dec() {
    return function (target, propKey, descr) {
        console.log(target[propKey]);
        //logs undefined
        //propKey has three underscores as prefix, but the method has only two underscores
    };
}
class A {
    __foo(bar) {
        // do something with bar
    }
}
__decorate([
    dec()
], A.prototype, "__foo", null);
