import "./src/index";
import "./src/switch";

export function plus(a: number, b: number) {
  return a + b;
}

export function fizzbuzz(i: number): number | string {
  return (i % 3 ? "" : "fizz") + (i % 5 ? "" : "buzz") || i;
}
