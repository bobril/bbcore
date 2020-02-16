"use strict";
(function () {
    var jasmine = jasmineRequire.core(jasmineRequire);
    window["jasmine"] = jasmine;
    var env = jasmine.getEnv();
    var jasmineInterface = jasmineRequire.interface(jasmine, env);
    for (var property in jasmineInterface)
        window[property] = jasmineInterface[property];
    function _inspect(arg, within) {
        var result = "";
        if (Object(arg) !== arg) {
            if (within && typeof arg == "string") {
                return '"' + arg + '"';
            }
            return arg;
        }
        if (arg && arg.nodeType == 1) {
            // Is element?
            result = "<" + arg.tagName;
            for (var i = 0, ii = arg.attributes.length; i < ii; i++) {
                if (arg.attributes[i].specified) {
                    result += " " + arg.attributes[i].name + '="' + arg.attributes[i].value + '"';
                }
            }
            if (arg.childNodes && arg.childNodes.length === 0) {
                result += "/";
            }
            return result + ">";
        }
        var kind = Object.prototype.toString.call(arg).slice(8, -1);
        switch (kind) {
            case "String":
                return 'String "' + arg + '"';
            case "Number":
            case "Boolean":
                return kind + " " + arg;
            case "Array":
            case "HTMLCollection":
            case "NodeList":
                // Is array-like object?
                result = kind == "Array" ? "[" : kind + " [";
                var arr_list = [];
                for (var j = 0, jj = arg.length; j < jj; j++) {
                    arr_list[j] = _inspect(arg[j], true);
                }
                return result + arr_list.join(", ") + "]";
            case "Function":
            case "Date":
                return arg;
            case "RegExp":
                return "/" + arg.source + "/";
            default:
                if (typeof arg === "object") {
                    var prefix;
                    if (kind == "Object") {
                        prefix = "";
                    }
                    else {
                        prefix = kind + " ";
                    }
                    if (within) {
                        return prefix + "{?}";
                    }
                    if (Object.getOwnPropertyNames) {
                        var keys = Object.getOwnPropertyNames(arg);
                    }
                    else {
                        keys = [];
                        for (var key in arg) {
                            if (arg.hasOwnProperty(key)) {
                                keys.push(key);
                            }
                        }
                    }
                    result = prefix + "{";
                    if (!keys.length) {
                        return result + "}";
                    }
                    keys = keys.sort();
                    var properties = [];
                    for (var n = 0, nn = keys.length; n < nn; n++) {
                        key = keys[n];
                        try {
                            var value = _inspect(arg[key], true);
                            properties.push(key + ": " + value);
                        }
                        catch (e) { }
                    }
                    return result + properties.join(", ") + "}";
                }
                else {
                    return arg;
                }
        }
    }
    function repeatString(text, times) {
        if (times < 1) {
            return "";
        }
        var result = text;
        for (var i = times; --i;) {
            result += text;
        }
        return result;
    }
    var _indent = "  ";
    function primitiveOf(object) {
        var value = object.valueOf();
        switch (typeof value) {
            case "object":
                return "";
            case "string":
                return '"' + value + '"';
            default:
                return String(value);
        }
    }
    function source_of(arg, limit, stack) {
        var aType = typeof arg;
        switch (aType) {
            case "string":
                return '"' + arg + '"';
            case "function":
                break;
            case "object":
                if (arg === null) {
                    return "null";
                }
                break;
            default:
                return String(arg);
        }
        var prefix;
        var kind = Object.prototype.toString.call(arg).slice(8, -1);
        if (kind == "Object") {
            prefix = "";
        }
        else {
            prefix = kind + " ";
            var primitive = primitiveOf(arg);
            if (primitive) {
                prefix += primitive + " ";
            }
        }
        if (!limit) {
            return prefix + "{?}";
        }
        // Check circular references
        var stack_length = stack.length;
        for (var si = 0; si < stack_length; si++) {
            if (stack[si] === arg) {
                return "#";
            }
        }
        stack[stack_length++] = arg;
        var indent = repeatString(_indent, stack_length);
        if (Object.getOwnPropertyNames) {
            var keys = Object.getOwnPropertyNames(arg);
        }
        else {
            keys = [];
            for (var key in arg) {
                keys.push(key);
            }
        }
        var result = prefix + "{";
        if (!keys.length) {
            return result + "}";
        }
        keys = keys.sort();
        var arr_obj = [];
        for (var n = 0, nn = keys.length; n < nn; n++) {
            key = keys[n];
            try {
                var value = source_of(arg[key], limit - 1, stack);
                arr_obj.push("\n" + indent + key + ": " + value);
            }
            catch (e) { }
        }
        return result + arr_obj.join(", ") + "\n" + repeatString(_indent, stack_length - 1) + "}";
    }
    var testId = window.location.hash;
    function realLog(message) {
        var stack;
        var err = new Error();
        stack = err.stack || err.stacktrace;
        if (!stack) {
            try {
                i.crash.fast++;
            }
            catch (err) {
                stack = err.stack || err.stacktrace;
            }
        }
        bbTest("consoleLog" + testId, { message: message, stack: stack });
    }
    var bbTest = window.parent.bbTest;
    if (bbTest) {
        var specFilter = window.parent.specFilter;
        var specFilterFnc = function (_spec) { return true; };
        if (specFilter) {
            var specFilterRegExp = new RegExp(specFilter);
            specFilterFnc = function (spec) { return specFilterRegExp.test(spec.getFullName()); };
        }
        var config = {
            failFast: false,
            oneFailurePerSpec: true,
            hideDisabled: false,
            specFilter: specFilterFnc
        };
        onerror = (function (msg, _url, _lineNo, _columnNo, error) {
            bbTest("onerror" + testId, { message: msg, stack: error.stack });
        });
        env.configure(config);
        var perfnow;
        if (window.performance) {
            var p_1 = window.performance;
            perfnow = p_1.now || p_1.webkitNow || p_1.msNow || p_1.mozNow;
            if (perfnow) {
                var realnow_1 = perfnow;
                perfnow = function () { return realnow_1.call(p_1); };
            }
        }
        else if (Date.now) {
            perfnow = Date.now;
        }
        else {
            perfnow = function () { return +new Date(); };
        }
        var stack_1 = [];
        var specStart_1 = 0;
        var totalStart_1 = 0;
        env.addReporter({
            jasmineStarted: function (suiteInfo) {
                bbTest("wholeStart" + testId, suiteInfo.totalSpecsDefined);
                totalStart_1 = perfnow();
            },
            jasmineDone: function () {
                bbTest("wholeDone" + testId, perfnow() - totalStart_1);
                var cov = window.__c0v;
                if (cov != undefined) {
                    var pos_1 = 0;
                    var sendPart_1 = function () {
                        while (pos_1 < cov.length && cov[pos_1] === 0)
                            pos_1++;
                        if (pos_1 < cov.length) {
                            var maxlen = Math.min(cov.length - pos_1, 1024);
                            var len = maxlen - 1;
                            while (cov[pos_1 + len] === 0)
                                len--;
                            len++;
                            bbTest("coverageReportPart" + testId, {
                                start: pos_1,
                                data: Array.prototype.slice.call(cov.slice(pos_1, pos_1 + len))
                            });
                            pos_1 += maxlen;
                            setTimeout(sendPart_1, 10);
                        }
                        else {
                            bbTest("coverageReportFinished" + testId, { length: cov.length });
                        }
                    };
                    bbTest("coverageReportStarted" + testId, { length: cov.length });
                    setTimeout(sendPart_1, 10);
                }
            },
            suiteStarted: function (result) {
                bbTest("suiteStart" + testId, result.description);
                stack_1.push(perfnow());
            },
            specStarted: function (result) {
                bbTest("testStart" + testId, { name: result.description, stack: result.stack });
                specStart_1 = perfnow();
            },
            specDone: function (result) {
                var duration = perfnow() - specStart_1;
                bbTest("testDone" + testId, {
                    name: result.description,
                    duration: duration,
                    status: result.status,
                    failures: result.failedExpectations
                });
            },
            suiteDone: function (result) {
                var duration = perfnow() - stack_1.pop();
                bbTest("suiteDone" + testId, {
                    name: result.description,
                    duration: duration,
                    status: result.status,
                    failures: result.failedExpectations
                });
            }
        });
        // Heavily inspired by https://github.com/NV/console.js
        if (typeof console === "undefined") {
            window.console = {
                toString: function () {
                    return "Inspired by Console.js version 0.9";
                }
            };
        }
        var dimensions_limit_1 = 3;
        console.dir = function dir( /* ...arguments */) {
            var result = [];
            for (var i = 0; i < arguments.length; i++) {
                result.push(source_of(arguments[i], dimensions_limit_1, []));
            }
            return realLog(result.join(_args_separator_1));
        };
        var log_methods = ["log", "info", "warn", "error", "debug", "dirxml"];
        var _args_separator_1 = "\n";
        var _interpolate_1 = /%[sdifo]/gi;
        for (var i = 0; i < log_methods.length; i++) {
            console[log_methods[i]] = function logger(first_arg) {
                var result = [];
                var args = Array.prototype.slice.call(arguments, 0);
                if (typeof first_arg === "string" && _interpolate_1.test(first_arg)) {
                    args.shift();
                    result.push(first_arg.replace(_interpolate_1, function () {
                        return _inspect(args.shift());
                    }));
                }
                for (var i = 0; i < args.length; i++) {
                    result.push(_inspect(args[i]));
                }
                return realLog(result.join(_args_separator_1));
            };
        }
        console.trace = function trace() {
            realLog("trace");
        };
        console.assert = function assert(is_ok, message) {
            if (!is_ok)
                realLog("ASSERT FAIL: " + message);
        };
        console.group = function group(name) {
            realLog("\n-------- " + name + " --------");
        };
        console.groupCollapsed = console.group;
        console.groupEnd = function groupEnd() {
            realLog("\n\n\n");
        };
        var _counters_1 = {};
        console.count = function count(title) {
            title = title || "";
            if (_counters_1[title]) {
                _counters_1[title]++;
            }
            else {
                _counters_1[title] = 1;
            }
            realLog(title + " " + _counters_1[title]);
        };
        var _timers_1 = {};
        console.time = function time(name) {
            var start = new Date().getTime();
            _timers_1[name] = {
                start: start
            };
        };
        console.timeEnd = function timeEnd(name) {
            var end = new Date().getTime();
            console.info(name + ": " + (end - _timers_1[name].start) + "ms");
            _timers_1[name].end = end;
        };
    }
    else {
        var config = {
            failFast: true,
            oneFailurePerSpec: true,
            hideDisabled: false,
            specFilter: function (_spec) { return true; }
        };
        env.configure(config);
        env.addReporter({
            jasmineStarted: function (suiteInfo) {
                console.log("Started " + suiteInfo.totalSpecsDefined);
            },
            jasmineDone: function () {
                console.log("Done");
            },
            suiteStarted: function (result) {
                console.log("Suite " + result.fullName);
            },
            specStarted: function (result) {
                console.log("Spec " + result.fullName);
            },
            specDone: function (result) {
                console.log("Spec finished " + result.status);
            },
            suiteDone: function (result) {
                console.log("Suite finished " + result.status);
            }
        });
    }
    /**
     * Setting up timing functions to be able to be overridden. Certain browsers (Safari, IE 8, phantomjs) require this hack.
     */
    window.setTimeout = window.setTimeout;
    window.setInterval = window.setInterval;
    window.clearTimeout = window.clearTimeout;
    window.clearInterval = window.clearInterval;
    window.onload = function () {
        env.execute();
    };
})();
