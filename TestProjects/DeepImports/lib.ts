import * as deep from "./deeplib";

console.log("lib.ts");

export function lib() {
    console.log("lib.ts: lib()");
    deep.deep();
}
