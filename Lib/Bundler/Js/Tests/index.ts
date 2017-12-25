import * as fs from "fs";
import * as path from "path";
import { mkdirSync } from "fs";

const tslibSourceContent = fs.readFileSync("../../../TSCompiler/tslib.js", "utf-8");

let prefix = "";
let currentTestDir = "";
let errors = 0;

function mkdir(dir: string) {
    if (dir === ".") return;
    if (fs.existsSync(dir)) return;
    var parent = path.dirname(dir);
    mkdir(parent);
    fs.mkdirSync(dir);
}

const bb: IBB = {
    readContent(name: string): string {
        return fs.readFileSync(path.join("Inputs", currentTestDir, name), "utf-8");
    },
    writeBundle(content: string): void {
        const outputName: string = prefix + "-bundle.js";
        fs.writeFileSync(path.join("Outputs", currentTestDir, outputName), content);
        if (fs.existsSync(path.join("Expected", currentTestDir, outputName))) {
            let expected = fs.readFileSync(path.join("Expected", currentTestDir, outputName), "utf-8");
            if (expected != content) {
                console.log("ERROR Difference in " + path.join("Expected", currentTestDir, outputName));
                errors++;
            }
        } else {
            console.log("New file " + path.join("Outputs", currentTestDir, outputName));
        }
    },
    resolveRequire(name: string, from: string): string {
        if (name.substr(0, 2) == "./") return name.substr(2) + ".js";
        return name + ".js";
    },
    tslibSource() {
        return tslibSourceContent;
    }
};

interface IBB {
    readContent(name: string): string;
    writeBundle(content: string): void;
    resolveRequire(name: string, from: string): string;
    tslibSource(): string;
}

interface IBundleProject {
    mainFiles: string[];
    // default true
    compress?: boolean;
    // default true
    mangle?: boolean;
    // default false
    beautify?: boolean;
    defines?: { [name: string]: any };
}

const uglifyJsContent = fs.readFileSync("../uglify.js", "utf-8");
const bundlerJsContent = fs.readFileSync("../bundler.js", "utf-8");
var bundleImp = eval(uglifyJsContent + bundlerJsContent + 'bundle;') as (project: IBundleProject) => void;

let tests = fs.readdirSync("Inputs");
mkdir("Expected");

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
