(() => {
    var e, o, d, c;
    (function(e) {
        e[e["Legacy"] = 0] = "Legacy";
        e[e["Strict"] = 1] = "Strict";
    })(o || (e = o = {}));
    class EntityDecoder {
        constructor(e) {
            this.mode = e;
        }
        decode(e) {
            return e + ":" + this.mode;
        }
    }
    d = {
        DecodingMode: e,
        EntityDecoder
    };
    c = d;
    console.log(new c.EntityDecoder(c.DecodingMode.Strict).decode("Ok"));
})();

