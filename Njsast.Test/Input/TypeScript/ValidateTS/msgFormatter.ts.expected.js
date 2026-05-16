import moment from "moment";

import { RuntimeFunctionGenerator } from "./RuntimeFunctionGenerator";

import * as localeDataStorage from "./localeDataStorage";

import * as numberFormatter from "./numberFormatter";

import { MsgAst } from "./msgFormatParser";

import { f } from "./translate";

import { isString, isArray, isNumber } from "bobril";

window.moment = moment;

var numberFormatterCache = Object.create(null);

const customFormatters = new Map();

function getFormatter(locale, format, interpret) {
    const key = interpret + "|" + locale + "|" + format;
    let res = numberFormatterCache[key];
    if (res) return res;
    res = numberFormatter.buildFormatter(localeDataStorage.getRules(locale), format, interpret);
    numberFormatterCache[key] = res;
    return res;
}

function noFuture(m) {
    if (m.toDate() > new Date()) return moment(new Date());
    return m;
}

function formatWithOptionalSpace(value) {
    if (value == null || value === "") return "";
    return String(value) + " ";
}

function formatWithQuotedValue(value, locale) {
    if (value == null || value === "") return "";
    const rules = localeDataStorage.getRules(locale);
    return rules.oq + String(value) + rules.cq + " ";
}

export function registerCustomFormatter(name, fn) {
    customFormatters.set(name, fn);
}

function getCustomFormatter(name) {
    const formatter = customFormatters.get(name);
    if (formatter === undefined) throw new Error('Unknown custom formatter "' + name + '"');
    return formatter;
}

registerCustomFormatter("space", formatWithOptionalSpace);

registerCustomFormatter("quoted", formatWithQuotedValue);

function AnyFormatter(locale, type, style, options, interpret) {
    switch (type) {
      case "number":
        {
            if (style === "custom" && "format" in options) {
                if (options.format === null) return (val, opt) => {
                    return getFormatter(locale, opt.format, interpret)(val);
                };
                return getFormatter(locale, options.format, interpret);
            }
            if (style === "default") {
                return getFormatter(locale, "0,0.[0000]", interpret);
            }
            if (style === "percent") {
                return getFormatter(locale, "0%", interpret);
            }
            if (style === "bytes") {
                return getFormatter(locale, "0b", interpret);
            }
            break;
        }

      case "date":
      case "time":
        {
            if (style === "relative") {
                if (options["noago"] === true) {
                    return (val, _opt) => {
                        return moment(val).locale(locale).fromNow(true);
                    };
                }
                if (options["noago"] === null) {
                    return (val, opt) => {
                        return moment(val).locale(locale).fromNow(opt["noago"]);
                    };
                }
                return (val, _opt) => {
                    return moment(val).locale(locale).fromNow(false);
                };
            }
            if (style === "relativepast") {
                if (options["noago"] === true) {
                    return (val, _opt) => {
                        return noFuture(moment(val)).locale(locale).fromNow(true);
                    };
                }
                if (options["noago"] === null) {
                    return (val, opt) => {
                        return noFuture(moment(val)).locale(locale).fromNow(opt["noago"]);
                    };
                }
                return (val, _opt) => {
                    return noFuture(moment(val)).locale(locale).fromNow(false);
                };
            }
            if (style === "calendar") {
                return (val, _opt) => {
                    return moment(val).locale(locale).calendar();
                };
            }
            if (style === "custom" && "format" in options) {
                return (val, opt) => {
                    return moment(val).locale(locale).format(opt.format);
                };
            }
            return (val, _opt) => {
                return moment(val).locale(locale).format(style);
            };
        }
    }
    throw new Error("bad type in AnyFormatter");
}

