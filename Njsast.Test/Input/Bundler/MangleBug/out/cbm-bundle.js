(() => {
    var e = {}, l = 0;
    function t(t, r, o) {
        e["b-" + l++] = {
            name: null,
            realName: null,
            parent: t,
            style: r,
            pseudo: o
        };
    }
    t("*", {
        color: "blue"
    });
})();

