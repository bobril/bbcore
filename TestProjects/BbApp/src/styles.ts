import * as b from "bobril";

export const style1 = b.styleDef({ color: "blue" });

export const style2 = b.styleDefEx(style1, { background: "red" });
