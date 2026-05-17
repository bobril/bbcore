"use strict";
exports.Children = void 0;

__exportStar(require("./src/isFunc"), exports);

__exportStar(require("./src/asap"), exports);

__exportStar(require("./src/cssTypes"), exports);

__exportStar(require("./src/frameCallbacks"), exports);

__exportStar(require("./src/core"), exports);

__exportStar(require("./src/keyEvents"), exports);

__exportStar(require("./src/mouseEvents"), exports);

__exportStar(require("./src/media"), exports);

__exportStar(require("./src/router"), exports);

__exportStar(require("./src/dnd"), exports);

__exportStar(require("./src/mediaQueryBuilder"), exports);

__exportStar(require("./src/svgExtensions"), exports);

__exportStar(require("./src/cssInJs"), exports);

__exportStar(require("./src/wc"), exports);

__exportStar(require("./src/cva"), exports);

exports.Children = __importStar(require("./src/children"));

const frameCallbacks_1 = require("./src/frameCallbacks");

const core_1 = require("./src/core");

const dnd_1 = require("./src/dnd");

const cssInJs_1 = require("./src/cssInJs");

if (!window.b) window.b = {
    deref: core_1.deref,
    getRoots: core_1.getRoots,
    setInvalidate: core_1.setInvalidate,
    invalidateStyles: cssInJs_1.invalidateStyles,
    ignoreShouldChange: core_1.ignoreShouldChange,
    setAfterFrame: frameCallbacks_1.setAfterFrame,
    setBeforeFrame: frameCallbacks_1.setBeforeFrame,
    getDnds: dnd_1.getDnds,
    setBeforeInit: core_1.setBeforeInit,
    setKeysInClassNames: core_1.setKeysInClassNames
};

