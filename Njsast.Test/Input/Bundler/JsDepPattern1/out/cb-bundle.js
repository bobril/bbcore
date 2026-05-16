var converters = function() {
    "use strict";
    window.converters = window.converters || {};
    (function() {
        var converters = window.converters;
        converters.statics = converters.statics || {};
        converters.inherit = function(child, base) {
            function Temp() {
                this.constructor = child;
            }
            Temp.prototype = base.prototype;
            child.prototype = new Temp();
        };
        converters.BuildType = {
            Default: "Default",
            WithReferences: "WithReferences"
        };
    })();
    converters.doIt = function() {
        console.log("Ok");
    };
    return converters;
}();

(() => {
    function hello() {
        converters.doIt();
    }
    hello();
})();

