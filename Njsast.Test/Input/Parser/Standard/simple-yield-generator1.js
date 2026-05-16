function* func1() {
    yield 42;
}

function* func2() {
    yield* func1();
}