import * as b from "bobril";

let k = "k";

export const s = b.styleDef(
    { color: "blue" },
    { [`.${k}`]: { color: "red" } },
    "name"
);
