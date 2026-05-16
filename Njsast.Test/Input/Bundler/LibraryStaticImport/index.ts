import { bar as foo } from "./lib2";

export function addOne(v: number) {
    return foo(1, v);
}

console.log(foo(1, 2));

export default foo;
