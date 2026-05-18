"use strict";
f(v => v ? [0] : Promise.reject());
f(async (v) => v ? [0] : Promise.reject());
g(v => v ? "contextuallyTypable" : Promise.reject());
g(async (v) => v ? "contextuallyTypable" : Promise.reject());
h(v => v ? (abc) => { } : Promise.reject());
h(async (v) => v ? (def) => { } : Promise.reject());
// repro from #29196
const increment = async (num, str) => {
    return a => {
        return a.length;
    };
};
const increment2 = async (num, str) => {
    return a => {
        return a.length;
    };
};
