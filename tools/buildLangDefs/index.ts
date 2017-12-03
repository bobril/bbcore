import * as pathUtils from "./pathUtils";
import * as pathPlatformDependent from "path";
const path = pathPlatformDependent.posix; // This works everywhere, just use forward slashes
import * as fs from "fs";
import * as uglify from "uglify-js";

function numeralJsPath(): string {
    return pathUtils.dirOfNodeModule("numeral");
}

function momentJsPath(): string {
    return pathUtils.dirOfNodeModule("moment");
}

function findLocaleFile(filePath: string, locale: string, ext: string): string | null {
    while (true) {
        if (fs.existsSync(path.join(filePath, locale + ext))) {
            return path.join(filePath, locale + ext);
        }
        let dashPos = locale.lastIndexOf("-");
        if (dashPos < 0) return null;
        locale = locale.substr(0, dashPos);
    }
}

const pluralFns = require("make-plural/umd/plurals.min");

function getLanguageFromLocale(locale: string): string {
    let idx = locale.indexOf("-");
    if (idx >= 0) return locale.substr(0, idx);
    return locale;
}

function buildStartOfTranslationFile(locale: string) {
    let resbufs: Buffer[] = [];
    if (locale === "en" || /^en-us/i.test(locale)) {
        // English is always included
    } else {
        let fn = findLocaleFile(path.join(momentJsPath(), "locale"), locale, ".js");
        if (fn) {
            let src = fs.readFileSync(fn).toString("utf-8");
            src = src.replace(
                /;\(function \(global, factory\) \{[\s\S]*?'use strict';/,
                "(function (moment){"
            );
            src = src.substr(0, src.lastIndexOf("return"));
            src = src.replace(/var \S*? = moment.defineLocale\(/, "moment.defineLocale(");
            src = src + "})(moment);";
            src = uglify.minify(src, { output: { comments: /^!/ } as any }).code;
            resbufs.push(new Buffer(src, "utf-8"));
            resbufs.push(new Buffer("\n", "utf-8"));
        }
    }
    resbufs.push(new Buffer("bobrilRegisterTranslations('" + locale + "',[", "utf-8"));
    let pluralFn = pluralFns[getLanguageFromLocale(locale)];
    if (pluralFn) {
        resbufs.push(new Buffer(pluralFn.toString(), "utf-8"));
    } else {
        resbufs.push(new Buffer("function(){return'other';}", "utf-8"));
    }
    let fn = findLocaleFile(path.join(numeralJsPath(), "min", "languages"), locale, ".min.js");
    let td = ",";
    let dd = ".";
    if (fn) {
        let c = fs.readFileSync(fn, "utf-8");
        let m = /thousands:"(.)",decimal:"(.)"/.exec(c);
        if (m != null) {
            td = m[1];
            dd = m[2];
        }
    }
    resbufs.push(new Buffer(`,"${td}","${dd}"`, "utf-8"));
    resbufs.push(new Buffer("],", "utf-8"));
    return Buffer.concat(resbufs).toString("utf-8");
}

let dir = path.join(momentJsPath(), "locale");
let list = fs.readdirSync(dir);
let res = Object.create(null);
for (let i = 0; i < list.length; i++) {
    if (/\.js$/.test(list[i])) {
        let locale = list[i].substring(0, list[i].length - 3);
        res[locale] = buildStartOfTranslationFile(locale);
    }
}
res["en"] = buildStartOfTranslationFile("en");
res["en-us"] = buildStartOfTranslationFile("en-us");

fs.writeFileSync("localeDefs.json", JSON.stringify(res));
