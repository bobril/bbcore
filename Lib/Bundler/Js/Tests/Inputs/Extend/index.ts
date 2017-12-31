import * as lib from './lib';

class Main extends lib.Base {
    hello() {
        console.log("Main");
    }
}

new Main().hello();
