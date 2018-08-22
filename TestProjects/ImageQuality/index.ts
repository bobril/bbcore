import * as b from "bobril";
import { light_png as lightPng, Sample_png } from "./src/assets";

function randomColor() {
  return (
    "#" +
    ((Math.random() * 0xffffff) | (0 + 0x1000000)).toString(16).substring(1)
  );
}

var sample = b.sprite(Sample_png);
var lightDefault = b.sprite(lightPng);
var lightRandom = b.sprite(lightPng, randomColor);
var lightRandomOnce = b.sprite(lightPng, randomColor());
//setInterval(() => b.invalidateStyles(), 1000);

b.init(() => {
  return [
    b.styledDiv("", sample),
    b.styledDiv("", lightDefault),
    b.styledDiv("", lightRandom),
    b.styledDiv("", lightRandomOnce)
  ];
});
