import "./src/index";
import "./src/switch";

export function plus(a: number, b: number) {
  return a + b;
}

function as() {
  return "a";
}

export function fizzbuzz(i: number): number | string {
  return (i % 3 ? "" : "fizz") + (i % 5 ? "" : "buzz") || i;
}
