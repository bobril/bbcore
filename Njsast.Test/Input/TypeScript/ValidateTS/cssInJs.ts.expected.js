"use strict";
exports.updateElementStyle = updateElementStyle;

exports.style = style;

exports.inlineStyleToCssDeclaration = inlineStyleToCssDeclaration;

exports.styleDef = styleDef;

exports.keyframesDef = keyframesDef;

exports.mediaQueryDef = mediaQueryDef;

exports.namedStyleDefEx = namedStyleDefEx;

exports.namedStyleDef = namedStyleDef;

exports.styleDefEx = styleDefEx;

exports.selectorStyleDef = selectorStyleDef;

exports.setAllowInvalidateStyles = setAllowInvalidateStyles;

exports.invalidateStyles = invalidateStyles;

exports.setImagesWithCredentials = setImagesWithCredentials;

exports.sprite = sprite;

exports.svg = svg;

exports.isSvgSprite = isSvgSprite;

exports.svgWithColor = svgWithColor;

exports.extractSvgDataUri = extractSvgDataUri;

exports.setBundlePngPath = setBundlePngPath;

exports.getSpritePaths = getSpritePaths;

exports.setSpritePaths = setSpritePaths;

exports.spriteb = spriteb;

exports.spritebc = spritebc;

exports.spriteWithColor = spriteWithColor;

exports.injectCss = injectCss;

exports.styledDiv = styledDiv;

exports.setStyleShim = setStyleShim;

const core_1 = require("./core");

const localHelpers_1 = require("./localHelpers");

const isFunc_1 = require("./isFunc");

const frameCallbacks_1 = require("./frameCallbacks");

const media_1 = require("./media");

const vendors = [ "Webkit", "Moz", "O" ];

const testingDivStyle = document.createElement("div").style;

function testPropExistence(name) {
    return isFunc_1.isString(testingDivStyle[name]);
}

var mapping = new Map();

var isUnitlessNumber = new Set("boxFlex boxFlexGroup columnCount flex flexGrow flexNegative flexPositive flexShrink fontWeight lineClamp lineHeight opacity order orphans strokeDashoffset widows zIndex zoom".split(" "));

function renamer(newName) {
    return (style, value, oldName) => {
        style[newName] = value;
        style[oldName] = undefined;
    };
}

function renamerPx(newName) {
    return (style, value, oldName) => {
        if (isFunc_1.isNumber(value)) {
            style[newName] = value + "px";
        } else {
            style[newName] = value;
        }
        style[oldName] = undefined;
    };
}

function pxAdder(style, value, name) {
    if (isFunc_1.isNumber(value)) style[name] = value + "px";
}

function shimStyle(newValue) {
    var k = Object.keys(newValue);
    for (var i = 0, l = k.length; i < l; i++) {
        var ki = k[i];
        var mi = mapping.get(ki);
        var vi = newValue[ki];
        if (vi === undefined) continue;
        if (mi === undefined) {
            if (DEBUG) {
                if (/-/.test(ki)) console.warn("Style property " + ki + " contains dash (must use JS props instead of css names)");
            }
            if (testPropExistence(ki)) {
                mi = isUnitlessNumber.has(ki) ? localHelpers_1.noop : pxAdder;
            } else {
                var titleCaseKi = ki.replace(/^\w/, match => match.toUpperCase());
                for (var j = 0; j < vendors.length; j++) {
                    if (testPropExistence(vendors[j] + titleCaseKi)) {
                        mi = (isUnitlessNumber.has(ki) ? renamer : renamerPx)(vendors[j] + titleCaseKi);
                        break;
                    }
                }
                if (mi === undefined) {
                    mi = isUnitlessNumber.has(ki) ? localHelpers_1.noop : pxAdder;
                    if (DEBUG && [ "overflowScrolling", "touchCallout" ].indexOf(ki) < 0) console.warn("Style property " + ki + " is not supported in this browser");
                }
            }
            mapping.set(ki, mi);
        }
        mi(newValue, vi, ki);
    }
}

function removeStyleProperty(s, name) {
    s.removeProperty(hyphenateStyle(name));
}

