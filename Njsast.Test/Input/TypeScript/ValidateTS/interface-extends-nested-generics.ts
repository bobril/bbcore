interface Base<T> {
    value: T;
}

export interface Data
    extends Omit<Base<string>, keyof Base<void | never>>,
        Base<string> {}

export function createData() {
    return 1;
}
