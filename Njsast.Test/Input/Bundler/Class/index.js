"use strict";
var Base = /** @class */ (function () {
    function Base() {
    }
    Base.prototype.render = function () {
        return "Hello";
    };
    Base.Id = "A";
    return Base;
}());
var Deriv = /** @class */ (function (_super) {
    __extends(Deriv, _super);
    function Deriv() {
        return _super !== null && _super.apply(this, arguments) || this;
    }
    Deriv.prototype.render = function () {
        return _super.prototype.render.call(this);
    };
    Deriv.Id = "B";
    return Deriv;
}(Base));
//# sourceMappingURL=index.js.map