function setStyleProperty(s, name, value) {
    let len = value.length;
    if (len > 11 && value.substr(len - 11, 11) === " !important") {
        s.setProperty(hyphenateStyle(name), value.substr(0, len - 11), "important");
        return;
    }
    s.setProperty(hyphenateStyle(name), value);
}

function setClassName(el, className, inSvg) {
    if (inSvg) el.setAttribute("class", className); else el.className = className;
}

function updateElementStyle(el, newStyle, oldStyle) {
    var s = el.style;
    if (newStyle !== undefined) {
        shimStyle(newStyle);
        var rule;
        if (oldStyle !== undefined) {
            for (rule in oldStyle) {
                if (oldStyle[rule] === undefined) continue;
                if (newStyle[rule] === undefined) removeStyleProperty(s, rule);
            }
            for (rule in newStyle) {
                var v = newStyle[rule];
                if (v !== undefined && oldStyle[rule] !== v) setStyleProperty(s, rule, v);
            }
        } else {
            for (rule in newStyle) {
                var v = newStyle[rule];
                if (v !== undefined) setStyleProperty(s, rule, v);
            }
        }
    } else {
        if (oldStyle !== undefined) {
            for (rule in oldStyle) {
                removeStyleProperty(s, rule);
            }
        }
    }
}

function createNodeStyle(el, newStyle, newClass, c, inSvg) {
    if (isFunc_1.isFunction(newStyle)) {
        localHelpers_1.assert(newClass === undefined);
        var appliedStyle = core_1.applyDynamicStyle(newStyle, c);
        newStyle = appliedStyle.style;
        newClass = appliedStyle.className;
    }
    if (newStyle) updateElementStyle(el, newStyle, undefined);
    if (newClass) setClassName(el, newClass, inSvg);
}

function updateNodeStyle(el, newStyle, newClass, c, inSvg) {
    if (isFunc_1.isFunction(newStyle)) {
        localHelpers_1.assert(newClass === undefined);
        var appliedStyle = core_1.applyDynamicStyle(newStyle, c);
        newStyle = appliedStyle.style;
        newClass = appliedStyle.className;
    } else {
        core_1.destroyDynamicStyle(c);
    }
    updateElementStyle(el, newStyle, c.style);
    c.style = newStyle;
    if (newClass !== c.className) {
        setClassName(el, newClass || "", inSvg);
        c.className = newClass;
    }
}

var allStyles = localHelpers_1.newHashObj();

var allAnimations = localHelpers_1.newHashObj();

var allMediaQueries = localHelpers_1.newHashObj();

var allSprites = localHelpers_1.newHashObj();

var bundledSprites = localHelpers_1.newHashObj();

var allNameHints = localHelpers_1.newHashObj();

var dynamicSprites = [];

var svgSprites = new Map();

var unusedBundled = new Map();

var bundledDynamicSprites = [];

var imageCache = localHelpers_1.newHashObj();

var injectedCss = "";

var rebuildStyles = false;

var htmlStyle = null;

var globalCounter = 0;

var chainedAfterFrame = frameCallbacks_1.setAfterFrame(afterFrame);

const cssSubRuleDelimiter = /\:|\ |\>/;

function buildCssSubRule(parent) {
    let matchSplit = cssSubRuleDelimiter.exec(parent);
    if (!matchSplit) return allStyles[parent].name;
    let posSplit = matchSplit.index;
    return allStyles[parent.substring(0, posSplit)].name + parent.substring(posSplit);
}

function buildCssRule(parent, name) {
    let result = "";
    if (parent) {
        if (isFunc_1.isArray(parent)) {
            for (let i = 0; i < parent.length; i++) {
                if (i > 0) {
                    result += ",";
                }
                result += "." + buildCssSubRule(parent[i]) + "." + name;
            }
        } else {
            result = "." + buildCssSubRule(parent) + "." + name;
        }
    } else {
        result = "." + name;
    }
    return result;
}