export function compile(locale, msgAst, interpret = false) {
    if (interpret) {
        return (params, hashArg) => {
            if (isString(msgAst)) {
                return msgAst;
            }
            if (isArray(msgAst)) {
                if (msgAst.length === 0) return "";
                let res = "";
                for (let i = 0; i < msgAst.length; i++) {
                    let item = msgAst[i];
                    if (isString(item)) {
                        res += item;
                    } else {
                        res += compile(locale, item, true)(params, hashArg);
                    }
                }
                return res;
            }
            switch (msgAst.type) {
              case "arg":
                return f(params[msgAst.id]);

              case "hash":
                if (hashArg === undefined) return "#";
                return hashArg;

              case "concat":
                {
                    const vals = msgAst.values;
                    if (vals.length === 0) return "";
                    let res = [];
                    for (let i = 0; i < vals.length; i++) {
                        let item = vals[i];
                        if (isString(item)) {
                            res.push(item);
                        } else {
                            res.push(compile(locale, item, true)(params, hashArg));
                        }
                    }
                    return res;
                }

              case "el":
                if (msgAst.value != undefined) {
                    return params[msgAst.id](compile(locale, msgAst.value, true)(params, hashArg));
                }
                return params[msgAst.id]();

              case "format":
                var local = params[msgAst.id];
                let type = msgAst.format.type;
                switch (type) {
                  case "plural":
                    {
                        let localArgOffset = local - msgAst.format.offset;
                        let options = msgAst.format.options;
                        for (let i = 0; i < options.length; i++) {
                            let opt = options[i];
                            if (!isNumber(opt.selector)) continue;
                            if (opt.selector === localArgOffset) {
                                return compile(locale, opt.value, true)(params, "" + localArgOffset);
                            }
                        }
                        let localCase = localeDataStorage.getRules(locale).pluralFn(localArgOffset, !!msgAst.format.ordinal);
                        for (let i = 0; i < options.length; i++) {
                            let opt = options[i];
                            if (opt.selector === localCase) {
                                return compile(locale, opt.value, true)(params, "" + localArgOffset);
                            }
                        }
                        for (let i = 0; i < options.length; i++) {
                            let opt = options[i];
                            if (opt.selector !== "other") continue;
                            return compile(locale, opt.value, true)(params, "" + localArgOffset);
                        }
                        break;
                    }

                  case "select":
                    {
                        let options = msgAst.format.options;
                        for (let i = 0; i < options.length; i++) {
                            let opt = options[i];
                            if (!isString(opt.selector)) continue;
                            if (opt.selector === "other") continue;
                            if (opt.selector === local) {
                                return compile(locale, opt.value, true)(params, local);
                            }
                        }
                        for (let i = 0; i < options.length; i++) {
                            let opt = options[i];
                            if (opt.selector !== "other") continue;
                            return compile(locale, opt.value, true)(params, local);
                        }
                        break;
                    }

                  case "number":
                  case "date":
                  case "time":
                    {
                        let style = msgAst.format.style || "default";
                        let options = msgAst.format.options;
                        if (options) {
                            let opts = {};
                            let complex = false;
                            for (let i = 0; i < options.length; i++) {
                                let opt = options[i];
                                if (typeof opt.value === "object") {
                                    complex = true;
                                    opts[opt.key] = null;
                                } else {
                                    let val = opt.value;
                                    if (val === undefined) val = true;
                                    opts[opt.key] = val;
                                }
                            }
                            let formatFn = AnyFormatter(locale, type, style, opts, interpret);
                            if (complex) {
                                let optLocal = opts;
                                for (let i = 0; i < options.length; i++) {
                                    let opt = options[i];
                                    if (typeof opt.value === "object") {
                                        optLocal[opt.key] = compile(locale, opt.value, true)(params, hashArg);
                                    }
                                }
                                return formatFn(local, optLocal);
                            } else {
                                return formatFn(local, opts);
                            }
                        } else {
                            let formatFn = AnyFormatter(locale, type, style, {}, interpret);
                            return formatFn(local, {});
                        }
                    }
                }
                return getCustomFormatter(type)(local, locale);
            }
            throw new Error("invalid AST in compile");
        };
    }
    if (isString(msgAst)) {
        return () => msgAst;
    }
    if (isArray(msgAst)) {
        if (msgAst.length === 0) return () => "";
        let comp = new RuntimeFunctionGenerator();
        let argParams = comp.addArg(0);
        let argHash = comp.addArg(1);
        comp.addBody("return ");
        for (let i = 0; i < msgAst.length; i++) {
            if (i > 0) comp.addBody("+");
            let item = msgAst[i];
            if (typeof item === "string") {
                comp.addBody(comp.addConstant(item));
            } else {
                comp.addBody(comp.addConstant(compile(locale, item)), `(${argParams},${argHash})`);
            }
        }
        comp.addBody(";");
        return comp.build();
    }
    switch (msgAst.type) {
      case "arg":
        return (name => params => f(params[name]))(msgAst.id);

      case "hash":
        return (_params, hashArg) => {
            if (hashArg === undefined) return "#";
            return hashArg;
        };

      case "concat":
        {
            const vals = msgAst.values;
            if (vals.length === 0) return () => "";
            let comp = new RuntimeFunctionGenerator();
            let argParams = comp.addArg(0);
            let argHash = comp.addArg(1);
            comp.addBody("return [");
            for (let i = 0; i < vals.length; i++) {
                if (i > 0) comp.addBody(",");
                let item = vals[i];
                if (isString(item)) {
                    comp.addBody(comp.addConstant(item));
                } else {
                    comp.addBody(comp.addConstant(compile(locale, item)), `(${argParams},${argHash})`);
                }
            }
            comp.addBody("];");
            return comp.build();
        }

      case "el":
        if (msgAst.value != undefined) {
            return ((id, valueFactory) => (params, hashArg) => params[id](valueFactory(params, hashArg)))(msgAst.id, compile(locale, msgAst.value));
        }
        return (id => params => params[id]())(msgAst.id);

      case "format":
        let comp = new RuntimeFunctionGenerator();
        let argParams = comp.addArg(0);
        let localArg = comp.addLocal();
        comp.addBody(`var ${localArg}=${argParams}[${comp.addConstant(msgAst.id)}];`);
        let type = msgAst.format.type;
        switch (type) {
          case "plural":
            {
                let localArgOffset = comp.addLocal();
                comp.addBody(`var ${localArgOffset}=${localArg}-${msgAst.format.offset};`);
                let options = msgAst.format.options;
                for (let i = 0; i < options.length; i++) {
                    let opt = options[i];
                    if (!isNumber(opt.selector)) continue;
                    let fn = comp.addConstant(compile(locale, opt.value));
                    comp.addBody(`if (${localArgOffset}===${opt.selector}) return ${fn}(${argParams},''+${localArgOffset});`);
                }
                let localCase = comp.addLocal();
                let pluralFn = comp.addConstant(localeDataStorage.getRules(locale).pluralFn);
                comp.addBody(`var ${localCase}=${pluralFn}(${localArgOffset},${msgAst.format.ordinal ? "!0" : "!1"});`);
                for (let i = 0; i < options.length; i++) {
                    let opt = options[i];
                    if (!isString(opt.selector)) continue;
                    if (opt.selector === "other") continue;
                    let fn = comp.addConstant(compile(locale, opt.value));
                    comp.addBody(`if (${localCase}===${comp.addConstant(opt.selector)}) return ${fn}(${argParams},''+${localArgOffset});`);
                }
                for (let i = 0; i < options.length; i++) {
                    let opt = options[i];
                    if (opt.selector !== "other") continue;
                    let fn = comp.addConstant(compile(locale, opt.value));
                    comp.addBody(`return ${fn}(${argParams},''+${localArgOffset});`);
                }
                break;
            }

          case "select":
            {
                let options = msgAst.format.options;
                for (let i = 0; i < options.length; i++) {
                    let opt = options[i];
                    if (typeof opt.selector !== "string") continue;
                    if (opt.selector === "other") continue;
                    let fn = comp.addConstant(compile(locale, opt.value));
                    comp.addBody(`if (${localArg}===${comp.addConstant(opt.selector)}) return ${fn}(${argParams},${localArg});`);
                }
                for (let i = 0; i < options.length; i++) {
                    let opt = options[i];
                    if (opt.selector !== "other") continue;
                    let fn = comp.addConstant(compile(locale, opt.value));
                    comp.addBody(`return ${fn}(${argParams},${localArg});`);
                }
                break;
            }

          case "number":
          case "date":
          case "time":
            {
                let style = msgAst.format.style || "default";
                let options = msgAst.format.options;
                if (options) {
                    let opts = {};
                    let complex = false;
                    for (let i = 0; i < options.length; i++) {
                        let opt = options[i];
                        if (typeof opt.value === "object") {
                            complex = true;
                            opts[opt.key] = null;
                        } else {
                            let val = opt.value;
                            if (val === undefined) val = true;
                            opts[opt.key] = val;
                        }
                    }
                    let formatFn = comp.addConstant(AnyFormatter(locale, type, style, opts, interpret));
                    if (complex) {
                        let optConst = comp.addConstant(opts);
                        let optLocal = comp.addLocal();
                        let hashArg = comp.addArg(1);
                        comp.addBody(`var ${optLocal}=${optConst};`);
                        for (let i = 0; i < options.length; i++) {
                            let opt = options[i];
                            if (typeof opt.value === "object") {
                                let fnConst = comp.addConstant(compile(locale, opt.value));
                                comp.addBody(`${optLocal}[${comp.addConstant(opt.key)}]=${fnConst}(${argParams},${hashArg});`);
                            }
                        }
                        comp.addBody(`return ${formatFn}(${localArg},${optLocal});`);
                    } else {
                        comp.addBody(`return ${formatFn}(${localArg},${comp.addConstant(opts)});`);
                    }
                } else {
                    let formatFn = comp.addConstant(AnyFormatter(locale, type, style, {}, interpret));
                    comp.addBody(`return ${formatFn}(${localArg});`);
                }
                break;
            }

          default:
            {
                let formatFn = comp.addConstant(getCustomFormatter(type));
                comp.addBody(`return ${formatFn}(${localArg},${comp.addConstant(locale)});`);
                break;
            }
        }
        return comp.build();
    }
    throw new Error("invalid AST in compile");
}

