import * as b from "bobril";
import * as g from "bobril-g11n";
import { icon_png } from "./icons";

b.init(() => {
  b.asset("logo.png");
  b.asset("" + Date.now());
  b.styleDef({ color: "red" });
  b.styleDefEx("parent", { color: "blue" });
  b.styleDef({ color: "red" }, { hover: { color: "pink" } });
  b.styleDefEx("parent", { color: "blue" }, { hover: { color: "pink" } });
  b.styleDef({ color: "red" }, undefined, "class1");
  b.styleDefEx("parent", { color: "blue" }, undefined, "class2");
  b.styleDef({ color: "red" }, { hover: { color: "pink" } }, "class3");
  b.styleDefEx(
    "parent",
    { color: "blue" },
    { hover: { color: "pink" } },
    "class4"
  );
  b.styleDef({ color: "red" }, undefined, "" + Date.now());
  b.sprite(icon_png);
  b.sprite("logo.png", "#123456");
  b.sprite("logo.png", () => "#" + Date.now());
  b.sprite("logo.png", undefined, 10, 20);
  b.sprite("logo.png", undefined, 10, 20, 30, 40);
  const d1 = g.dt("Delayed", null, "Hint");
  const d2 = g.dt("Delayed", undefined, "Hint");
  console.log(g.f("{d1} {d2}", { d1, d2 }));
  console.log(g.t("Hello"));
  console.log(g.t("Hello", undefined, "hint"));
  console.log(g.t("World{p}", { p: "!" }));
  console.log(g.t("{p}", { p: d1, pp: d2 }));
  let notConst = "" + Date.now();
  console.log(g.t(notConst, null, "" + Date.now()));
});
