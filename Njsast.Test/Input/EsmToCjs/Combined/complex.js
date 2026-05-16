var a = 1;
var b = 2;
export { a, b as c };
export var d = 3;
import { x } from "./mod";
export { x as y };
