function test(info) {
    const { isActive = true } = info;
    if (!isActive) {
        console.log("Should not be printed");
    }
}

test({});
