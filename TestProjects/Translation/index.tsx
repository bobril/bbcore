import * as b from "bobril";
import * as g from "bobril-g11n";

b.init(() => {
  //console.log(g.t("Hello"));
  //console.log(g.t("World{p}", { p: "!" }));
  //console.log(g.t("{p}", { p: "?", pp: "not used" }));
  return (
    <g.T param="parameter">
      Normal text{" "}
      <b>
        bold text{" "}
        <i>
          <u>with</u>
        </i>{" "}
        {g.t("{param}")}
      </b>
    </g.T>
  );
});