function flattenStyle(cur, curPseudo, style, stylePseudo) {
    if (isFunc_1.isString(style)) {
        if (unusedBundled.has(style)) {
            unusedBundled.get(style).used = true;
            unusedBundled.delete(style);
            invalidateStyles();
        }
        let externalStyle = allStyles[style];
        if (externalStyle === undefined) {
            throw new Error("Unknown style " + style);
        }
        flattenStyle(cur, curPseudo, externalStyle.style, externalStyle.pseudo);
    } else if (isFunc_1.isFunction(style)) {
        style(cur, curPseudo);
    } else if (isFunc_1.isArray(style)) {
        for (let i = 0; i < style.length; i++) {
            flattenStyle(cur, curPseudo, style[i], undefined);
        }
    } else if (isFunc_1.isObject(style)) {
        for (let key in style) {
            if (!localHelpers_1.hOP.call(style, key)) continue;
            let val = style[key];
            if (isFunc_1.isFunction(val)) {
                val = val(cur, key);
            }
            cur[key] = val;
        }
    }
    if (stylePseudo != undefined && curPseudo != undefined) {
        for (let pseudoKey in stylePseudo) {
            let curPseudoVal = curPseudo[pseudoKey];
            if (curPseudoVal === undefined) {
                curPseudoVal = localHelpers_1.newHashObj();
                curPseudo[pseudoKey] = curPseudoVal;
            }
            flattenStyle(curPseudoVal, undefined, stylePseudo[pseudoKey], undefined);
        }
    }
}

let lastDppx = 0;

let lastSpriteUrl = "";

let lastSpriteDppx = 1;

let hasBundledSprites = false;

let wasSpriteUrlChanged = true;

