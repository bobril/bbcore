namespace Runtime {
    export const value: number = 1;
    const hidden: number = 2;

    export function get(): number {
        return value + hidden;
    }

    export class Box {
        read(): number {
            return value;
        }
    }

    export enum Kind {
        First,
        Second = 4
    }
}

console.log(Runtime.value, Runtime.get(), Runtime.Kind.Second);
