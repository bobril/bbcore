(() => {
    var __export_DecodingMode, DecodingMode_decode, __export_$, exports_decode;
    (function(DecodingMode) {
        DecodingMode[DecodingMode["Legacy"] = 0] = "Legacy";
        DecodingMode[DecodingMode["Strict"] = 1] = "Strict";
    })(DecodingMode_decode || (__export_DecodingMode = DecodingMode_decode = {}));
    class EntityDecoder {
        constructor(mode) {
            this.mode = mode;
        }
        decode(input) {
            return input + ":" + this.mode;
        }
    }
    __export_$ = {
        DecodingMode: __export_DecodingMode,
        EntityDecoder
    };
    exports_decode = __export_$;
    console.log(new exports_decode.EntityDecoder(exports_decode.DecodingMode.Strict).decode("Ok"));
})();

