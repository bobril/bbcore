"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var fs = require("fs");
var path = require("path");
var tslibSourceContent = fs.readFileSync("../../../TSCompiler/tslib.js", "utf-8");
var importSourceContent = fs.readFileSync("../../../TSCompiler/import.js", "utf-8");
var prefix = "";
var currentTestDir = "";
var errors = 0;
function mkdir(dir) {
    if (dir === ".")
        return;
    if (fs.existsSync(dir))
        return;
    var parent = path.dirname(dir);
    mkdir(parent);
    fs.mkdirSync(dir);
}
var bb = {
    readContent: function (name) {
        return fs.readFileSync(path.join("Inputs", currentTestDir, name), "utf-8");
    },
    writeBundle: function (name, content) {
        var outputName = prefix + "-" + name + ".js";
        fs.writeFileSync(path.join("Outputs", currentTestDir, outputName), content);
        if (fs.existsSync(path.join("Expected", currentTestDir, outputName))) {
            var expected = fs.readFileSync(path.join("Expected", currentTestDir, outputName), "utf-8");
            if (expected != content) {
                console.log("ERROR Difference in " + path.join("Expected", currentTestDir, outputName));
                errors++;
            }
        }
        else {
            console.log("New file " + path.join("Outputs", currentTestDir, outputName));
        }
    },
    generateBundleName: function (forName) {
        if (forName === "")
            return "bundle";
        return forName.replace(/[\/\\]/g, "_");
    },
    resolveRequire: function (name, from) {
        if (name.substr(0, 2) == "./")
            return name.substr(2) + ".js";
        return name + ".js";
    },
    tslibSource: function (withImport) {
        return tslibSourceContent + (withImport ? importSourceContent : "");
    }
};
var uglifyJsContent = fs.readFileSync("../uglify.js", "utf-8");
var bundlerJsContent = fs.readFileSync("../bundler.js", "utf-8");
var bundleImp = eval(uglifyJsContent + bundlerJsContent + 'bundle;');
var tests = fs.readdirSync("Inputs");
mkdir("Expected");
currentTestDir = "Split";
mkdir(path.join("Outputs", currentTestDir));
prefix = "cb";
bundleImp({ mainFiles: ["index.js"], compress: true, beautify: true, mangle: false, defines: { DEBUG: false } });
/*
for (let i = 0; i < tests.length; i++) {
    currentTestDir = tests[i];
    mkdir(path.join("Outputs", currentTestDir));
    prefix = "cm";
    bundleImp({ mainFiles: ["index.js"], compress: true, beautify: false, mangle: true, defines: { DEBUG: false } });
    prefix = "cbm";
    bundleImp({ mainFiles: ["index.js"], compress: true, beautify: true, mangle: true, defines: { DEBUG: false } });
    prefix = "cb";
    bundleImp({ mainFiles: ["index.js"], compress: true, beautify: true, mangle: false, defines: { DEBUG: false } });
    prefix = "b";
    bundleImp({ mainFiles: ["index.js"], compress: false, beautify: true, mangle: false, defines: { DEBUG: false } });
}

console.log("Total " + tests.length + " tests with " + errors + " errors");
*/ 
