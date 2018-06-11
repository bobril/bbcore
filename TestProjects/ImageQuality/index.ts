import * as b from "bobril";
import * as assets from "./src/assets";

function randomColor() {
    return "#" + ((Math.random() * 0xffffff) | (0 + 0x1000000)).toString(16).substring(1);
}

var sample = b.sprite(assets.Sample_png);
var lightDefault = b.sprite(assets.light_png);
var lightRandom = b.sprite(assets.light_png, randomColor);

//setInterval(() => b.invalidateStyles(), 1000);

b.init(() => {
    return [b.styledDiv("", sample), b.styledDiv("", lightDefault), b.styledDiv("", lightRandom)];
});
