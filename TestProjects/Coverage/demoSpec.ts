import * as index from "./index";

it("sums", () => {
  expect(index.plus(1, 2)).toBe(3);
});

describe("fizzbuzz", () => {
  it("one", () => {
    expect(index.fizzbuzz(1)).toBe(1);
  });
  it("three", () => {
    expect(index.fizzbuzz(3)).toBe("fizz");
  });
});
