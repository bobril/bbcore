var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __hasOwnProp = Object.prototype.hasOwnProperty;
var __copyProps = (to, from, except, desc) => {
    if (from && typeof from === "object" || typeof from === "function") {
        for (let key of __getOwnPropNames(from))
            if (!__hasOwnProp.call(to, key) && key !== except)
                __defProp(to, key, { get: () => from[key], enumerable: !(desc = __getOwnPropDesc(from, key)) || desc.enumerable });
    }
    return to;
};
var __reExport = (target, mod, secondTarget) => (__copyProps(target, mod, "default"), secondTarget && __copyProps(secondTarget, mod, "default"));

// This pattern is taken from monaco-editor's esm/vs/basic-languages/_.contribution.js.
// In native ESM the import below is hoisted, so referencing `star` here works.
// When the bundler converts the import to a `const` declaration, the reference
// becomes a temporal-dead-zone violation and throws at runtime.
var fake_editor_exports = {};
__reExport(fake_editor_exports, star);
import * as star from "./api.js";

export function loadLanguage() {
    return star.apiFunction();
}
