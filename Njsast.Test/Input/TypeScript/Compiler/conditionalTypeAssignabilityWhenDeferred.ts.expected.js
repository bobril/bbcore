"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.func = func;
function select(property, list, valueProp) { }
function func(x, tipos) {
    select(x, tipos, "value");
}
onlyNullablePlease(z); // works as expected
onlyNullablePlease2(z); // works as expected
onlyNullablePlease(y); // error as expected
onlyNullablePlease2(y); // error as expected
function f(t) {
    var x = Math.random() > 0.5 ? null : t;
    onlyNullablePlease(x); // should work
    onlyNullablePlease2(x); // should work
}
function f2(t1, t2) {
    t1 = t2; // OK
    t2 = t1; // should fail
}
function test(x, s) {
    x = "a"; // Currently an error, should be ok
    x = s; // Error
}
function testAssignabilityToConditionalType() {
    const o = { a: 1, b: 2 };
    const x = undefined;
    // Simple case: OK
    const o1 = o;
    // Simple case where source happens to be a conditional type: also OK
    const x1 = x;
    // Infer type parameters: no good
    const o2 = o;
    // The next 4 are arguable - if you choose to ignore the `never` distribution case,
    // then they're all good. The `never` case _is_ a bit of an outlier - we say distributive types
    // look approximately like the sum of their branches, but the `never` case bucks that.
    // There's an argument for the result of dumping `never` into a distributive conditional
    // being not `never`, but instead the intersection of the branches - a much more precise bound
    // on that "impossible" input.
    // Distributive where T might instantiate to never: no good
    const o3 = o;
    // Distributive where T & string might instantiate to never: also no good
    const o4 = o;
    // Distributive where {a: T} cannot instantiate to never: OK
    const o5 = o;
    // Distributive where check type is a conditional which returns a non-never type upon instantiation with `never` but can still return never otherwise: no good
    const o6 = o;
}
class Foo2 {
    method() {
        set(this, "prop", "hi"); // <-- type error
    }
}
set(new Foo2(), "prop", "hi"); // <-- typechecks
function f3(x) {
    return x;
}
function f4(x) {
    return x; // should fail
}
