(() => {
    var allStyles = {}, globalCounter = 0;
    function selectorStyleDef(selector, style, pseudoOrAttr) {
        allStyles["b-" + globalCounter++] = {
            name: null,
            realName: null,
            parent: selector,
            style,
            pseudo: pseudoOrAttr
        };
    }
    selectorStyleDef("*", {
        color: "blue"
    });
})();

