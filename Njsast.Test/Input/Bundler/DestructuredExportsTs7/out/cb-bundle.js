(() => {
    var __export_is, __export_get, __export_set, __export_first, __export_second;
    function make(name) {
        return {
            is: x => x > 1,
            get: () => name.length,
            set: v => name = v
        };
    }
    ({is: __export_is, get: __export_get, set: __export_set} = make("programList/thumbnailView/move"));
    [__export_first, __export_second] = [ 10, 20 ];
    __export_set("abc");
    console.log(__export_is(5), __export_get(), __export_first, __export_second);
})();

