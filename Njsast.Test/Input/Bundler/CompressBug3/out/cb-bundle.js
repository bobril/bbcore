(() => {
    function test(info) {
        const {isActive = !0} = info;
        isActive || console.log("Should not be printed");
    }
    test({});
})();

