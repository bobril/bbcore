import "bobril"; // Promotion :-)

describe("Coverage suite", () => {
    it("works", () => {
        expect(1 + 1).toBe(2);
    });

    it("reveals uncovered", () => {
        if (3 < 1) {
            console.log("Never get here");
        }
    });

    it("supports more complex conditions", () => {
        for (let i = 0; i < 10; i++) {
            if (i % 2 == 0 && i > 9) {
                console.log("no no!");
            }
            if (i == 7) break;
        }
    });
});