function afterFrame(root) {
    var currentDppx = media_1.getMedia().dppx;
    if (hasBundledSprites && lastDppx != currentDppx) {
        lastDppx = currentDppx;
        let newSpriteUrl = bundlePath;
        let newSpriteDppx = 1;
        if (lastDppx > 1) {
            for (let i = 0; i < bundlePath2.length; i++) {
                [newSpriteUrl, newSpriteDppx] = bundlePath2[i];
                if (newSpriteDppx >= lastDppx) break;
            }
        }
        if (lastSpriteUrl != newSpriteUrl) {
            lastSpriteUrl = newSpriteUrl;
            lastSpriteDppx = newSpriteDppx;
            rebuildStyles = true;
            wasSpriteUrlChanged = true;
        }
    }
    if (rebuildStyles) {
        rebuildStyles = false;
        if (hasBundledSprites) {
            let imageSprite = imageCache[lastSpriteUrl];
            if (imageSprite === undefined) {
                imageSprite = null;
                imageCache[lastSpriteUrl] = imageSprite;
                loadImage(lastSpriteUrl, image => {
                    imageCache[lastSpriteUrl] = getImageData(image);
                    invalidateStyles();
                });
            }
            if (imageSprite != null) {
                for (let i = 0; i < bundledDynamicSprites.length; i++) {
                    let dynSprite = bundledDynamicSprites[i];
                    if (!dynSprite.used) continue;
                    let colorStr = dynSprite.color;
                    if (!isFunc_1.isString(colorStr)) colorStr = colorStr();
                    if (wasSpriteUrlChanged || colorStr !== dynSprite.lastColor) {
                        dynSprite.lastColor = colorStr;
                        let mulWidth = dynSprite.width * lastSpriteDppx | 0;
                        let mulHeight = dynSprite.height * lastSpriteDppx | 0;
                        let lastUrl = recolorAndClip(imageSprite, colorStr, mulWidth, mulHeight, dynSprite.left * lastSpriteDppx | 0, dynSprite.top * lastSpriteDppx | 0);
                        var stDef = allStyles[dynSprite.styleId];
                        stDef.style = {
                            backgroundImage: `url(${lastUrl})`,
                            width: dynSprite.width,
                            height: dynSprite.height,
                            backgroundPosition: 0,
                            backgroundSize: "100%"
                        };
                    }
                }
                if (wasSpriteUrlChanged) {
                    let iWidth = imageSprite.width / lastSpriteDppx;
                    let iHeight = imageSprite.height / lastSpriteDppx;
                    for (let key in bundledSprites) {
                        let sprite = bundledSprites[key];
                        if (sprite.color !== undefined) continue;
                        var stDef = allStyles[sprite.styleId];
                        let width = sprite.width;
                        let height = sprite.height;
                        let percentWidth = 100 * iWidth / width;
                        let percentHeight = 100 * iHeight / height;
                        stDef.style = {
                            backgroundImage: `url(${lastSpriteUrl})`,
                            width,
                            height,
                            backgroundPosition: `${100 * sprite.left / (iWidth - width)}% ${100 * sprite.top / (iHeight - height)}%`,
                            backgroundSize: `${percentWidth}% ${percentHeight}%`
                        };
                    }
                }
                wasSpriteUrlChanged = false;
            }
        }
        for (let i = 0; i < dynamicSprites.length; i++) {
            let dynSprite = dynamicSprites[i];
            let image = imageCache[dynSprite.url];
            if (image == undefined) continue;
            let colorStr = dynSprite.color();
            if (colorStr !== dynSprite.lastColor) {
                dynSprite.lastColor = colorStr;
                if (dynSprite.width == undefined) dynSprite.width = image.width;
                if (dynSprite.height == undefined) dynSprite.height = image.height;
                let lastUrl = recolorAndClip(image, colorStr, dynSprite.width, dynSprite.height, dynSprite.left, dynSprite.top);
                var stDef = allStyles[dynSprite.styleId];
                stDef.style = {
                    backgroundImage: `url(${lastUrl})`,
                    width: dynSprite.width,
                    height: dynSprite.height,
                    backgroundPosition: 0
                };
            }
        }
        var styleStr = injectedCss;
        for (var key in allAnimations) {
            var anim = allAnimations[key];
            styleStr += "@keyframes " + anim.name + " {";
            for (var key2 in anim.def) {
                let item = anim.def[key2];
                let style = localHelpers_1.newHashObj();
                flattenStyle(style, undefined, item, undefined);
                shimStyle(style);
                styleStr += key2 + (key2 == "from" || key2 == "to" ? "" : "%") + " {" + inlineStyleToCssDeclaration(style) + "}\n";
            }
            styleStr += "}\n";
        }
        for (var key in allStyles) {
            var ss = allStyles[key];
            let parent = ss.parent;
            let name = ss.name;
            let ssPseudo = ss.pseudo;
            let ssStyle = ss.style;
            if (isFunc_1.isFunction(ssStyle) && ssStyle.length === 0) {
                [ssStyle, ssPseudo] = ssStyle();
            }
            if (isFunc_1.isString(ssStyle) && ssPseudo == undefined) {
                ss.realName = ssStyle;
                localHelpers_1.assert(name != undefined, "Cannot link existing class to selector");
                continue;
            }
            ss.realName = name;
            let style = localHelpers_1.newHashObj();
            let flattenPseudo = localHelpers_1.newHashObj();
            flattenStyle(undefined, flattenPseudo, undefined, ssPseudo);
            flattenStyle(style, flattenPseudo, ssStyle, undefined);
            shimStyle(style);
            let cssStyle = inlineStyleToCssDeclaration(style);
            if (cssStyle.length > 0) styleStr += (name == undefined ? parent : buildCssRule(parent, name)) + " {" + cssStyle + "}\n";
            for (var key2 in flattenPseudo) {
                let item = flattenPseudo[key2];
                shimStyle(item);
                styleStr += (name == undefined ? parent + addDoubleDot(key2) : buildCssRule(parent, name + addDoubleDot(key2))) + " {" + inlineStyleToCssDeclaration(item) + "}\n";
            }
        }
        for (var key in allMediaQueries) {
            var mediaQuery = allMediaQueries[key];
            styleStr += "@media " + key + "{";
            for (var definition of mediaQuery) {
                for (var key2 in definition) {
                    let item = definition[key2];
                    let style = localHelpers_1.newHashObj();
                    flattenStyle(style, undefined, item, undefined);
                    shimStyle(style);
                    styleStr += "." + key2 + " {" + inlineStyleToCssDeclaration(style) + "}\n";
                }
            }
            styleStr += "}\n";
        }
        if (styleStr.length > 0) {
            var styleElement = document.createElement("style");
            styleElement.appendChild(localHelpers_1.createTextNode(styleStr));
            var head = document.head || document.getElementsByTagName("head")[0];
            if (htmlStyle != null) {
                head.replaceChild(styleElement, htmlStyle);
            } else {
                head.appendChild(styleElement);
            }
            htmlStyle = styleElement;
        }
    }
    chainedAfterFrame(root);
}

