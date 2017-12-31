!function(undefined) {
    "use strict";
    var uppercasePattern = /([A-Z])/g, msPattern = /^ms-/;
    function hyphenateStyle(s) {
        return "cssFloat" === s ? "float" : s.replace(uppercasePattern, "-$1").toLowerCase().replace(msPattern, "-ms-");
    }
    function inlineStyleToCssDeclaration(style) {
        var res = "";
        for (var key in style) {
            var v = style[key];
            v !== undefined && (res += hyphenateStyle(key) + ":" + ("" === v ? '""' : v) + ";");
        }
        return res = res.slice(0, -1);
    }
    console.log(inlineStyleToCssDeclaration({
        a: 1,
        b: 2
    }));
}();