// @target: es2015
type Box<T> = T;
declare function flat<A>(
    arr: A
): Box<A>[]

function foo<T>(arr: T[]) {
    return flat(arr);
}
