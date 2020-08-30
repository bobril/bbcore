import * as b from "bobril";
import * as g from "bobril-g11n";

b.init(() => b.styledDiv(g.t("Hello World"), { fontSize: "20px" }));
