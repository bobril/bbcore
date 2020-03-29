import * as index from "./index";
import * as sw from "./switch";

it("test1", () => {
  index.calc1(1, 10);
  index.calc1(0, 20);
});

it("switch", () => {
  expect(sw.calcSwitch(1, 0)).toBe(1);
  expect(sw.calcSwitch(0, 2)).toBe(2);
  expect(sw.calcSwitch(2, 1)).toBe(2);
  expect(sw.calcSwitch(5, 5)).toBe(3);
});
