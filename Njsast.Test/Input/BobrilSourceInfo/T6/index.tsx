import * as b from "bobril";
import * as g from "bobril-g11n";

b.init(() => {
  let param = {
    p1: "Visit page"
  };
  let param2 = {
    p2: "Bobril"
  };
  return (
    <g.T hint="hint" {...param} {...param2}>
      {g.t("{p1}")} of <a href="https://bobril.com">{g.t("{p2}")}</a>{" "}
      <img src={b.asset("./logo.png")} />
    </g.T>
  );
});
