import { kType } from "./lib2";

const kError = Symbol("kError");
const kNext = Symbol("kNext");

export class A {
    [kType] = "B";

    constructor() {}

    [kError]() {
        throw new Error();
    }
    [kNext]() {
        console.log("next");
    }
}
