"use strict";
// @target: es2015
class C {
    constructor() {
        this.Foo = 0; // error - duplicate identifier Foo - confirmed
    }
    get Foo() { return "foo"; } // ok
    set Foo(foo) { } // ok
    get Goo(v) { return null; } // error - getters must not have a parameter
    set Goo(v): string { } // error - setters must not specify a return type
}
class E {
    get Baz() { return 0; }
    set Baz(n) { } // error - accessors do not agree in visibility
}
