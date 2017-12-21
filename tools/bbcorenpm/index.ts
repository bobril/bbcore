import * as path from "path";
import * as http from "http";
import * as https from "https";
import * as urlmodule from "url";
import * as yauzl from "yauzl";
import * as fs from "fs";
import * as util from "util";
import * as os from "os";
import { homedir } from "os";
import * as child_process from "child_process";
import { mkdirSync } from "fs";

interface IResponse extends http.IncomingMessage {
    body: Buffer;
}

function get(url: string, options: http.RequestOptions): Promise<IResponse> {
    return new Promise<IResponse>((resolve, reject) => {
        options = Object.assign(urlmodule.parse(url), options);
        https
            .get(options, response => {
                var str: Buffer[] = [];
                response.on("data", function (chunk: Buffer) {
                    str.push(chunk);
                });

                response.on("end", function () {
                    var res = Object.assign({}, response, {
                        body: Buffer.concat(str)
                    });
                    resolve(res);
                });
            })
            .end();
    });
}

async function getRelease(tagName?: string) {
    var urlPath = "releases/";

    if (tagName != null) {
        urlPath += `tags/${tagName}`;
    } else urlPath += "latest";

    return await callRepoApi(urlPath);
}

async function downloadAsset(asset: any) {
    return await downloadAssetOfUrl(asset.url);
}

async function downloadAssetOfUrl(url: string): Promise<Buffer> {
    var binary = await get(url, getDownloadOptions(url));
    if (binary.statusCode === 302) {
        return await downloadAssetOfUrl(binary.headers.location!);
    } else if (binary.statusCode !== 200) {
        throw new Error(`Request failed with code ${binary.statusCode}`);
    }
    return binary.body;
}

async function callRepoApi(path: string) {
    var options = {
        proxy: process.env.http_proxy || process.env.https_proxy,
        headers: {
            accept: "application/vnd.github.v3.json",
            "user-agent": "bobril-build-core/1.0.0"
        }
    };

    var binary = await get(
        `https://api.github.com/repos/bobril/bbcore/${path}`,
        options
    );
    var data = JSON.parse(binary.body.toString("utf-8"));
    if (binary.statusCode !== 200) throw new Error(data.message);
    return data;
}

function getDownloadOptions(url: string) {
    var isGitHubUrl = urlmodule.parse(url).hostname === "api.github.com";

    var headers = isGitHubUrl
        ? {
            accept: "application/octet-stream",
            "user-agent": "bobril-build-core/1.0.0"
        }
        : {};

    return {
        followRedirect: false,
        proxy: process.env.http_proxy || process.env.https_proxy,
        headers: headers
    };
}

function mkdirp(dir: string, cb: () => void) {
    if (dir === ".") return cb();
    fs.stat(dir, function (err: any) {
        if (err == null) return cb(); // already exists

        var parent = path.dirname(dir);
        mkdirp(parent, function () {
            fs.mkdir(dir, cb);
        });
    });
}

function unzip(buffer: Buffer, targetDir: string): Promise<void> {
    return new Promise((resolve, reject) => {
        yauzl.fromBuffer(
            buffer,
            { lazyEntries: true },
            (err: any, zipfile: any) => {
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

                zipfile.on("entry", function (entry: any) {
                    if (/\/$/.test(entry.fileName)) {
                        // directory file names end with '/'
                        mkdirp(
                            path.join(targetDir, entry.fileName),
                            function () {
                                if (err) throw err;
                                zipfile.readEntry();
                            }
                        );
                    } else {
                        // ensure parent directory exists
                        mkdirp(
                            path.join(targetDir, path.dirname(entry.fileName)),
                            function () {
                                zipfile.openReadStream(entry, function (
                                    err: any,
                                    readStream: any
                                ) {
                                    if (err) {
                                        reject(err);
                                        return;
                                    }
                                    // pump file contents
                                    var writeStream = fs.createWriteStream(
                                        path.join(targetDir, entry.fileName)
                                    );
                                    incrementHandleCount();
                                    writeStream.on("close", () => {
                                        decrementHandleCount();
                                        zipfile.readEntry();
                                    });
                                    readStream.pipe(writeStream);
                                });
                            }
                        );
                    }
                });
                zipfile.readEntry();
            }
        );
    });
}

