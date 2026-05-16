import * as b from "bobril";

import moment from "moment";

import * as msgFormatParser from "./msgFormatParser";

import * as msgFormatter from "./msgFormatter";

import { jsonp } from "./jsonp";

import * as localeDataStorage from "./localeDataStorage";

import * as numberFormatter from "./numberFormatter";

let noEval = false;

export function setNoEval(value = true) {
    noEval = value;
}

let spyTranslationFunc;

let cfg = {
    defaultLocale: "en-US",
    pathToTranslation: () => undefined,
    runScriptAsync: jsonp
};

let failedToLoadLocales = new Set();

let loadedLocales = new Set();

export let registeredTranslations = Object.create(null);

let currentLocale = "";

let currentRules = localeDataStorage.getRules("en");

let currentUnformatter;

let currentTranslations = [];

let currentCachedFormat = [];

let stringCachedFormats = new Map();

let keysByTranslationId = undefined;

let key2TranslationId = undefined;

if (window.g11nPath) {
    cfg.pathToTranslation = window.g11nPath;
}

if (window.g11nLoc) {
    cfg.defaultLocale = window.g11nLoc;
}

function currentTranslationMessage(message) {
    let text = currentTranslations[message];
    if (text === undefined) {
        throw new Error("Message " + message + " is not defined. Current locale: " + currentLocale + " Loaded locales: " + Array.from(loadedLocales).join(", ") + " Failed to load locales: " + Array.from(failedToLoadLocales).join(", "));
    }
    return text;
}

function spyTranslatedString(translated) {
    if (spyTranslationFunc === undefined) return translated;
    return spyTranslationFunc(translated);
}

export function t(message, params, _translationHelp) {
    if (currentLocale.length === 0) {
        throw new Error("before using t you need to wait for initialization of g11n");
    }
    let format;
    if (Array.isArray(message)) {
        if (typeof message[0] === "string") {
            return formatSerializedMessage(message);
        }
        return formatDelayedMessage(message);
    }
    if (typeof message === "number") {
        if (params == null) {
            return spyTranslatedString(currentTranslationMessage(message));
        }
        format = currentCachedFormat[message];
        if (format === undefined) {
            let ast = msgFormatParser.parse(currentTranslationMessage(message));
            if (msgFormatParser.isParserError(ast)) {
                throw new Error("message " + message + " in " + currentLocale + " has error: " + ast.msg);
            }
            format = msgFormatter.compile(currentLocale, ast, noEval);
            currentCachedFormat[message] = format;
        }
    } else {
        if (params == null) return spyTranslatedString(message);
        format = stringCachedFormats.get(message);
        if (format === undefined) {
            let ast = msgFormatParser.parse(message);
            if (msgFormatParser.isParserError(ast)) {
                throw new Error('message "' + message + '" has error: ' + ast.msg + " on position: " + ast.pos);
            }
            format = msgFormatter.compile(currentLocale, ast, noEval);
            stringCachedFormats.set(message, format);
        }
    }
    return spyTranslatedString(format(params));
}

export function dt(message, params, _translationHelp) {
    if (params == undefined) return [ message ];
    return [ message, params ];
}

let lazyLoadKeys = undefined;

export function loadSerializationKeys() {
    if (lazyLoadKeys === undefined) {
        lazyLoadKeys = cfg.runScriptAsync(cfg.pathToTranslation("l10nkeys")).then(b.ignoreShouldChange);
    }
    return lazyLoadKeys;
}

export function serializationKeysLoaded() {
    return keysByTranslationId != undefined;
}

const HOP = {}.hasOwnProperty;

function serializeParams(params) {
    if (params == undefined) return params;
    let needSerialization = false;
    for (const key in params) {
        if (HOP.call(params, key)) {
            const element = params[key];
            if (Array.isArray(element)) {
                needSerialization = true;
                break;
            }
        }
    }
    if (!needSerialization) return params;
    let res = {};
    for (const key in params) {
        if (HOP.call(params, key)) {
            let element = params[key];
            if (Array.isArray(element)) {
                element = serializeMessage(element);
            }
            res[key] = element;
        }
    }
    return res;
}

