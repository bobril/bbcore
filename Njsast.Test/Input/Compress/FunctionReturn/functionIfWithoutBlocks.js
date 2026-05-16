function func() {
    var a = call();
    if (a) {
        var b = call2();
        if (b) {
            if (a + b) return undefined;
        } else return undefined;
    }
    call3();
    return a;
}