function addDoubleDot(pseudoOrElse) {
    var c = pseudoOrElse.charCodeAt(0);
    if (c == 32 || c == 91 || c == 46) return pseudoOrElse;
    return ":" + pseudoOrElse;
}

function style(node, ...styles) {
    let className = node.className;
    let inlineStyle = node.style;
    let stack = null;
    let i = 0;
    let ca = styles;
    while (true) {
        if (ca.length === i) {
            if (stack === null || stack.length === 0) break;
            ca = stack.pop();
            i = stack.pop() + 1;
            continue;
        }
        let s = ca[i];
        if (s == undefined || s === true || s === false || s === "" || s === 0) {} else if (isFunc_1.isString(s)) {
            if (unusedBundled.has(s)) {
                unusedBundled.get(s).used = true;
                unusedBundled.delete(s);
                rebuildStyles = true;
            }
            var sd = allStyles[s];
            if (sd != undefined) {
                s = sd.realName;
            }
            if (className == undefined) className = s; else className += " " + s;
        } else if (isFunc_1.isArray(s)) {
            if (ca.length > i + 1) {
                if (stack == undefined) stack = [];
                stack.push(i);
                stack.push(ca);
            }
            ca = s;
            i = 0;
            continue;
        } else {
            if (inlineStyle == undefined) inlineStyle = localHelpers_1.newHashObj();
            for (let key in s) {
                if (localHelpers_1.hOP.call(s, key)) {
                    let val = s[key];
                    if (isFunc_1.isFunction(val)) val = val();
                    inlineStyle[key] = val;
                }
            }
        }
        i++;
    }
    node.className = className;
    node.style = inlineStyle;
    return node;
}

const uppercasePattern = /([A-Z])/g;

const hyphenateCache = new Map([ [ "cssFloat", "float" ] ]);

function hyphenateStyle(s) {
    var res = hyphenateCache.get(s);
    if (res === undefined) {
        res = s.replace(uppercasePattern, "-$1").toLowerCase();
        hyphenateCache.set(s, res);
    }
    return res;
}

function inlineStyleToCssDeclaration(style) {
    var res = "";
    for (var key in style) {
        var v = style[key];
        if (v === undefined) continue;
        res += hyphenateStyle(key) + ":" + (v === "" ? '""' : v) + ";";
    }
    res = res.slice(0, -1);
    return res;
}

function styleDef(style, pseudoOrAttr, nameHint) {
    return styleDefEx(undefined, style, pseudoOrAttr, nameHint);
}

function makeName(nameHint) {
    if (nameHint && nameHint !== "b-") {
        nameHint = nameHint.replace(/[^a-z0-9_-]/gi, "_").replace(/^[0-9]/, "_$&");
        if (allNameHints[nameHint]) {
            var counter = 1;
            while (allNameHints[nameHint + counter]) counter++;
            nameHint = nameHint + counter;
        }
        allNameHints[nameHint] = true;
    } else {
        nameHint = "b-" + globalCounter++;
    }
    return nameHint;
}

function keyframesDef(def, nameHint) {
    nameHint = makeName(nameHint);
    allAnimations[nameHint] = {
        name: nameHint,
        def
    };
    rebuildStyles = true;
    const res = params => {
        if (isFunc_1.isString(params)) return params + " " + nameHint;
        return nameHint;
    };
    res.toString = res;
    return res;
}

function mediaQueryDef(def, mediaQueryDefinition) {
    let mediaQuery = allMediaQueries[def];
    if (!mediaQuery) {
        mediaQuery = [];
        allMediaQueries[def] = mediaQuery;
    }
    mediaQuery.push(mediaQueryDefinition);
    rebuildStyles = true;
}

function namedStyleDefEx(name, parent, style, pseudoOrAttr) {
    var res = styleDefEx(parent, style, pseudoOrAttr, name);
    if (res != name) throw new Error("named style " + name + " is not unique");
    return res;
}

function namedStyleDef(name, style, pseudoOrAttr) {
    return namedStyleDefEx(name, undefined, style, pseudoOrAttr);
}

function styleDefEx(parent, style, pseudoOrAttr, nameHint) {
    nameHint = makeName(nameHint);
    allStyles[nameHint] = {
        name: nameHint,
        realName: nameHint,
        parent,
        style,
        pseudo: pseudoOrAttr
    };
    if (isFunc_1.isString(style) && pseudoOrAttr == undefined) {
        allStyles[nameHint].realName = style;
    } else rebuildStyles = true;
    return nameHint;
}

