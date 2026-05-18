"use strict";
// @target: es2015
// @experimentalDecorators: true
let Greeter = class Greeter {
    constructor(message) {
        this.greeting = message;
    }
    greet() {
        return "Hello, " + this.greeting;
    }
    greet1() {
        return "Hello, " + this.greeting;
    }
    greet2(param) {
        return "Hello, " + this.greeting;
    }
    greet3(param) {
        return "Hello, " + this.greeting;
    }
};
__decorate([
    lambda(Enum.No),
    deco(Enum.No)
], Greeter.prototype, "greeting", void 0);
__decorate([
    lambda(Enum.No),
    deco(Enum.No)
], Greeter.prototype, "greet", null);
__decorate([
    lambda,
    deco
], Greeter.prototype, "greet1", null);
__decorate([
    __param(0, lambda(Enum.No)),
    __param(0, deco(Enum.No))
], Greeter.prototype, "greet2", null);
__decorate([
    __param(0, lambda),
    __param(0, deco)
], Greeter.prototype, "greet3", null);
Greeter = __decorate([
    lambda(Enum.No),
    deco(Enum.No)
], Greeter);
function deco(...args) { }
var Enum;
(function (Enum) {
    Enum[Enum["No"] = 0] = "No";
    Enum[Enum["Yes"] = 1] = "Yes";
})(Enum || (Enum = {}));
const lambda = (...args) => { };
let Greeter1 = class Greeter1 {
    constructor(message) {
        this.greeting = message;
    }
    greet() {
        return "Hello, " + this.greeting;
    }
    greet1() {
        return "Hello, " + this.greeting;
    }
    greet2(param) {
        return "Hello, " + this.greeting;
    }
    greet3(param) {
        return "Hello, " + this.greeting;
    }
};
__decorate([
    lambda(Enum.No),
    deco(Enum.No)
], Greeter1.prototype, "greeting", void 0);
__decorate([
    lambda(Enum.No),
    deco(Enum.No)
], Greeter1.prototype, "greet", null);
__decorate([
    lambda,
    deco
], Greeter1.prototype, "greet1", null);
__decorate([
    __param(0, lambda(Enum.No)),
    __param(0, deco(Enum.No))
], Greeter1.prototype, "greet2", null);
__decorate([
    __param(0, lambda),
    __param(0, deco)
], Greeter1.prototype, "greet3", null);
Greeter1 = __decorate([
    lambda(Enum.No),
    deco(Enum.No)
], Greeter1);
