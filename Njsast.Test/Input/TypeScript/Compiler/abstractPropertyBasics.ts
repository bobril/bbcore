//@target: ES5, ES2015
abstract class B {
    abstract prop: string;
}
class C extends B {
    prop = "foo";
}
