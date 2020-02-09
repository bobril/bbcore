import * as index from "./index";

function Yield() {
  var res = new Promise((resolve, reject) => {
    setTimeout(() => resolve(), 0);
  });
  return res;
}
let long = new Array(30000).join("A");
describe("root", () => {
  for (var j = 0; j < 1000; j++) {
    describe("suite" + j, () => {
      for (var i = 0; i < 10; i++) {
        let ii = i;
        it("test" + ii, async () => {
          if (j > 990) console.log(long);
          expect(index.add(ii, ii)).toEqual(ii + ii);
        });
      }
    });
  }
});
