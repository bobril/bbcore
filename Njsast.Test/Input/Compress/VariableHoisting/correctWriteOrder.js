function invokeMouseOwner(handlerName) {
    if (ownerCtx == null) {
        return false;
    }
    var c = ownerCtx.me.component;
    var handler = c[handlerName];
}