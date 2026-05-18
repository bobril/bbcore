"use strict";
let Greeter = class Greeter {
    constructor(greeting, ...b) {
        this.greeting = greeting;
    }
    greet() {
        return "<h1>" + this.greeting + "</h1>";
    }
    static { this.x1 = 10; }
    fn(x) {
        return this.greeting;
    }
    get greetings() {
        return this.greeting;
    }
    set greetings(greetings) {
        this.greeting = greetings;
    }
};
__decorate([
    PropertyDecorator1,
    PropertyDecorator2(40)
], Greeter.prototype, "greet", null);
__decorate([
    PropertyDecorator1,
    PropertyDecorator2(50)
], Greeter.prototype, "x", void 0);
__decorate([
    __param(0, ParameterDecorator1),
    __param(0, ParameterDecorator2(70))
], Greeter.prototype, "fn", null);
__decorate([
    PropertyDecorator1,
    PropertyDecorator2(80),
    __param(0, ParameterDecorator1),
    __param(0, ParameterDecorator2(90))
], Greeter.prototype, "greetings", null);
__decorate([
    PropertyDecorator1,
    PropertyDecorator2(60)
], Greeter, "x1", void 0);
Greeter = __decorate([
    ClassDecorator1,
    ClassDecorator2(10),
    __param(0, ParameterDecorator1),
    __param(0, ParameterDecorator2(20)),
    __param(1, ParameterDecorator1),
    __param(1, ParameterDecorator2(30))
], Greeter);
