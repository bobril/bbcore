"use strict";
exports.transitionRunCount = exports.activeStyleDef = exports.RouteTransitionType = void 0;

exports.encodeUrl = encodeUrl;

exports.decodeUrl = decodeUrl;

exports.encodeUrlPath = encodeUrlPath;

exports.extractParams = extractParams;

exports.injectParams = injectParams;

exports.routes = routes;

exports.route = route;

exports.routeDefault = routeDefault;

exports.routeNotFound = routeNotFound;

exports.isActive = isActive;

exports.urlOfRoute = urlOfRoute;

exports.Link = Link;

exports.link = link;

exports.createRedirectPush = createRedirectPush;

exports.createRedirectReplace = createRedirectReplace;

exports.createBackTransition = createBackTransition;

exports.runTransition = runTransition;

exports.Anchor = Anchor;

exports.anchor = anchor;

exports.getRoutes = getRoutes;

exports.getActiveRoutes = getActiveRoutes;

exports.getActiveParams = getActiveParams;

exports.getActiveState = getActiveState;

exports.setActiveState = setActiveState;

exports.useCanDeactivate = useCanDeactivate;

const core_1 = require("./core");

const cssInJs_1 = require("./cssInJs");

const isFunc_1 = require("./isFunc");

const localHelpers_1 = require("./localHelpers");

const mouseEvents_1 = require("./mouseEvents");

var RouteTransitionType;

(function(RouteTransitionType) {
    RouteTransitionType[RouteTransitionType["Push"] = 0] = "Push";
    RouteTransitionType[RouteTransitionType["Replace"] = 1] = "Replace";
    RouteTransitionType[RouteTransitionType["Pop"] = 2] = "Pop";
})(RouteTransitionType || (exports.RouteTransitionType = RouteTransitionType = {}));

var waitingForPopHashChange = -1;

function emitOnHashChange() {
    if (waitingForPopHashChange >= 0) clearTimeout(waitingForPopHashChange);
    waitingForPopHashChange = -1;
    core_1.invalidate();
    return false;
}

core_1.addEvent("hashchange", 10, emitOnHashChange);

let historyDeepness = 0;

let programPath = "";

function history() {
    return window.history;
}

function push(path, inApp, state) {
    var l = window.location;
    if (inApp) {
        programPath = path;
        activeState = state;
        historyDeepness++;
        history().pushState({
            historyDeepness,
            state
        }, "", path);
        core_1.invalidate();
    } else {
        l.href = path;
    }
}

function replace(path, inApp, state) {
    var l = window.location;
    if (inApp) {
        programPath = path;
        activeState = state;
        history().replaceState({
            historyDeepness,
            state
        }, "", path);
        core_1.invalidate();
    } else {
        l.replace(path);
    }
}

function pop(distance) {
    waitingForPopHashChange = setTimeout(emitOnHashChange, 50);
    history().go(-distance);
}

let rootRoutes;

let nameRouteMap = {};

function encodeUrl(url) {
    return encodeURIComponent(url).replace(/%20/g, "+");
}

function decodeUrl(url) {
    try {
        return decodeURIComponent(url.replace(/\+/g, " "));
    } catch {
        return "";
    }
}

function encodeUrlPath(path) {
    return String(path).split("/").map(encodeUrl).join("/");
}

const paramCompileMatcher = /(\/?):([a-zA-Z_$][a-zA-Z0-9_$]*)([?]?)|[*.()\[\]\\+|{}^$]/g;

const paramInjectMatcher = /(\/?)(?::([a-zA-Z_$][a-zA-Z0-9_$?]*[?]?)|[*])/g;

let compiledPatterns = {};

function compilePattern(pattern) {
    if (!(pattern in compiledPatterns)) {
        var paramNames = [];
        var source = pattern.replace(paramCompileMatcher, (match, leadingSlash, paramName, optionalParamChar = "") => {
            if (paramName) {
                paramNames.push(paramName);
                return (leadingSlash ? "(?:/([^/?]+))" : "([^/?]+)") + optionalParamChar;
            } else if (match === "*") {
                paramNames.push("splat");
                return "(.*?)";
            } else {
                return "\\" + match;
            }
        });
        compiledPatterns[pattern] = {
            matcher: new RegExp("^" + source + (pattern.endsWith("/") ? "?" : "\\/?") + "$", "i"),
            paramNames
        };
    }
    return compiledPatterns[pattern];
}

