"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var pathUtils = require("./pathUtils");
var pathPlatformDependent = require("path");
var path = pathPlatformDependent.posix; // This works everywhere, just use forward slashes
var fs = require("fs");
var uglify = require("uglify-js");
function numeralJsPath() {
    return pathUtils.dirOfNodeModule("numeral");
}
function momentJsPath() {
    return pathUtils.dirOfNodeModule("moment");
}
function findLocaleFile(filePath, locale, ext) {
    while (true) {
        if (fs.existsSync(path.join(filePath, locale + ext))) {
            return path.join(filePath, locale + ext);
        }
        var dashPos = locale.lastIndexOf("-");
        if (dashPos < 0)
            return null;
        locale = locale.substr(0, dashPos);
    }
}
var pluralFns = require("make-plural/umd/plurals.min");
function getLanguageFromLocale(locale) {
    var idx = locale.indexOf("-");
    if (idx >= 0)
        return locale.substr(0, idx);
    return locale;
}
function buildStartOfTranslationFile(locale) {
    var resbufs = [];
    if (locale === "en" || /^en-us/i.test(locale)) {
        // English is always included
    }
    else {
        var fn_1 = findLocaleFile(path.join(momentJsPath(), "locale"), locale, ".js");
        if (fn_1) {
            var src = fs.readFileSync(fn_1).toString("utf-8");
            src = src.replace(/;\(function \(global, factory\) \{[\s\S]*?'use strict';/, "(function (moment){");
            src = src.substr(0, src.lastIndexOf("return"));
            src = src.replace(/var \S*? = moment.defineLocale\(/, "moment.defineLocale(");
            src = src + "})(moment);";
            src = uglify.minify(src, { output: { comments: /^!/ } }).code;
            resbufs.push(new Buffer(src, "utf-8"));
            resbufs.push(new Buffer("\n", "utf-8"));
        }
    }
    resbufs.push(new Buffer("bobrilRegisterTranslations('" + locale + "',[", "utf-8"));
    var pluralFn = pluralFns[getLanguageFromLocale(locale)];
    if (pluralFn) {
        resbufs.push(new Buffer(pluralFn.toString(), "utf-8"));
    }
    else {
        resbufs.push(new Buffer("function(){return'other';}", "utf-8"));
    }
    var fn = findLocaleFile(path.join(numeralJsPath(), "min", "languages"), locale, ".min.js");
    var td = ",";
    var dd = ".";
    if (fn) {
        var c = fs.readFileSync(fn, "utf-8");
        var m = /thousands:"(.)",decimal:"(.)"/.exec(c);
        if (m != null) {
            td = m[1];
            dd = m[2];
        }
    }
    resbufs.push(new Buffer(",\"" + td + "\",\"" + dd + "\"", "utf-8"));
    resbufs.push(new Buffer("],", "utf-8"));
    return Buffer.concat(resbufs).toString("utf-8");
}
var dir = path.join(momentJsPath(), "locale");
var list = fs.readdirSync(dir);
var res = Object.create(null);
for (var i = 0; i < list.length; i++) {
    if (/\.js$/.test(list[i])) {
        var locale = list[i].substring(0, list[i].length - 3);
        res[locale] = buildStartOfTranslationFile(locale);
    }
}
res["en"] = buildStartOfTranslationFile("en");
res["en-us"] = buildStartOfTranslationFile("en-us");
fs.writeFileSync("localeDefs.json", JSON.stringify(res));
