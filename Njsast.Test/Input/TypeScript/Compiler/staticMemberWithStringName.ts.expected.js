"use strict";
class C {
    constructor() {
        this.x = C["foo"];
    }
    static {
        this["foo"] = 0;
    }
}
