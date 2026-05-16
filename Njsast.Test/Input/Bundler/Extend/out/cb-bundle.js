(() => {
    var __extendStatics, __extends, Base_lib, Main_index;
    __extendStatics = Object.setPrototypeOf || {
        __proto__: []
    } instanceof Array && function(d, b) {
        d.__proto__ = b;
    } || function(d, b) {
        var p;
        for (p in b) b.hasOwnProperty(p) && (d[p] = b[p]);
    };
    __extends = function(d, b) {
        __extendStatics(d, b);
        function __() {
            this.constructor = d;
        }
        d.prototype = b === null ? Object.create(b) : (__.prototype = b.prototype, new __());
    };
    Base_lib = function() {
        function Base() {}
        Base.prototype.hello = function() {
            console.log("Base");
        };
        return Base;
    }();
    Main_index = function(_super) {
        __extends(Main, _super);
        function Main() {
            return _super !== null && _super.apply(this, arguments) || this;
        }
        Main.prototype.hello = function() {
            console.log("Main");
        };
        return Main;
    }(Base_lib);
    new Main_index().hello();
})();

