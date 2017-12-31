"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var lib = require("./lib");
var Main = /** @class */ (function (_super) {
    __extends(Main, _super);
    function Main() {
        return _super !== null && _super.apply(this, arguments) || this;
    }
    Main.prototype.hello = function () {
        console.log("Main");
    };
    return Main;
}(lib.Base));
new Main().hello();
