import * as b from "bobril";
import * as g from "bobril-g11n";

b.init(() => {
  //console.log(g.t("Hello"));
  //console.log(g.t("World{p}", { p: "!" }));
  //console.log(g.t("{p}", { p: "?", pp: "not used" }));
  return g.t("Normal text {1}bold text {2}with{/2} {param}{/1}", {
    1: (p: b.IBobrilChildren) => <b>{p}</b>,
    2: (p: b.IBobrilChildren) => (
      <i>
        <u>{p}</u>
      </i>
    ),
    param: "parameter"
  });
});
