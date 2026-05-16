import * as b from "bobril";
import * as g from "bobril-g11n";

b.init(() => (
  <g.T>
    Before
    <b>
      <hr />
    </b>
    After
  </g.T>
));
