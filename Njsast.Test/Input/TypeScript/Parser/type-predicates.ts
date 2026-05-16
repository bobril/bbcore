function isString(value: unknown): value is string {
    return typeof value === "string";
}
function isNumber(value: unknown): value is number {
    return typeof value === "number";
}
if (isString(input)) {
    console.log(input.toUpperCase());
}
