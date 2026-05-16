import * as b from "bobril";
import * as g from "bobril-g11n";

b.init(() => (
  <g.T p1="param1">
    Before
    <hr />
    {g.t("{p1}")}
  </g.T>
));
