// @ts-ignore
import ex from "External";
// @ts-ignore
import { ex2 } from "External";
// @ts-ignore
import { ex22 } from "External2";

export function getDescriptor() {
    return { a: ex, b: ex2, c: ex.ex3, d: ex22 };
}
