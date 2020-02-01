"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
const fs = require("fs");
const path = require("path");
const tslibSourceContent = fs.readFileSync("tslib.js", "utf-8");
const importSourceContent = fs.readFileSync("import.js", "utf-8");
let prefix = "";
let currentTestDir = "";
let errors = 0;
function mkdir(dir) {
    if (dir === ".")
        return;
    if (fs.existsSync(dir))
        return;
    var parent = path.dirname(dir);
    mkdir(parent);
    fs.mkdirSync(dir);
}
const bb = {
    readContent(name) {
        return fs.readFileSync(path.join("Inputs", currentTestDir, name), "utf-8");
    },
    getPlainJsDependencies(name) {
        let dirName = name.substr(0, name.length - 3);
        name = path.join("Inputs", currentTestDir, dirName);
        if (fs.existsSync(name))
            return fs
                .readdirSync(name)
                .map(s => dirName + "/" + s)
                .join("|");
        return "";
    },
    writeBundle(name, content) {
        const outputName = prefix + "-" + name + ".js";
        fs.writeFileSync(path.join("Outputs", currentTestDir, outputName), content);
        if (fs.existsSync(path.join("Expected", currentTestDir, outputName))) {
            let expected = fs.readFileSync(path.join("Expected", currentTestDir, outputName), "utf-8");
            if (expected != content) {
                console.log("ERROR Difference in " + path.join("Expected", currentTestDir, outputName));
                errors++;
            }
        }
        else {
            console.log("New file " + path.join("Outputs", currentTestDir, outputName));
        }
    },
    generateBundleName(forName) {
        if (forName === "")
            return "bundle";
        return forName.replace(/[\/\\]/g, "_");
    },
    resolveRequire(name, from) {
        function addJs(name) {
            if (name.endsWith(".json"))
                return name;
            return name + ".js";
        }
        if (name.substr(0, 2) == "./")
            return addJs(name.substr(2));
        return addJs(name);
    },
    tslibSource(withImport) {
        return tslibSourceContent + (withImport ? importSourceContent : "");
    },
    log(text) {
        console.log(text);
    }
};
const uglifyJsContent = fs.readFileSync("../uglify.js", "utf-8");
const bundlerJsContent = fs.readFileSync("../bundler.js", "utf-8");
var bundleImp = eval(uglifyJsContent + bundlerJsContent + "bundle;");
let tests = fs.readdirSync("Inputs");
mkdir("Expected");
/*
currentTestDir = "Split";
mkdir(path.join("Outputs", currentTestDir));
prefix = "cb";
bundleImp({ mainFiles: ["index.js"], compress: true, beautify: true, mangle: false, defines: { DEBUG: false } });
*/
for (let i = 0; i < tests.length; i++) {
    currentTestDir = tests[i];
    console.log(currentTestDir);
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
