function* foo(index) {
    while (index < 2) {
        yield index++;
    }
}