"use strict";
class B {
}
class C extends B {
    constructor() {
        super(...arguments);
        this.prop = "foo";
    }
}