function selectorStyleDef(selector, style, pseudoOrAttr) {
    allStyles["b-" + globalCounter++] = {
        name: null,
        realName: null,
        parent: selector,
        style,
        pseudo: pseudoOrAttr
    };
    rebuildStyles = true;
}

let allowInvalidateStyles = true;

function setAllowInvalidateStyles(value) {
    allowInvalidateStyles = value;
}

function invalidateStyles() {
    if (!allowInvalidateStyles) return;
    rebuildStyles = true;
    core_1.invalidate();
}

function getSafeCssUrl(url) {
    const escapedUrl = url.replace(/[\(\)]/g, "\\$&");
    return `url(${escapedUrl})`;
}

function updateSprite(spDef) {
    var stDef = allStyles[spDef.styleId];
    var style = {
        backgroundImage: getSafeCssUrl(spDef.url),
        width: spDef.width,
        height: spDef.height,
        backgroundPosition: `${-spDef.left}px ${-spDef.top}px`,
        backgroundSize: `${spDef.width}px ${spDef.height}px`
    };
    stDef.style = style;
    invalidateStyles();
}

function emptyStyleDef(url) {
    return styleDef({
        width: 0,
        height: 0
    }, undefined, url);
}

const rgbaRegex = /\s*rgba\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d+|\d*\.\d+)\s*\)\s*/;

function createCanvas(width, height) {
    var canvas = document.createElement("canvas");
    canvas.width = width;
    canvas.height = height;
    return [ canvas, canvas.getContext("2d") ];
}

function getImageData(image) {
    let width = image.naturalWidth;
    let height = image.naturalHeight;
    let ctx = createCanvas(width, height)[1];
    ctx.drawImage(image, 0, 0);
    return ctx.getImageData(0, 0, width, height);
}

function recolorAndClip(imageData, colorStr, width, height, left, top) {
    let [canvas, ctx] = createCanvas(width, height);
    let imgData = ctx.createImageData(width, height);
    let imgDataData = imgData.data;
    let rgba = rgbaRegex.exec(colorStr);
    let cRed, cGreen, cBlue, cAlpha;
    if (rgba) {
        cRed = parseInt(rgba[1], 10);
        cGreen = parseInt(rgba[2], 10);
        cBlue = parseInt(rgba[3], 10);
        cAlpha = Math.round(parseFloat(rgba[4]) * 255);
    } else {
        cRed = parseInt(colorStr.slice(1, 3), 16);
        cGreen = parseInt(colorStr.slice(3, 5), 16);
        cBlue = parseInt(colorStr.slice(5, 7), 16);
        cAlpha = colorStr.length == 9 ? parseInt(colorStr.slice(7, 9), 16) : 255;
    }
    let targetOffset = 0;
    let targetStride = 4 * width;
    let sourceData = imageData.data;
    let sourceStride = 4 * imageData.width;
    let sourceOffset = left * 4 + top * sourceStride;
    for (let y = 0; y < height; y++) {
        imgDataData.subarray(targetOffset, targetOffset + targetStride).set(sourceData.subarray(sourceOffset, sourceOffset + targetStride));
        targetOffset += targetStride;
        sourceOffset += sourceStride;
    }
    if (cAlpha === 255) {
        for (let i = 0; i < imgDataData.length; i += 4) {
            let red = imgDataData[i];
            if (red === imgDataData[i + 1] && red === imgDataData[i + 2] && (red === 128 || imgDataData[i + 3] < 255 && red > 112)) {
                imgDataData[i] = cRed;
                imgDataData[i + 1] = cGreen;
                imgDataData[i + 2] = cBlue;
            }
        }
    } else {
        for (let i = 0; i < imgDataData.length; i += 4) {
            let red = imgDataData[i];
            let alpha = imgDataData[i + 3];
            if (red === imgDataData[i + 1] && red === imgDataData[i + 2] && (red === 128 || alpha < 255 && red > 112)) {
                if (alpha === 255) {
                    imgDataData[i] = cRed;
                    imgDataData[i + 1] = cGreen;
                    imgDataData[i + 2] = cBlue;
                    imgDataData[i + 3] = cAlpha;
                } else {
                    alpha = alpha * (1 / 255);
                    imgDataData[i] = Math.round(cRed * alpha);
                    imgDataData[i + 1] = Math.round(cGreen * alpha);
                    imgDataData[i + 2] = Math.round(cBlue * alpha);
                    imgDataData[i + 3] = Math.round(cAlpha * alpha);
                }
            }
        }
    }
    ctx.putImageData(imgData, 0, 0);
    return canvas.toDataURL();
}

