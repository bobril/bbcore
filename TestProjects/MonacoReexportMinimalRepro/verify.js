// Minimal Node runner for bbcore's bundle.
const fs = require("fs");
const path = require("path");

const bundlePath = path.join(__dirname, "dist", "bundle.js");
const bundleSrc = fs.readFileSync(bundlePath, "utf-8");

global.window = global;
global.document = { addEventListener: () => {} };

eval(bundleSrc);

const moduleName = "src/fake-editor/index";
const entry = R.m.get(moduleName.toLowerCase());
if (!entry) {
    console.error("Module not registered:", moduleName);
    process.exitCode = 2;
    return;
}

const moduleObj = { exports: {} };
try {
    entry.fn((id) => R.r(id, moduleName), moduleObj, moduleObj.exports);
    console.log("OK: module loaded without temporal-dead-zone error. Exports:", Object.keys(moduleObj.exports));
} catch (err) {
    console.error("Runtime error reproduced:");
    console.error(err.name + ": " + err.message);
    process.exitCode = 1;
}
