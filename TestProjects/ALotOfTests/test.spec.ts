import * as index from "./index";

function Yield() {
    var res = new Promise((resolve, reject) => {
        setTimeout(() => resolve(), 0);
    });
    return res;
}

describe("root", () => {
    for (var j = 0; j < 10; j++) {
        describe("suite" + j, () => {
            for (var i = 0; i < 10; i++) {
                let ii = i;
                it("test" + ii, async () => {
                    await Yield();
                    expect(index.add(ii, ii)).toEqual(ii + ii);
                });
            }
        });
    }
});
