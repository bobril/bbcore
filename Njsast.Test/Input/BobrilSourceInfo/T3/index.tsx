import * as b from "bobril";
import * as g from "bobril-g11n";

b.init(() => (
  <g.T
    hint="Leave things like '{1}' on appropriate places"
    p1={b.now() - 100000}
    p2={42}
  >
    Normal{" "}
    <b style={{ fontSize: 5 }}>
      Bold {g.t("{p1, time, relativepast}")} <i>and italic</i> and back to just
      bold
    </b>{" "}
    backslash \ and number {g.t("{p2}")}
  </g.T>
));