function extractParams(pattern, path) {
    var object = compilePattern(pattern);
    var match = decodeUrl(path).match(object.matcher);
    if (!match) return undefined;
    var params = {};
    var pn = object.paramNames;
    var l = pn.length;
    for (var i = 0; i < l; i++) {
        params[pn[i]] = match[i + 1];
    }
    return params;
}

function injectParams(pattern, params) {
    params = params || {};
    var splatIndex = 0;
    return pattern.replace(paramInjectMatcher, (_match, leadingSlash = "", paramName) => {
        paramName = paramName || "splat";
        if (paramName.slice(-1) !== "?") {
            if (params[paramName] == undefined) throw new Error('Missing "' + paramName + '" parameter for path "' + pattern + '"');
        } else {
            paramName = paramName.slice(0, -1);
            if (params[paramName] == undefined) {
                return "";
            }
        }
        var segment;
        if (paramName === "splat" && Array.isArray(params[paramName])) {
            segment = params[paramName][splatIndex++];
            if (segment == undefined) throw new Error("Missing splat # " + splatIndex + ' for path "' + pattern + '"');
        } else {
            segment = params[paramName];
        }
        return leadingSlash + encodeUrlPath(segment);
    }) || "/";
}

function findMatch(path, rs, outParams) {
    var l = rs.length;
    var notFoundRoute;
    var defaultRoute;
    var params;
    for (var i = 0; i < l; i++) {
        var r = rs[i];
        if (r.isNotFound) {
            notFoundRoute = r;
            continue;
        }
        if (r.isDefault) {
            defaultRoute = r;
            continue;
        }
        if (r.children) {
            var res = findMatch(path, r.children, outParams);
            if (res) {
                res.push(r);
                return res;
            }
        }
        if (r.url) {
            params = extractParams(r.url, path);
            if (params) {
                outParams.p = params;
                return [ r ];
            }
        }
    }
    if (defaultRoute) {
        params = extractParams(defaultRoute.url || "", path);
        if (params) {
            outParams.p = params;
            return [ defaultRoute ];
        }
    }
    if (notFoundRoute) {
        params = extractParams(notFoundRoute.url || "", path);
        if (params) {
            outParams.p = params;
            return [ notFoundRoute ];
        }
    }
    return undefined;
}

let activeRoutes = [];

let futureRoutes;

let activeParams = localHelpers_1.newHashObj();

let activeState = undefined;

let nodesArray = [];

let setterOfNodesArray = [];

const urlRegex = /.*(?:\:|\/).*/;

function isInApp(name) {
    return !urlRegex.test(name);
}

function isAbsolute(url) {
    return url[0] === "/";
}

const renderActiveRouter = {
    render(_ctx, me) {
        me.children = me.data.activeRouteHandler();
    }
};

function getSetterOfNodesArray(idx) {
    while (idx >= setterOfNodesArray.length) {
        setterOfNodesArray.push(((a, ii) => n => {
            if (n) {
                var i = ii;
                a[i] = n;
                while (i-- > 0) {
                    a[i] = undefined;
                }
            }
        })(nodesArray, setterOfNodesArray.length));
    }
    return setterOfNodesArray[idx];
}

core_1.addEvent("popstate", 5, ev => {
    let newHistoryDeepness = ev.state?.historyDeepness;
    if (newHistoryDeepness != undefined) {
        activeState = ev.state.state;
        if (newHistoryDeepness != historyDeepness) core_1.invalidate();
        historyDeepness = newHistoryDeepness;
    }
    return false;
});

var firstRouting = true;

