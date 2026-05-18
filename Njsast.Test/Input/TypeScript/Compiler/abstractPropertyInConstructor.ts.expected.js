"use strict";
// @target: es2015
class AbstractClass {
    constructor(str, other) {
        this.other = this.prop;
        this.fn = () => this.prop;
        this.method(parseInt(str));
        let val = this.prop.toLowerCase();
        if (!str) {
            this.prop = "Hello World";
        }
        this.cb(str);
        // OK, reference is inside function
        const innerFunction = () => {
            return this.prop;
        };
        // OK, references are to another instance
        other.cb(other.prop);
    }
    method2() {
        this.prop = this.prop + "!";
    }
}
class DerivedAbstractClass extends AbstractClass {
    constructor(str, other, yetAnother) {
        super(str, other);
        this.cb = (s) => { };
        // there is no implementation of 'prop' in any base class
        this.cb(this.prop.toLowerCase());
        this.method(1);
        // OK, references are to another instance
        other.cb(other.prop);
        yetAnother.cb(yetAnother.prop);
    }
}
class Implementation extends DerivedAbstractClass {
    constructor(str, other, yetAnother) {
        super(str, other, yetAnother);
        this.prop = "";
        this.cb = (s) => { };
        this.cb(this.prop);
    }
    method(n) {
        this.cb(this.prop + n);
    }
}
class User {
    constructor(a) {
        a.prop;
        a.cb("hi");
        a.method(12);
        a.method2();
    }
}
class C1 {
    constructor() {
        let self = this; // ok
        let { x, y: y1 } = this; // error
        ({ x, y: y1, "y": y1 } = this); // error
    }
}
class C2 {
    constructor() {
        let self = this; // ok
        let { x, y: y1 } = this; // ok
        ({ x, y: y1, "y": y1 } = this); // ok
    }
}