export function serializeMessage(message) {
    if (keysByTranslationId === undefined) throw new Error("Make sure to await loadSerializationKeys");
    let m = message[0];
    if (typeof m == "string") {
        if (message.length === 1) {
            if (key2TranslationId.has(m) || m.endsWith("\t0")) return message;
            return [ m + "\t0" ];
        }
        if (key2TranslationId.has(m) || m.endsWith("\t1")) return message;
        return [ m + "\t1", serializeParams(message[1]) ];
    }
    let key = keysByTranslationId[m];
    if (message.length == 1) return [ key ];
    return [ key, serializeParams(message[1]) ];
}

export function formatDelayedMessage(message) {
    return t(message[0], message[1]);
}

export function deserializeMessage(message) {
    let id = undefined;
    if (!serializationKeysLoaded()) {
        loadSerializationKeys();
    } else {
        id = key2TranslationId.get(message[0]);
    }
    if (id === undefined) {
        id = message[0];
        id = id.substr(0, id.lastIndexOf("\t"));
    }
    if (message.length === 1) {
        return [ id ];
    }
    return [ id, message[1] ];
}

export function formatSerializedMessage(message) {
    return formatDelayedMessage(deserializeMessage(message));
}

export function f(message, params) {
    if (typeof message !== "object" && params === undefined) return message;
    return t(message, params);
}

let initPromise = Promise.resolve(undefined).then(() => setLocale(cfg.defaultLocale));

b.setBeforeInit(cb => {
    initPromise.then(cb, cb);
});

export function initGlobalization(config) {
    Object.assign(cfg, config);
    if (currentLocale.length !== 0) {
        if (!loadedLocales.has(currentLocale)) {
            currentLocale = "";
        }
        return setLocale(cfg.defaultLocale);
    }
    return initPromise;
}

export async function setLocale(locale) {
    if (currentLocale === locale) return;
    var lcLocale = locale.toLowerCase();
    if (!loadedLocales.has(lcLocale)) {
        let pathToTranslation = cfg.pathToTranslation;
        if (pathToTranslation) {
            let p = pathToTranslation(locale);
            if (p) {
                try {
                    await cfg.runScriptAsync(p);
                    if (!loadedLocales.has(lcLocale)) {
                        throw Error("Locale " + locale + " was not loaded correctly");
                    }
                } catch (e) {
                    console.warn(e);
                    failedToLoadLocales.add(locale);
                    if (locale != cfg.defaultLocale) {
                        await setLocale(cfg.defaultLocale);
                    }
                    throw e;
                }
            }
        }
    }
    currentLocale = locale;
    currentRules = localeDataStorage.getRules(lcLocale);
    currentTranslations = registeredTranslations[lcLocale] || [];
    currentUnformatter = undefined;
    currentCachedFormat = [];
    currentCachedFormat.length = currentTranslations.length;
    stringCachedFormats = new Map();
    moment.locale(currentLocale);
    b.ignoreShouldChange();
}

export function getLocale() {
    return currentLocale;
}

export const getMoment = moment;

export function unformatNumber(str) {
    if (currentUnformatter === undefined) {
        currentUnformatter = numberFormatter.buildUnformat(currentRules);
    }
    return currentUnformatter(str);
}

export function registerTranslations(locale, localeDefs, msgs) {
    if (locale == "") {
        keysByTranslationId = msgs;
        key2TranslationId = new Map();
        for (let i = 0; i < msgs.length; i++) {
            key2TranslationId.set(msgs[i], i);
        }
        return;
    }
    locale = locale.toLowerCase();
    if (Array.isArray(localeDefs)) {
        localeDataStorage.setRules(locale, localeDefs);
    }
    if (Array.isArray(msgs)) registeredTranslations[locale] = msgs;
    loadedLocales.add(locale);
}

export function spyTranslation(spyFn) {
    if (spyFn === undefined) return spyTranslationFunc;
    if (spyFn === null) {
        spyTranslationFunc = undefined;
    } else {
        spyTranslationFunc = spyFn;
    }
    return spyTranslationFunc;
}

if (window) {
    window["bobrilRegisterTranslations"] = registerTranslations;
    if (window["b"] != null) window["b"].spyTr = spyTranslation;
}

export function T(data) {
    return data;
}