const platformToAssetNameMap: { [name: string]: string } = {
    "win32-x64": "win-x64.zip",
    "win32-x86": "win-x64.zip",
    "win32-x32": "win-x64.zip",
    "linux-x64": "linux-x64.zip",
    "darwin-x64": "osx-x64.zip"
};

const platformWithArch = os.platform() + "-" + os.arch();

let platformAssetName =
    platformToAssetNameMap[platformWithArch] || platformWithArch + ".zip";

let homeDir = path.join(os.homedir(), ".bbcore");
let lastVersionFileName = path.join(homeDir, ".lastversion");
let requestedVersion = "*";

if (fs.existsSync("package.json")) {
    try {
        var packageJson = JSON.parse(
            fs.readFileSync("package.json").toString("utf-8")
        );
        if (packageJson.bobril && packageJson.bobril.bbVersion) {
            requestedVersion = packageJson.bobril.bbVersion;
        }
    } catch {
        // ignore
    }
}

const fsExists = util.promisify(fs.exists);
const fsStat = util.promisify(fs.stat);
const fsWriteFile = util.promisify(fs.writeFile);
const fsReadFile = util.promisify(fs.readFile);
const fsMkdir = util.promisify(fs.mkdir);
const fsChmod = util.promisify(fs.chmod);

async function checkFreshnessOfCachedLastVersion(): Promise<boolean> {
    try {
        if (!await fsExists(lastVersionFileName)) {
            return false;
        }
        var s = await fsStat(lastVersionFileName);
        return +s.mtime + 8 * 3600 * 1000 > Date.now();
    } catch {
        return false;
    }
}

(async () => {
    if (!await fsExists(homeDir)) {
        try {
            await fsMkdir(homeDir);
        } catch (err) {
            console.error("Cannot create dir " + homeDir);
            console.error(err.stack);
            process.exit(1);
        }
    }
    if (requestedVersion == "*") {
        if (!await checkFreshnessOfCachedLastVersion()) {
            console.log("Updating latest version information from Github");
            var rel = await getRelease();

            await fsWriteFile(lastVersionFileName, JSON.stringify(rel));
        }
        var last = JSON.parse(await fsReadFile(lastVersionFileName, "utf-8"));
        requestedVersion = last.tag_name;
    }

    let toRun = path.join(homeDir, requestedVersion);
    if (!await fsExists(path.join(toRun, ".success"))) {
        var rel = await getRelease(requestedVersion);
        var assets = rel.assets as { url: string; name: string }[];
        var asset = assets.find(a => a.name == platformAssetName);
        if (asset) {
            console.log("Downloading " + asset.name + " from " + rel.tag_name);
            var zip = await downloadAsset(asset);
            console.log("Unzipping");
            await unzip(zip, path.join(homeDir, requestedVersion));
            let bbName = path.join(homeDir, requestedVersion, "bb");
            if (await fsExists(bbName)) {
                var stat = await fsStat(bbName);
                await fsChmod(bbName, stat.mode | parseInt("110", 8));
            }
        } else {
            console.log(
                "Bobril-build core is currently not available on your platform. Please help with porting it."
            );
            console.log(
                "Not found " +
                platformAssetName +
                " in " +
                assets.map(a => a.name).join(", ")
            );
            process.exit(1);
        }
    }
    try {
        await fsWriteFile(path.join(toRun, ".success"), "", "utf-8");
    } catch (err) {
        console.log(
            "Ignoring failure to update " +
            path.join(toRun, ".success") +
            " " +
            err
        );
    }
    console.log("Bobril-build core running " + toRun);
    let proc = child_process.spawn(
        path.join(toRun, "bb"),
        process.argv.slice(2),
        { stdio: "inherit" }
    );
    proc.on("exit", (code: number) => {
        process.exit(code);
    });
})();
