const rootKey = "app.root";

export const rootCursor: Cursor<State> = {
    key: rootKey
};

export interface Cursor<T> {
    key: string;
}

export interface State {
    value: string;
}

export class WithParameterProperties {
    registrations = new Set<string>();

    constructor(public readonly getValue: () => string, private readonly createKey: (value: string) => string) {
    }
}
