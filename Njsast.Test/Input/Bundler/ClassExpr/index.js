var Klass;

(() => {
    Klass = class Klass {
        constructor() {
            console.log('Klass');
        }

        method() {
            new Klass();
        }
    }
})();

var inst = new Klass();

inst.method();
