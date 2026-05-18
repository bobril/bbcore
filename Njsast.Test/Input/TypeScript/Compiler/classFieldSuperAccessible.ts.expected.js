"use strict";
// @target: esnext
class A extends class Expr {
} {
    static {
        console.log(super.name);
    }
}
class B extends Number {
    static {
        console.log(super.EPSILON);
    }
}
class C extends Array {
    foo() {
        console.log(super.length);
    }
}
class D {
    #b_accessor_storage = () => { };
    get b() { return this.#b_accessor_storage; }
    set b(value) { this.#b_accessor_storage = value; }
}
class E extends D {
    foo() {
        super.b();
    }
}
