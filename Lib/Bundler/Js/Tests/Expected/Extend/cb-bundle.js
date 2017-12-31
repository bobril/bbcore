!function(undefined) {
    "use strict";
    var __extendStatics = Object.setPrototypeOf || {
        __proto__: []
    } instanceof Array && function(d, b) {
        d.__proto__ = b;
    } || function(d, b) {
        for (var p in b) b.hasOwnProperty(p) && (d[p] = b[p]);
    }, __extends = function(d, b) {
        __extendStatics(d, b);
        function __() {
            this.constructor = d;
        }
        d.prototype = null === b ? Object.create(b) : (__.prototype = b.prototype, new __());
    }, Base = function() {
        function Base_lib() {}
        return Base_lib.prototype.hello = function() {
            console.log("Base");
        }, Base_lib;
    }(), __export_Base = Base;
    new (function(_super) {
        __extends(Main_index, _super);
        function Main_index() {
            return null !== _super && _super.apply(this, arguments) || this;
        }
        return Main_index.prototype.hello = function() {
            console.log("Main");
        }, Main_index;
    }(__export_Base))().hello();
}();