function rootNodeFactory() {
    if (waitingForPopHashChange >= 0) return undefined;
    if (history().state == undefined && historyDeepness != undefined) {
        history().replaceState({
            historyDeepness,
            state: activeState
        }, "");
    }
    let browserPath = window.location.hash;
    let path = browserPath.substr(1);
    if (!isAbsolute(path)) path = "/" + path;
    var out = {
        p: {}
    };
    var matches = findMatch(path, rootRoutes, out) || [];
    if (firstRouting) {
        firstRouting = false;
        currentTransition = {
            inApp: true,
            type: RouteTransitionType.Pop,
            name: undefined,
            params: undefined,
            state: undefined
        };
        transitionState = -1;
        programPath = browserPath;
    } else {
        if (!currentTransition && matches.length > 0 && browserPath != programPath) {
            programPath = browserPath;
            runTransition(createRedirectReplace(matches[0].name, out.p));
        }
    }
    if (currentTransition && currentTransition.type === RouteTransitionType.Pop && transitionState < 0) {
        programPath = browserPath;
        currentTransition.inApp = true;
        if (currentTransition.name == undefined && matches.length > 0) {
            currentTransition.name = matches[0].name;
            currentTransition.params = out.p;
            nextIteration();
            if (currentTransition != null) return undefined;
        } else return undefined;
    }
    if (currentTransition == undefined) {
        activeRoutes = matches;
        while (nodesArray.length > activeRoutes.length) nodesArray.shift();
        while (nodesArray.length < activeRoutes.length) nodesArray.unshift(undefined);
        activeParams = out.p;
    }
    var fn = localHelpers_1.noop;
    for (var i = 0; i < activeRoutes.length; i++) {
        ((fnInner, r, routeParams, i) => {
            fn = (otherData => {
                var data = r.data || {};
                core_1.assign(data, otherData);
                data.activeRouteHandler = fnInner;
                data.routeParams = routeParams;
                var handler = r.handler;
                var res;
                if (isFunc_1.isFunction(handler)) {
                    res = {
                        key: undefined,
                        ref: undefined,
                        children: handler(data)
                    };
                } else {
                    res = {
                        key: undefined,
                        ref: undefined,
                        data,
                        component: handler || renderActiveRouter
                    };
                }
                if (r.keyBuilder) res.key = r.keyBuilder(routeParams); else res.key = r.name;
                res.ref = getSetterOfNodesArray(i);
                return res;
            });
        })(fn, activeRoutes[i], activeParams, i);
    }
    return fn();
}

function joinPath(p1, p2) {
    if (isAbsolute(p2)) return p2;
    if (p1[p1.length - 1] === "/") return p1 + p2;
    return p1 + "/" + p2;
}

function registerRoutes(url, rs) {
    var l = rs.length;
    for (var i = 0; i < l; i++) {
        var r = rs[i];
        var u = url;
        var name = r.name;
        if (!name && url === "/") {
            name = "root";
            r.name = name;
            nameRouteMap[name] = r;
        } else if (name) {
            nameRouteMap[name] = r;
            u = joinPath(u, name);
        }
        if (r.isDefault) {
            u = url;
        } else if (r.isNotFound) {
            u = joinPath(url, "*");
        } else if (r.url) {
            u = joinPath(url, r.url);
        }
        r.url = u;
        if (r.children) registerRoutes(u, r.children);
    }
}

function routes(root) {
    if (!isFunc_1.isArray(root)) {
        root = [ root ];
    }
    registerRoutes("/", root);
    rootRoutes = root;
    core_1.init(rootNodeFactory);
}

function route(config, nestedRoutes) {
    return {
        name: config.name,
        url: config.url,
        data: config.data,
        handler: config.handler,
        keyBuilder: config.keyBuilder,
        children: nestedRoutes
    };
}

function routeDefault(config) {
    return {
        name: config.name,
        data: config.data,
        handler: config.handler,
        keyBuilder: config.keyBuilder,
        isDefault: true
    };
}

function routeNotFound(config) {
    return {
        name: config.name,
        data: config.data,
        handler: config.handler,
        keyBuilder: config.keyBuilder,
        isNotFound: true
    };
}

function isActive(name, params) {
    if (params) {
        for (var prop in params) {
            if (params.hasOwnProperty(prop)) {
                if (activeParams[prop] !== params[prop]) return false;
            }
        }
    }
    for (var i = 0, l = activeRoutes.length; i < l; i++) {
        if (activeRoutes[i].name === name) {
            return true;
        }
    }
    return false;
}

