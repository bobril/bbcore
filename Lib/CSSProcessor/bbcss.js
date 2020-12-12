"use strict";
var postcss = csslib.postcss;
var cssnano = csslib.cssnano;
var postcssUrl = csslib.postcssurl;
function processCss(source, from, callback) {
    return postcss([postcssUrl({
            url: function (asset, dir) {
                if (asset.url.startsWith("data:"))
                    return asset.url;
                return callback(asset.url, dir.from);
            }
        })]).process(source, { from: from });
}
function concatenateCssAndMinify(inputs, callback) {
    return Promise.all(inputs.map(function (i) {
        return processCss(i.source, i.from, callback);
    })).then(function (results) {
        var r = results[0].root;
        for (var i = 1; i < results.length; i++) {
            r = r.append(results[i].root);
        }
        return postcss([cssnano({
                safe: true
            })]).process(r.toResult());
    });
}
function bbProcessCss(source, from) {
    processCss(source, from, function (url, from) { return bb.urlReplace(url, from); }).then(function (v) { return bb.finish(v.css); }, function (e) { return bb.fail(e.message + " " + e.stack); });
}
function bbConcatAndMinify(inputs) {
    var inp = JSON.parse(inputs);
    concatenateCssAndMinify(inp, function (url, from) { return bb.urlReplace(url, from); }).then(function (v) { return bb.finish(v.css); }, function (e) { return bb.fail(e.message + " " + e.stack); });
}
