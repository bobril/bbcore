function value(): number {
    return 2;
}

const enum Dynamic {
    A = value()
}

console.log(Dynamic.A);