function urlOfRoute(name, params) {
    if (isInApp(name)) {
        var r = nameRouteMap[name];
        if (DEBUG) {
            if (rootRoutes == undefined) throw Error("Cannot use urlOfRoute before defining routes");
            if (r == undefined) throw Error("Route with name " + name + " if not defined in urlOfRoute");
        }
        return "#" + injectParams(r.url, params);
    }
    return name;
}

exports.activeStyleDef = cssInJs_1.styleDef("active");

function Link(data) {
    return cssInJs_1.style({
        tag: "a",
        component: {
            id: "link",
            onClick() {
                runTransition((data.replace ? createRedirectReplace : createRedirectPush)(data.name, data.params, data.state));
                return true;
            }
        },
        children: data.children,
        attrs: {
            href: urlOfRoute(data.name, data.params)
        }
    }, isActive(data.name, data.params) ? data.activeStyle != undefined ? data.activeStyle : [ data.style, exports.activeStyleDef ] : data.style);
}

function link(node, name, params, state) {
    node.data = node.data || {};
    node.data.routeName = name;
    node.data.routeParams = params;
    node.data.routeState = state;
    core_1.postEnhance(node, {
        render(ctx, me) {
            let data = ctx.data;
            if (me.tag === "a") {
                me.attrs = me.attrs || {};
                me.attrs.href = urlOfRoute(data.routeName, data.routeParams);
            }
            me.className = me.className || "";
            if (isActive(data.routeName, data.routeParams)) {
                me.className += " active";
            }
        },
        onClick(ctx) {
            let data = ctx.data;
            runTransition(createRedirectPush(data.routeName, data.routeParams, data.routeState));
            return true;
        }
    });
    return node;
}

function createRedirectPush(name, params, state) {
    return {
        inApp: isInApp(name),
        type: RouteTransitionType.Push,
        name,
        params: params || {},
        state: state ?? activeState
    };
}

function createRedirectReplace(name, params, state) {
    return {
        inApp: isInApp(name),
        type: RouteTransitionType.Replace,
        name,
        params: params || {},
        state: state ?? activeState
    };
}

function createBackTransition(distance) {
    distance = distance || 1;
    return {
        inApp: historyDeepness - distance >= 0,
        type: RouteTransitionType.Pop,
        name: undefined,
        params: {},
        state: undefined,
        distance
    };
}

var currentTransition = null;

var nextTransition = null;

var transitionState = 0;

function doAction(transition) {
    switch (transition.type) {
      case RouteTransitionType.Push:
        push(urlOfRoute(transition.name, transition.params), transition.inApp, transition.state);
        break;

      case RouteTransitionType.Replace:
        replace(urlOfRoute(transition.name, transition.params), transition.inApp, transition.state);
        break;

      case RouteTransitionType.Pop:
        pop(transition.distance);
        break;
    }
}