let lastFuncId = 0;

const funcIdName = "b@funcId";

let imagesWithCredentials = false;

const colorLessSpriteMap = new Map();

function loadImage(url, onload) {
    var image = new Image();
    image.crossOrigin = imagesWithCredentials ? "use-credentials" : "anonymous";
    image.addEventListener("load", () => onload(image));
    image.src = url;
}

function setImagesWithCredentials(value) {
    imagesWithCredentials = value;
}

function sprite(url, color, width, height, left, top) {
    localHelpers_1.assert(allStyles[url] === undefined, "Wrong sprite url");
    left = left || 0;
    top = top || 0;
    let colorId = color || "";
    let isVarColor = false;
    if (isFunc_1.isFunction(color)) {
        isVarColor = true;
        colorId = color[funcIdName];
        if (colorId == undefined) {
            colorId = "" + lastFuncId++;
            color[funcIdName] = colorId;
        }
    }
    var key = url + ":" + colorId + ":" + (width || 0) + ":" + (height || 0) + ":" + left + ":" + top;
    var spDef = allSprites[key];
    if (spDef) return spDef.styleId;
    var styleId = emptyStyleDef(url);
    spDef = {
        styleId,
        url,
        width,
        height,
        left,
        top
    };
    if (isVarColor) {
        spDef.color = color;
        spDef.lastColor = "";
        spDef.lastUrl = "";
        dynamicSprites.push(spDef);
        if (imageCache[url] === undefined) {
            imageCache[url] = null;
            loadImage(url, image => {
                imageCache[url] = getImageData(image);
                invalidateStyles();
            });
        }
        invalidateStyles();
    } else if (width == undefined || height == undefined || color != undefined) {
        loadImage(url, image => {
            if (spDef.width == undefined) spDef.width = image.width;
            if (spDef.height == undefined) spDef.height = image.height;
            if (color != undefined) {
                spDef.url = recolorAndClip(getImageData(image), color, spDef.width, spDef.height, spDef.left, spDef.top);
                spDef.left = 0;
                spDef.top = 0;
            }
            updateSprite(spDef);
        });
    } else {
        updateSprite(spDef);
    }
    allSprites[key] = spDef;
    if (colorId === "") {
        colorLessSpriteMap.set(styleId, spDef);
    }
    return styleId;
}

function svg(content) {
    var key = content + ":1";
    var styleId = svgSprites.get(key);
    if (styleId !== undefined) return styleId;
    styleId = buildSvgStyle(content, 1);
    var svgSprite = {
        styleId,
        svg: content
    };
    svgSprites.set(key, styleId);
    colorLessSpriteMap.set(styleId, svgSprite);
    return styleId;
}

function isSvgSprite(id) {
    let orig = colorLessSpriteMap.get(id);
    if (orig == undefined) throw new Error(id + " is not colorless sprite");
    return "svg" in orig;
}

function svgWithColor(id, colors, size = 1) {
    var original = colorLessSpriteMap.get(id);
    if (DEBUG && (original == undefined || !("svg" in original))) throw new Error(id + " is not colorless svg");
    var key = original.svg + ":" + size;
    if (isFunc_1.isFunction(colors)) {
        colors = colors();
        key += ":gray:" + colors;
    } else if (isFunc_1.isString(colors)) {
        key += ":gray:" + colors;
    } else for (let ckey in colors) {
        if (localHelpers_1.hOP.call(colors, ckey)) {
            let val = colors[ckey];
            if (isFunc_1.isFunction(val)) val = val();
            key += ":" + ckey + ":" + val;
        }
    }
    var styleId = svgSprites.get(key);
    if (styleId !== undefined) return styleId;
    var colorsMap = new Map();
    if (isFunc_1.isString(colors)) {
        colorsMap.set("gray", colors);
    } else for (let ckey in colors) {
        if (localHelpers_1.hOP.call(colors, ckey)) {
            let val = colors[ckey];
            if (isFunc_1.isFunction(val)) val = val();
            colorsMap.set(ckey, val);
        }
    }
    styleId = buildSvgStyle(original.svg.replace(/[\":][A-Z-]+[\";]/gi, m => {
        var c = colorsMap.get(m.substr(1, m.length - 2));
        return c !== undefined ? m[0] + c + m[m.length - 1] : m;
    }), size);
    svgSprites.set(key, styleId);
    return styleId;
}

