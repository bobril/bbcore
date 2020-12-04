import { x } from "../../first/src/index";
import { x as x2 } from "first/src/index";
describe("first", () => {
  it("test", () => {
    expect(x).toBe(1);
  });
  it("test2", () => {
    expect(x2).toBe(1);
  });
});
