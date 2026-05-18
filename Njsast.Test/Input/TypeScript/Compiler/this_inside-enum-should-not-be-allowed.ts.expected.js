"use strict";
// @target: es2015
var TopLevelEnum;
(function (TopLevelEnum) {
    TopLevelEnum[TopLevelEnum["ThisWasAllowedButShouldNotBe"] = this] = "ThisWasAllowedButShouldNotBe"; // Should not be allowed
})(TopLevelEnum || (TopLevelEnum = {}));
var ModuleEnum;
(function (ModuleEnum) {
    let EnumInModule;
    (function (EnumInModule) {
        EnumInModule[EnumInModule["WasADifferentError"] = this] = "WasADifferentError"; // this was handled as if this was in a module
    })(EnumInModule || (EnumInModule = {}));
})(ModuleEnum || (ModuleEnum = {}));
