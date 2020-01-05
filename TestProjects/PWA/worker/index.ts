import * as Comlink from "comlink";

const obj = {
    counter: 0,
    inc() {
        console.log("Worker incrementing counter");
        this.counter++;
    }
};

Comlink.expose(obj);