function extractSvgDataUri(styleId) {
    let sd = allStyles[styleId];
    if (sd == undefined) throw new Error("Unknown styleId " + styleId);
    let backgroundImage = sd.style?.backgroundImage;
    if (backgroundImage == undefined) throw new Error("Not svg styleId " + styleId);
    return backgroundImage.slice(5, -2);
}

function buildSvgStyle(content, size) {
    var sizeStr = content.split('"', 1)[0];
    var [width, height] = sizeStr.split(" ").map(s => parseFloat(s) * size);
    var backgroundImage = 'url("data:image/svg+xml,' + encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="' + width + '" height="' + height + '" viewBox="0 0 ' + content + "</svg>") + '")';
    return styleDef({
        width,
        height,
        backgroundImage
    });
}

var bundlePath = window["bobrilBPath"] || "bundle.png";

var bundlePath2 = window["bobrilBPath2"] || [];

function setBundlePngPath(path) {
    bundlePath = path;
}

function getSpritePaths() {
    return [ bundlePath, bundlePath2 ];
}

function setSpritePaths(main, others) {
    bundlePath = main;
    bundlePath2 = others;
}

function spriteb(width, height, left, top) {
    var key = ":" + width + ":" + height + ":" + left + ":" + top;
    var spDef = bundledSprites[key];
    if (spDef) return spDef.styleId;
    hasBundledSprites = true;
    var styleId = styleDef({
        width,
        height
    });
    spDef = {
        styleId,
        width,
        height,
        left,
        top
    };
    bundledSprites[key] = spDef;
    wasSpriteUrlChanged = true;
    colorLessSpriteMap.set(styleId, spDef);
    return styleId;
}

function spritebc(color, width, height, left, top) {
    if (color == undefined) {
        return spriteb(width, height, left, top);
    }
    var colorId;
    if (isFunc_1.isString(color)) {
        colorId = color;
    } else {
        colorId = color[funcIdName];
        if (colorId == undefined) {
            colorId = "" + lastFuncId++;
            color[funcIdName] = colorId;
        }
    }
    var key = colorId + ":" + width + ":" + height + ":" + left + ":" + top;
    var spDef = bundledSprites[key];
    if (spDef) return spDef.styleId;
    hasBundledSprites = true;
    var styleId = styleDef({
        width,
        height
    });
    spDef = {
        styleId,
        width,
        height,
        left,
        top,
        used: false,
        color,
        lastColor: "",
        lastUrl: ""
    };
    bundledDynamicSprites.push(spDef);
    bundledSprites[key] = spDef;
    unusedBundled.set(styleId, spDef);
    return styleId;
}

function spriteWithColor(colorLessSprite, color) {
    const original = colorLessSpriteMap.get(colorLessSprite);
    if (DEBUG && original == undefined) throw new Error(colorLessSprite + " is not colorless sprite");
    if ("svg" in original) {
        return svgWithColor(colorLessSprite, {
            gray: color
        }, 1);
    } else if ("url" in original) {
        return sprite(original.url, color, original.width, original.height, original.left, original.top);
    } else {
        return spritebc(color, original.width, original.height, original.left, original.top);
    }
}

function injectCss(css) {
    injectedCss += css;
    invalidateStyles();
}

function styledDiv(children, ...styles) {
    return style({
        tag: "div",
        children
    }, styles);
}

function setStyleShim(name, action) {
    mapping.set(name, action);
}

setStyleShim("float", renamer("cssFloat"));

core_1.internalSetCssInJsCallbacks(createNodeStyle, updateNodeStyle, style);

