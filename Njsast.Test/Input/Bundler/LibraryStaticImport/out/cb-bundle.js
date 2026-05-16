var __export_default;

function fn(a, b) {
    return a + b;
}

function addOne(v) {
    return fn(1, v);
}

console.log(fn(1, 2));

__export_default = fn;

export { addOne, __export_default as default };

