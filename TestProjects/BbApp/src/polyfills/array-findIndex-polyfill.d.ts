// redeclare due to the missing index "argument" in original declaration
interface Array<T> {
    findIndex(predicate: (element: T, index: number) => boolean, thisArg?: any): number;
}
