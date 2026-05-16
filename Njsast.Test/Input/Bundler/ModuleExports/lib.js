"use strict";

var param = require("./param");

module.exports = require("./wrapper")(param);

if ("test" in global) {
  setTimeout(global.test, 1);
}
