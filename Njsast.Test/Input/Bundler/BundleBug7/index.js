"use strict";
var sessionUpdater = require("./sessionUpdater");
var sessionStopper = require("./sessionStopper");
sessionUpdater.start();
console.log("working");
sessionStopper.stop();
