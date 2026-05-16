import { isArray } from "./lib3";

export { isArray } from "./lib3";

let isArray2 = isArray;

export function setArray2(value: typeof isArray) {
    isArray2 = value;
}

export function test() {
    console.log(isArray([]));
    console.log(isArray2([]));
}
