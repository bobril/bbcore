"use strict";
// @target: es2015
// @strictNullChecks: true
function f1(x) {
    if (typeof x !== 'string') {
        switch (x.kind) {
            case 'foo':
                x.name;
        }
    }
}
function f2(x) {
    if (typeof x === "object") {
        switch (x.kind) {
            case 'foo':
                x.name;
        }
    }
}
function f3(x) {
    if (x && typeof x !== "string") {
        switch (x.kind) {
            case 'foo':
                x.name;
        }
    }
}
function f4(x) {
    if (x && typeof x === "object") {
        switch (x.kind) {
            case 'foo':
                x.name;
        }
    }
}
// Repro from #31319
var EnumTypeNode;
(function (EnumTypeNode) {
    EnumTypeNode["Pattern"] = "Pattern";
    EnumTypeNode["Disjunction"] = "Disjunction";
})(EnumTypeNode || (EnumTypeNode = {}));
let n;
if (n.type === "Disjunction") {
    n.alternatives.slice();
}
else {
    n.elements.slice(); // n should be narrowed to Pattern
}
