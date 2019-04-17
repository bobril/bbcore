// redeclare due to the missing index "argument" in original declaration
interface Array<T> {
    find(predicate: (element: T, index: number) => boolean): T;
}
