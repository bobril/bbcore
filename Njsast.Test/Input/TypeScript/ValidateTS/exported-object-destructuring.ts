import { register } from "./dnd";

export const key = "example/key";
export const {
    get: getData,
    set: setData,
    is: isData,
} = register<{ value: string }>()(key);
