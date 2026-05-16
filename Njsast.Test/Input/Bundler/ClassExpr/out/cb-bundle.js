(() => {
    var inst, Klass_index;
    (() => {
        Klass_index = class Klass {
            constructor() {
                console.log("Klass");
            }
            method() {
                new Klass();
            }
        };
    })();
    inst = new Klass_index();
    inst.method();
})();

