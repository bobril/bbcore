import * as b from "bobril";

b.asset("./asset1.js");
b.asset("./asset2.js");

console.log((globalThis as any).KindOfGlobal);
