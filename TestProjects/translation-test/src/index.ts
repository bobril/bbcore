import * as b from "bobril";
import * as g from "bobril-g11n";
import { t } from "bobril-g11n";

(() => {
  g.initGlobalization({
    defaultLocale: "en-us",
  }).then(() => {
    g.setLocale("fr-fr");
  });
})();

g.f("" + Math.random());
b.init(() => t("test-en-key"));
