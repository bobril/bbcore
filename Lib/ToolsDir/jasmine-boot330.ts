declare var jasmineRequire: any;

(function() {
    var jasmine = jasmineRequire.core(jasmineRequire);
    (<any>window)["jasmine"] = jasmine;

    var env = jasmine.getEnv();

    var jasmineInterface = jasmineRequire.interface(jasmine, env);
    for (var property in jasmineInterface) (<any>window)[property] = jasmineInterface[property];

    function _inspect(arg: any, within?: boolean): string {
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
                    } else {
                        prefix = kind + " ";
                    }
                    if (within) {
                        return prefix + "{?}";
                    }
                    if (Object.getOwnPropertyNames) {
                        var keys = Object.getOwnPropertyNames(arg);
                    } else {
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
                        } catch (e) {}
                    }
                    return result + properties.join(", ") + "}";
                } else {
                    return arg;
                }
        }
    }

    function repeatString(text: string, times: number) {
        if (times < 1) {
            return "";
        }
        var result = text;
        for (var i = times; --i; ) {
            result += text;
        }
        return result;
    }

    const _indent = "  ";

    function primitiveOf(object: any) {
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

    function source_of(arg: any, limit: number, stack: any[]): string {
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
        } else {
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
        } else {
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
            } catch (e) {}
        }
        return result + arr_obj.join(", ") + "\n" + repeatString(_indent, stack_length - 1) + "}";
    }

    let testId = window.location.hash;

    function realLog(message: string) {
        let stack: string;
        let err = <any>new Error();
        stack = err.stack || err.stacktrace;
        if (!stack) {
            try {
                (<any>i).crash.fast++;
            } catch (err) {
                stack = err.stack || err.stacktrace;
            }
        }
        bbTest("consoleLog" + testId, { message, stack });
    }

    var bbTest = (<any>window.parent).bbTest;
    if (bbTest) {
        var specFilter = (<any>window.parent).specFilter;
        var specFilterFnc = (_spec: any) => true;
        if (specFilter) {
            var specFilterRegExp = new RegExp(specFilter);
            specFilterFnc = (spec: any) => specFilterRegExp.test(spec.getFullName());
        }
        var config = {
            failFast: false,
            oneFailurePerSpec: true,
            hideDisabled: false,
            specFilter: specFilterFnc
        };
        onerror = ((msg: string, _url: string, _lineNo: number, _columnNo: number, error: Error) => {
            bbTest("onerror" + testId, { message: msg, stack: error.stack });
        }) as any;
        env.configure(config);
        var perfnow: () => number;
        if (window.performance) {
            let p = <any>window.performance;
            perfnow = p.now || p.webkitNow || p.msNow || p.mozNow;
            if (perfnow) {
                let realnow = perfnow;
                perfnow = () => realnow.call(p);
            }
        } else if (Date.now) {
            perfnow = Date.now;
        } else {
            perfnow = () => +new Date();
        }

        let stack: number[] = [];
        let specStart = 0;
        let totalStart = 0;
        env.addReporter({
            jasmineStarted: (suiteInfo: { totalSpecsDefined: number }) => {
                bbTest("wholeStart" + testId, suiteInfo.totalSpecsDefined);
                totalStart = perfnow();
            },
            jasmineDone: () => {
                bbTest("wholeDone" + testId, perfnow() - totalStart);
                var cov = (window as any).__c0v as Uint32Array;
                if (cov != undefined) {
                    let pos = 0;
                    const sendPart = () => {
                        while (pos < cov.length && cov[pos] === 0) pos++;
                        if (pos < cov.length) {
                            let maxlen = Math.min(cov.length - pos, 1024);
                            let len = maxlen - 1;
                            while (cov[pos + len] === 0) len--;
                            len++;
                            bbTest("coverageReportPart" + testId, {
                                start: pos,
                                data: Array.prototype.slice.call(cov.slice(pos, pos + len))
                            });
                            pos += maxlen;
                            setTimeout(sendPart, 10);
                        } else {
                            bbTest("coverageReportFinished" + testId, { length: cov.length });
                        }
                    };
                    bbTest("coverageReportStarted" + testId, { length: cov.length });
                    setTimeout(sendPart, 10);
                }
            },
            suiteStarted: (result: { description: string; fullName: string }) => {
                bbTest("suiteStart" + testId, result.description);
                stack.push(perfnow());
            },
            specStarted: (result: { description: string; stack: string }) => {
                bbTest("testStart" + testId, { name: result.description, stack: result.stack });
                specStart = perfnow();
            },
            specDone: (result: {
                description: string;
                status: string;
                failedExpectations: { message: string; stack: string }[];
            }) => {
                let duration = perfnow() - specStart;
                bbTest("testDone" + testId, {
                    name: result.description,
                    duration,
                    status: result.status,
                    failures: result.failedExpectations
                });
            },
            suiteDone: (result: {
                description: string;
                status: string;
                failedExpectations: { message: string; stack: string }[];
            }) => {
                let duration = perfnow() - stack.pop()!;
                bbTest("suiteDone" + testId, {
                    name: result.description,
                    duration,
                    status: result.status,
                    failures: result.failedExpectations
                });
            }
        });

        // Heavily inspired by https://github.com/NV/console.js
        if (typeof console === "undefined") {
            (<any>window).console = <any>{
                toString: function() {
                    return "Inspired by Console.js version 0.9";
                }
            };
        }

        let dimensions_limit = 3;

        console.dir = function dir(/* ...arguments */) {
            var result = [];
            for (var i = 0; i < arguments.length; i++) {
                result.push(source_of(arguments[i], dimensions_limit, []));
            }
            return realLog(result.join(_args_separator));
        };

        var log_methods = ["log", "info", "warn", "error", "debug", "dirxml"];

        const _args_separator = "\n";
        const _interpolate = /%[sdifo]/gi;

        for (var i = 0; i < log_methods.length; i++) {
            (<any>console)[log_methods[i]] = function logger(first_arg: any) {
                var result = [];
                var args = Array.prototype.slice.call(arguments, 0);
                if (typeof first_arg === "string" && _interpolate.test(first_arg)) {
                    args.shift();
                    result.push(
                        first_arg.replace(_interpolate, function() {
                            return _inspect(args.shift());
                        })
                    );
                }
                for (var i = 0; i < args.length; i++) {
                    result.push(_inspect(args[i]));
                }
                return realLog(result.join(_args_separator));
            };
        }

        console.trace = function trace() {
            realLog("trace");
        };

        console.assert = function assert(is_ok: boolean, message: string) {
            if (!is_ok) realLog("ASSERT FAIL: " + message);
        };

        console.group = function group(name: string) {
            realLog("\n-------- " + name + " --------");
        };

        console.groupCollapsed = console.group;

        console.groupEnd = function groupEnd() {
            realLog("\n\n\n");
        };

        let _counters: { [name: string]: number } = {};

        console.count = function count(title) {
            title = title || "";
            if (_counters[title]) {
                _counters[title]++;
            } else {
                _counters[title] = 1;
            }
            realLog(title + " " + _counters[title]);
        };

        let _timers: { [name: string]: any } = {};

        console.time = function time(name: string) {
            var start = new Date().getTime();
            _timers[name] = {
                start: start
            };
        };

        console.timeEnd = function timeEnd(name: string) {
            var end = new Date().getTime();
            console.info(name + ": " + (end - _timers[name].start) + "ms");
            _timers[name].end = end;
        };
    } else {
        var config = {
            failFast: true,
            oneFailurePerSpec: true,
            hideDisabled: false,
            specFilter: (_spec: any) => true
        };
        env.configure(config);

        env.addReporter({
            jasmineStarted: (suiteInfo: { totalSpecsDefined: number }) => {
                console.log("Started " + suiteInfo.totalSpecsDefined);
            },
            jasmineDone: () => {
                console.log("Done");
            },
            suiteStarted: (result: { description: string; fullName: string }) => {
                console.log("Suite " + result.fullName);
            },
            specStarted: (result: { description: string; fullName: string }) => {
                console.log("Spec " + result.fullName);
            },
            specDone: (result: {
                description: string;
                status: string;
                failedExpectations: { message: string; stack: any }[];
            }) => {
                console.log("Spec finished " + result.status);
            },
            suiteDone: (result: {
                description: string;
                status: string;
                failedExpectations: { message: string; stack: any }[];
            }) => {
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

    window.onload = function() {
        env.execute();
    };
})();