function nextIteration() {
    while (true) {
        if (transitionState >= 0 && transitionState < activeRoutes.length) {
            let node = nodesArray[transitionState];
            transitionState++;
            if (!node) continue;
            let comp = node.component;
            if (!comp && isFunc_1.isArray(node.children)) {
                node = node.children[0];
                if (!node) continue;
                comp = node.component;
            }
            if (!comp) continue;
            let fn = comp.canDeactivate;
            if (!fn) continue;
            let res = fn.call(comp, node.ctx, currentTransition);
            if (res === true) continue;
            Promise.resolve(res).then(resp => {
                if (resp === true) {} else if (resp === false) {
                    currentTransition = null;
                    nextTransition = null;
                    if (programPath) replace(programPath, true);
                    return;
                } else {
                    nextTransition = resp;
                }
                nextIteration();
            }).catch(err => {
                console.log(err);
            });
            return;
        } else if (transitionState == activeRoutes.length) {
            if (nextTransition) {
                if (currentTransition && currentTransition.type == RouteTransitionType.Push) {
                    push(urlOfRoute(currentTransition.name, currentTransition.params), currentTransition.inApp);
                }
                currentTransition = nextTransition;
                nextTransition = null;
            }
            transitionState = -1;
            if (!currentTransition.inApp || currentTransition.type === RouteTransitionType.Pop) {
                let tr = currentTransition;
                if (!currentTransition.inApp) currentTransition = null;
                doAction(tr);
                return;
            }
        } else if (transitionState === -1) {
            var out = {
                p: {}
            };
            if (currentTransition.inApp) {
                futureRoutes = findMatch(urlOfRoute(currentTransition.name, currentTransition.params).substring(1), rootRoutes, out) || [];
            } else {
                futureRoutes = [];
            }
            transitionState = -2;
        } else if (transitionState === -2 - futureRoutes.length) {
            if (nextTransition) {
                transitionState = activeRoutes.length;
                continue;
            }
            if (currentTransition.type !== RouteTransitionType.Pop) {
                let tr = currentTransition;
                currentTransition = null;
                doAction(tr);
            } else {
                core_1.invalidate();
            }
            currentTransition = null;
            return;
        } else {
            if (nextTransition) {
                transitionState = activeRoutes.length;
                continue;
            }
            let rr = futureRoutes[futureRoutes.length + 1 + transitionState];
            transitionState--;
            let handler = rr.handler;
            let comp = undefined;
            if (isFunc_1.isFunction(handler)) {
                let node = handler({
                    activeRouteHandler: () => undefined,
                    routeParams: currentTransition.params
                });
                if (!node || !isFunc_1.isObject(node) || isFunc_1.isArray(node)) continue;
                comp = node.component;
            } else {
                comp = handler;
            }
            if (!comp) continue;
            let fn = comp.canActivate;
            if (!fn) continue;
            let res = fn.call(comp, currentTransition);
            if (res === true) continue;
            Promise.resolve(res).then(resp => {
                if (resp === true) {} else if (resp === false) {
                    currentTransition = null;
                    nextTransition = null;
                    return;
                } else {
                    nextTransition = resp;
                }
                nextIteration();
            }).catch(err => {
                console.log(err);
            });
            return;
        }
    }
}

exports.transitionRunCount = 1;

function runTransition(transition) {
    exports.transitionRunCount++;
    mouseEvents_1.preventClickingSpree();
    if (currentTransition != null) {
        nextTransition = transition;
        return;
    }
    firstRouting = false;
    currentTransition = transition;
    transitionState = 0;
    nextIteration();
}

function Anchor({children, name, params, onAnchor}) {
    return anchor(children, name, params, onAnchor);
}

function anchor(children, name, params, onAnchor) {
    return {
        children,
        component: {
            id: "anchor",
            postUpdateDom(ctx, me) {
                handleAnchorRoute(ctx, me, name, params, onAnchor);
            },
            postInitDom(ctx, me) {
                handleAnchorRoute(ctx, me, name, params, onAnchor);
            }
        }
    };
}

function handleAnchorRoute(ctx, me, name, params, onAnchor) {
    let routeName;
    if (name) {
        routeName = name;
    } else {
        const firstChild = me.children && me.children[0];
        routeName = firstChild.attrs && firstChild.attrs.id;
    }
    if (!isActive(routeName, params)) {
        ctx.l = 0;
        return;
    }
    if (ctx.l === exports.transitionRunCount) {
        return;
    }
    const element = core_1.getDomNode(me);
    onAnchor && onAnchor(element) || element.scrollIntoView();
    ctx.l = exports.transitionRunCount;
}

function getRoutes() {
    return rootRoutes;
}

function getActiveRoutes() {
    return activeRoutes;
}

function getActiveParams() {
    return activeParams;
}

function getActiveState() {
    return activeState;
}

function setActiveState(state) {
    history().replaceState({
        historyDeepness,
        state
    }, "");
    activeState = state;
}

function useCanDeactivate(handler) {
    const ctx = core_1.getCurrentCtx();
    if (ctx) {
        ctx.me.component.canDeactivate = function(ctx, transition) {
            return handler.call(ctx, transition);
        };
    }
}

