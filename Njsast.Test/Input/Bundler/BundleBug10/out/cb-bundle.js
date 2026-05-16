(() => {
    var __export_kType, _a;
    __export_kType = Symbol.for("Type");
    const kError = Symbol("kError");
    const kNext = Symbol("kNext");
    class A {
        constructor() {
            this[_a] = "B";
        }
        [(_a = __export_kType, kError)]() {
            throw new Error();
        }
        [kNext]() {
            console.log("next");
        }
    }
    new A();
})();

