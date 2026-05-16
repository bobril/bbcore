var __c0v = new Uint32Array(0);

globalThis.__c0v = __c0v;

function __c0vS(i) {
    __c0v[i]++;
}

function __c0vC(r, i) {
    __c0v[i + (r ? 1 : 0)]++;
    return r;
}

var BlobBuilder = typeof BlobBuilder !== "undefined" ? BlobBuilder : typeof WebKitBlobBuilder !== "undefined" ? WebKitBlobBuilder : typeof MSBlobBuilder !== "undefined" ? MSBlobBuilder : typeof MozBlobBuilder !== "undefined" ? MozBlobBuilder : false;

var blobSupported = function() {
    try {
        var a = new Blob([ "hi" ]);
        return a.size === 2;
    } catch (e) {
        return false;
    }
}();

var blobSupportsArrayBufferView = blobSupported && function() {
    try {
        var b = new Blob([ new Uint8Array([ 1, 2 ]) ]);
        return b.size === 2;
    } catch (e) {
        return false;
    }
}();

var blobBuilderSupported = BlobBuilder && BlobBuilder.prototype.append && BlobBuilder.prototype.getBlob;

function mapArrayBufferViews(ary) {
    return ary.map(function(chunk) {
        if (chunk.buffer instanceof ArrayBuffer) {
            var buf = chunk.buffer;
            if (chunk.byteLength !== buf.byteLength) {
                var copy = new Uint8Array(chunk.byteLength);
                copy.set(new Uint8Array(buf, chunk.byteOffset, chunk.byteLength));
                buf = copy.buffer;
            }
            return buf;
        }
        return chunk;
    });
}

function BlobBuilderConstructor(ary, options) {
    options = options || {};
    var bb = new BlobBuilder();
    mapArrayBufferViews(ary).forEach(function(part) {
        bb.append(part);
    });
    return options.type ? bb.getBlob(options.type) : bb.getBlob();
}

//# sourceMappingURL=cov.js.map
