"use strict";
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
Object.defineProperty(exports, "__esModule", { value: true });
const path = require("path");
const https = require("https");
const url_module = require("url");
const yauzl = require("yauzl");
const fs = require("fs");
const util = require("util");
const os = require("os");
const child_process = require("child_process");
const buffer_1 = require("buffer");
const ver = "bobril-build-core/1.2.0";
function get(url, options) {
    return new Promise((resolve, reject) => {
        try {
            options = Object.assign(url_module.parse(url), options);
            https
                .get(options, (response) => {
                var str = [];
                response.on("data", function (chunk) {
                    str.push(chunk);
                });
                response.on("end", function () {
                    resolve({ response, body: buffer_1.Buffer.concat(str) });
                });
                response.on("error", function (err) {
                    reject(err);
                });
            })
                .on("error", function (err) {
                reject(err);
            })
                .end();
        }
        catch (err) {
            reject(err);
        }
    });
}
function getRelease(tagName) {
    return __awaiter(this, void 0, void 0, function* () {
        var urlPath = "releases/";
        if (tagName != null) {
            urlPath += `tags/${tagName}`;
        }
        else
            urlPath += "latest";
        return yield callRepoApi(urlPath);
    });
}
function downloadAsset(asset) {
    return __awaiter(this, void 0, void 0, function* () {
        return yield downloadAssetOfUrl(asset.url);
    });
}
function downloadAssetOfUrl(url) {
    return __awaiter(this, void 0, void 0, function* () {
        var binary = yield get(url, getDownloadOptions(url));
        if (binary.response.statusCode === 302) {
            return yield downloadAssetOfUrl(binary.response.headers.location);
        }
        else if (binary.response.statusCode !== 200) {
            throw new Error(`Request failed with code ${binary.response.statusCode}`);
        }
        return binary.body;
    });
}
let githubToken;
function addAuthorization(headers) {
    if (githubToken) {
        headers["Authorization"] = "token " + githubToken;
    }
}
function callRepoApi(path) {
    return __awaiter(this, void 0, void 0, function* () {
        var options = {
            headers: {
                accept: "application/vnd.github.v3.json",
                "user-agent": ver,
            },
        };
        addAuthorization(options.headers);
        var binary = yield get(`https://api.github.com/repos/bobril/bbcore/${path}`, options);
        var data = JSON.parse(binary.body.toString("utf-8"));
        if (binary.response.statusCode !== 200)
            throw new Error(data.message);
        return data;
    });
}
function getDownloadOptions(url) {
    var isGitHubUrl = url_module.parse(url).hostname === "api.github.com";
    var headers = isGitHubUrl
        ? {
            accept: "application/octet-stream",
            "user-agent": ver,
        }
        : {};
    if (isGitHubUrl)
        addAuthorization(headers);
    return {
        headers: headers,
    };
}
function mkdirp(dir, cb) {
    if (dir === ".")
        return cb();
    fs.stat(dir, function (err) {
        if (err == null)
            return cb(); // already exists
        var parent = path.dirname(dir);
        mkdirp(parent, function () {
            fs.mkdir(dir, cb);
        });
    });
}
function unzip(buffer, targetDir) {
    return new Promise((resolve, reject) => {
        yauzl.fromBuffer(buffer, { lazyEntries: true }, (err, zipfile) => {
            if (err) {
                reject(err);
                return;
            }
            // track when we've closed all our file handles
            var handleCount = 0;
            function incrementHandleCount() {
                handleCount++;
            }
            function decrementHandleCount() {
                handleCount--;
                if (handleCount === 0) {
                    resolve();
                }
            }
            incrementHandleCount();
            zipfile.on("end", function () {
                decrementHandleCount();
            });
            zipfile.on("entry", function (entry) {
                if (/\/$/.test(entry.fileName)) {
                    // directory file names end with '/'
                    mkdirp(path.join(targetDir, entry.fileName), function () {
                        if (err)
                            throw err;
                        zipfile.readEntry();
                    });
                }
                else {
                    // ensure parent directory exists
                    mkdirp(path.join(targetDir, path.dirname(entry.fileName)), function () {
                        zipfile.openReadStream(entry, function (err, readStream) {
                            if (err) {
                                reject(err);
                                return;
                            }
                            // pump file contents
                            var writeStream = fs.createWriteStream(path.join(targetDir, entry.fileName));
                            incrementHandleCount();
                            writeStream.on("close", () => {
                                decrementHandleCount();
                                zipfile.readEntry();
                            });
                            readStream.pipe(writeStream);
                        });
                    });
                }
            });
            zipfile.readEntry();
        });
    });
}
const platformToAssetNameMap = {
    "win32-x64": "win-x64.zip",
    "win32-x86": "win-x64.zip",
    "win32-x32": "win-x64.zip",
    "win32-ia32": "win-x64.zip",
    "linux-x64": "linux-x64.zip",
    "darwin-x64": "osx-x64.zip",
    "darwin-arm64": "osx-arm64.zip",
};
const platformWithArch = os.platform() + "-" + os.arch();
let platformAssetName = platformToAssetNameMap[platformWithArch] || platformWithArch + ".zip";
let homeDir = process.env["BBCACHEDIR"] || path.join(os.homedir(), ".bbcore");
let lastVersionFileName = path.join(homeDir, ".lastversion");
let requestedVersion = "*";
let envVersion = process.env["BBVERSION"];
if (envVersion) {
    requestedVersion = envVersion;
}
versionKnown: do {
    if (global.bb2 && envVersion) {
        break versionKnown;
    }
    if (fs.existsSync("package.json")) {
        try {
            var bbrcjson = JSON.parse(fs
                .readFileSync("package.json")
                .toString("utf-8")
                .replace("\uFEFF", ""));
            if (bbrcjson.bobril && bbrcjson.bobril.bbVersion) {
                requestedVersion = bbrcjson.bobril.bbVersion;
                break versionKnown;
            }
        }
        catch (_a) {
            // ignore
        }
    }
    var dir = process.cwd();
    while (true) {
        var fn = path.join(dir, ".bbrc");
        if (fs.existsSync(fn)) {
            try {
                var bbrcjson = JSON.parse(fs.readFileSync(fn).toString("utf-8").replace("\uFEFF", ""));
                if (bbrcjson && bbrcjson.bbVersion) {
                    requestedVersion = bbrcjson.bbVersion;
                    break versionKnown;
                }
            }
            catch (_b) {
                // ignore
            }
        }
        dir = path.dirname(dir);
        if (dir.length < 4) {
            break;
        }
    }
} while (false);
if (process.env.GITHUB_TOKEN) {
    githubToken = "" + process.env.GITHUB_TOKEN;
}
else {
    let githubTokenFile = path.join(os.homedir(), ".github", "token.txt");
    if (fs.existsSync(githubTokenFile)) {
        try {
            githubToken = fs
                .readFileSync(githubTokenFile)
                .toString("utf-8")
                .split(/\r?\n/)[0];
        }
        catch (_c) {
            // ignore
        }
    }
}
const fsExists = util.promisify(fs.exists);
const fsStat = util.promisify(fs.stat);
const fsWriteFile = util.promisify(fs.writeFile);
const fsReadFile = util.promisify(fs.readFile);
const fsMkdir = util.promisify(fs.mkdir);
const fsChmod = util.promisify(fs.chmod);
function checkFreshnessOfCachedLastVersion() {
    return __awaiter(this, void 0, void 0, function* () {
        try {
            if (!(yield fsExists(lastVersionFileName))) {
                return false;
            }
            var s = yield fsStat(lastVersionFileName);
            return +s.mtime + 8 * 3600 * 1000 > Date.now();
        }
        catch (_a) {
            return false;
        }
    });
}
(() => __awaiter(void 0, void 0, void 0, function* () {
    if (!(yield fsExists(homeDir))) {
        try {
            yield fsMkdir(homeDir);
        }
        catch (err) {
            console.error("Cannot create dir " + homeDir);
            console.error(err.stack);
            process.exit(1);
        }
    }
    if (requestedVersion == "*") {
        if (!(yield checkFreshnessOfCachedLastVersion())) {
            console.log("Updating latest version information from Github");
            try {
                var rel = yield getRelease();
                yield fsWriteFile(lastVersionFileName, JSON.stringify(rel));
            }
            catch (ex) {
                console.log("Update latest version information failed ignoring");
                console.log(ex.stack);
            }
        }
        try {
            var last = JSON.parse(yield fsReadFile(lastVersionFileName, "utf-8"));
            requestedVersion = last.tag_name;
        }
        catch (_d) {
            console.log("Github did not return latest version information\nplease read https://github.com/bobril/bbcore");
            process.exit(1);
            return;
        }
    }
    let toRun = path.join(homeDir, requestedVersion);
    if (!(yield fsExists(path.join(toRun, ".success")))) {
        var rel;
        try {
            rel = yield getRelease(requestedVersion);
        }
        catch (ex) {
            console.log("Failed to retrieve information about version " +
                requestedVersion);
            console.log(ex.stack);
            process.exit(1);
        }
        var assets = rel.assets;
        var asset = assets.find((a) => a.name == platformAssetName);
        if (asset) {
            console.log("Downloading " + asset.name + " from " + rel.tag_name);
            var zip;
            try {
                zip = yield downloadAsset(asset);
            }
            catch (ex) {
                console.log("Failed to download version " + requestedVersion);
                console.log(ex.stack);
                process.exit(1);
            }
            console.log("Unzipping");
            yield unzip(zip, path.join(homeDir, requestedVersion));
            let bbName = path.join(homeDir, requestedVersion, "bb");
            if (yield fsExists(bbName)) {
                var stat = yield fsStat(bbName);
                yield fsChmod(bbName, stat.mode | parseInt("110", 8));
            }
        }
        else {
            console.log("Bobril-build core is currently not available on your platform. Please help with porting it.");
            console.log("Not found " +
                platformAssetName +
                " in " +
                assets.map((a) => a.name).join(", "));
            process.exit(1);
        }
    }
    try {
        yield fsWriteFile(path.join(toRun, ".success"), "", "utf-8");
    }
    catch (err) {
        console.log("Ignoring failure to update " +
            path.join(toRun, ".success") +
            " " +
            err);
    }
    console.log("Bobril-build core running " + toRun);
    let proc = child_process.spawn(path.join(toRun, "bb"), process.argv.slice(2), { stdio: ["pipe", process.stdout, process.stderr] });
    proc.on("exit", (code) => {
        process.exit(code);
    });
    process.stdin.pipe(proc.stdin, { end: true });
    function endBBCore() {
        proc.stdin.write("\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b" +
            os.EOL +
            "quit" +
            os.EOL);
    }
    process.on("SIGINT", endBBCore);
    process.on("SIGTERM", endBBCore);
    process.on("SIGBREAK", endBBCore);
}))();
