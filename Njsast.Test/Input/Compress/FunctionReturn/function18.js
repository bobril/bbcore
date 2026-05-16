function func() {
    if (a) {
        return call();
        function call() {
            return a;
        }
    }
    return call();
}