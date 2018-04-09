import * as b from "bobril";
import * as g from "bobril-g11n";

b.init(() => {
    console.log(g.t("Hello"));
    console.log(g.t("World{p}", { p: "!" }));
});
