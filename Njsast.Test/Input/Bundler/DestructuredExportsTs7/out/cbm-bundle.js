(() => {
    var _, e, t, o, r;
    function s(_) {
        return {
            is: _ => _ > 1,
            get: () => _.length,
            set: e => _ = e
        };
    }
    ({is: _, get: e, set: t} = s("programList/thumbnailView/move"));
    [o, r] = [ 10, 20 ];
    t("abc");
    console.log(_(5), e(), o, r);
})();

