interface Item {
    value: number;
}

export default interface DefaultErased {
    value: string;
}

declare export class AmbientExternal {
    value: string;
}

declare export abstract class AmbientAbstractExternal {
    abstract value: string;
}

declare export default abstract class AmbientDefaultAbstractExternal {
    abstract value: string;
}

type Renamed<T> = {
    readonly [K in keyof T as `prefix_${K & string}`]?: T[K];
};

import type { Shape } from "./types";
import { type type as ImportedTypeName } from "./types";
import { type "type-name" as TypeName, "runtime-name" as runtimeName } from "./types";
export type { Shape } from "./types";
export { type type as ExportedTypeName } from "./types";
export { type as as ExportedAsName, runtime as exportedRuntime } from "./types";
export { localArbitraryName as "arbitrary-name" };
export { "source-name" as renamedArbitraryName } from "./types";
export { "runtime-name" as runtimeNameWithAttributes, type "type-name" as TypeNameWithAttributes } from "./types" with { mode: "live" };
export { runtime } from "./mixed" with { mode: "live" };
export * as ns from "./ns" with { type: "js" };

const localArbitraryName = 1;
const plain = source as Item;
console.log(runtimeName);
const loadedJson = import<typeof import("./data.json")>("./data.json", { with: { type: "json" } });
const asserted = ((foo as Foo).bar! satisfies Bar) as const;
const instantiated = factory<string>(value as Input);
const assertedInstantiation = factory<string> as Factory;
const checkedInstantiation = factory<string> satisfies Factory;
const optionalCall = service!.load<string>?.(value as Input);
const optionalTagged = tag?.<string>`value ${plain}`;
const optionalMemberTagged = service?.tag<string>`value ${plain}`;
const parenthesizedOptionalMemberTagged = (service?.tag)<string>`value ${plain}`;
const asyncGeneric = async <T extends string>(value: T): Promise<T> => value;
const objectWithAsyncGenerator = {
    async *items<T>(): AsyncIterable<T> {
        yield value as T;
    },
    readonly async *readonlyItems<T>(): AsyncIterable<T> {
        yield value as T;
    },
    readonly *readonlyGenerator<T>(): Iterable<T> {
        yield value as T;
    },
    readonly async readonlyLoad<T>(value: T): Promise<T> {
        return value;
    },
    readonly get readonlyValue(): T {
        return value as T;
    },
    readonly set readonlyValue(value: T) {
        this.value = value;
    }
};
let definiteValue!: number;
definiteValue = 1;
const { a: destructuredValue = fallback }: { a?: number } = source;
const [tupleFirst = fallback, ...tupleRest]: [number?, ...number[]] = values;
console.log(definiteValue, destructuredValue, tupleFirst, tupleRest);

namespace N {
    export let uninitialized;
    export const { value = fallback } = source;
    export const [first, ...tail] = values;
    console.log(uninitialized, value, first, tail, plain);
    export class Service {
        value = 1;
    }
    export function make(): Service {
        return new Service();
    }
    export default class DefaultService {
        value = make().value;
    }
    export enum Mode {
        A = 1,
        B = A + 1
    }
    export const enum ConstMode {
        A = 1,
        B = A + 1
    }
    export enum KeywordMode {
        "a-b" = 1,
        default = 2,
        [dynamicKey] = 3
    }
    export import ServiceAlias = Service;
    export const selected = Mode.B + ConstMode.B;
    console.log(Service, make, DefaultService, ServiceAlias, KeywordMode["a-b"], KeywordMode.default, ConstMode.B);
}

namespace DecoratedNamespace {
    @sealed
    export default class Service {
        constructor(@param value: string) {}
    }
    export class MemberService {
        @field value = 1;
        @field static ready = true;
        @field [dynamicKey] = 2;
        @field static [anotherKey] = 3;
        @field [methodKey]() {}
        @field get [getterKey]() { return 1; }
        @field set [setterKey](@param value: number) {}
    }
    export const selected = Service;
}

namespace Computed {
    using namespaceResource = acquire();
    export const { [dynamicKey]: dynamic = fallback, ...rest } = source;
    export const { nested: { value = fallback, ...innerRest }, [anotherKey]: another, ...outerRest } = source;
    namespaceResource.use(dynamic, rest);
    console.log(dynamic, rest);
    console.log(value, innerRest, another, outerRest);
}

namespace DestructuringSources {
    export const { a, b } = getObject();
    export const [first, second] = getArray();
    export const { nested: { x, y } } = getNestedObject();
    export const [[innerFirst, innerSecond]] = getNestedArray();
    export const { [key()]: computed, plain } = getComputedObject();
    export const { withDefault = fallback, afterDefault } = getDefaultObject();
    export const { restValue, ...objectRest } = getRestObject();
    export const [arrayDefault = fallback, afterArrayDefault] = getDefaultArray();
    export const [arrayHead, ...arrayRest] = getRestArray();
    export const { nestedDefault: { deepDefault = fallback, afterDeepDefault } } = getDeepDefaultObject();
    export const { nestedRest: { deepRestValue, ...deepRest }, afterDeepRest } = getDeepRestObject();
    export let { mutableA, mutableB } = getMutableObject();
    export var [varFirst, varSecond] = getVarArray();
    export const { erasedConst };
    export let [erasedLet];
    export var { erasedVar };
    console.log(a, b, first, second, x, y, innerFirst, innerSecond, computed, plain,
        withDefault, afterDefault, restValue, objectRest, arrayDefault, afterArrayDefault,
        arrayHead, arrayRest, deepDefault, afterDeepDefault, deepRestValue, deepRest, afterDeepRest,
        mutableA, mutableB, varFirst, varSecond);
}

module Legacy {
    export const value = 1;
}

enum Mode {
    Ready = 1,
    Done = Ready << 1,
    Label = "label",
    Dynamic = "label".length,
    Joined = "a" + "b",
    Runtime = compute(),
    AfterRuntime = Runtime,
    true = 1,
    default = 2,
    class = 3,
    await = 4,
    "a-b" = 5,
    ["computed-name"] = 6,
    Nan = NaN,
    Inf = Infinity,
    NegInf = -Infinity,
    AfterNonFinite,
    StringBeforeImplicit = "text",
    AfterString,
    TrueValue = true as any,
    NullValue = null as any,
    UndefinedValue = undefined as any,
    constructor = 9,
    __proto__ = 10
}

const enumValue = Mode.Done;

function sealed(target: unknown) {}
function field(target: unknown, key: string) {}
function param(target: unknown, key: string | undefined, index: number) {}
function assertString(value: unknown): asserts value is string {
    if (typeof value !== "string") throw Error();
}
const isString = (value: unknown): value is string => typeof value === "string";
function throwTyped(value: unknown) {
    throw value as Error;
}
function* yieldTyped(value: unknown) {
    yield value as string;
    yield* values as Iterable<string>;
}
async function awaitTyped(value: Promise<unknown>) {
    return (await value) satisfies string;
}

@sealed
class Decorated {
    @field value = 1;
    @field #privateIgnored = 2;
    method(@param value: string) {
        return value;
    }
}

abstract class DecoratedAbstractMembers {
    @field abstract value: string;
    @field abstract accessor ready: boolean;
    @field abstract [abstractKey]: string;
    @field static abstract staticValue: string;
    @field readonly abstract readonlyValue: string;
    @field abstract method(): void;
}

class DecoratedDeclaredMembers {
    @field declare value: string;
    @field declare [declaredKey]: string;
    @field static declare staticValue: string;
    @field declare #privateValue: string;
    @field declare accessor ready: boolean;
}

@sealed
export class ExportedDecorated {
    value = 1;
}

export @sealed class ExportedDecoratedAfterExport {
    value = 2;
}

export @sealed abstract class ExportedAbstractDecoratedAfterExport {
    abstract value: number;
}

@sealed
export default class {
    @field value = 2;
    @field accessor ready = true;
}

class Accessors {
    accessor value = 1;
    accessor #privateValue;
    static accessor staticValue = 2;
    accessor [dynamicKey] = 3;
}

class AccessorNames {
    accessor = 1;
    static accessor = 2;
    static readonly accessor = 3;
    accessor() {
        return this.accessor;
    }
}

class ContextualKeywordNames {
    get = 1;
    set = 2;
    async = 3;
    static = 4;
    static get = 5;
    static set = 6;
    static async = 7;
    readonly get = 8;
    readonly set = 9;
}

class ModifierMethodBoundaries {
    readonly async run(): Promise<void> {}
    public static async load(): Promise<void> {}
    static public async save(): Promise<void> {}
    readonly *items(): Iterable<number> {}
    static readonly *staticItems(): Iterable<number> {}
    public get value(): number {
        return 1;
    }
    protected set value(value: number) {}
    static readonly async = 1;
}

class StaticBlockResources {
    static {
        using resource = acquire();
        resource.use();
    }
    static {
        await using resource = acquireAsync();
        resource.use();
    }
    static {
        @sealed
        class Local {
            static {
                using resource = acquire();
                resource.use();
            }
        }
        console.log(Local);
    }
}

if (shouldDecorateLocal)
    @sealed
    class IfDecoratedLocal {
        constructor(@param value: number) {}
    }

localDecoratedLabel:
    @sealed
    class LabelDecoratedLocal {}

switch (decoratedMode) {
    case 1:
        @sealed
        class SwitchDecoratedLocal {}
        break;
}

class Base {
    constructor(value: string) {}
}

class WithParams extends Base {
    constructor(public name: string, readonly id = 1, ...rest: string[]) {
        super(name);
        console.log(rest);
    }
}

class WithAccessorParam {
    constructor(protected accessor value = 1) {}
}

class WithDecoratedParameterProperties {
    constructor(@param public value: string, @param private readonly id = 1, @param protected accessor ready = true) {}
}

class GenericBase<T> {}

interface Runnable<T> {
    run(value: T): void;
}

class Derived extends GenericBase<string> implements Runnable<string>, Disposable {
    run(value: string): void {
        console.log(value);
    }
}

class MixedBase extends mixin<string>(Base) {}

const derived = new Derived();
const boxed = new GenericBase<string>();
const factoryInstance = new (factory<string>())(value);
console.log(MixedBase, boxed, factoryInstance);

function makeAccessorClass() {
    using localResource = acquire();
    using typedResource: Disposable = acquireTyped();
    using secondResource = acquireSecond();
    return class {
        accessor [dynamicKey] = 4;
        static accessor value = 5;
        static accessor [staticKey()] = 6;
    };
}

async function disposeLater() {
    await using asyncResource = acquireAsync();
    await using typedAsyncResource: AsyncDisposable = acquireTypedAsync();
    using syncResource = acquireSync();
    return asyncResource.value;
}

function disposeSyncLoop() {
    for (using item of resources) {
        item.use();
    }
    for (using resource = acquire(); shouldContinue(); step())
        resource.use();
}

async function disposeLoop() {
    for (await using asyncItem: Disposable of asyncResources)
        asyncItem.use();
    for (await using asyncResource: AsyncDisposable = acquireAsync(); shouldContinue(); step()) {
        asyncResource.use();
    }
    for await (using streamed of stream)
        streamed.use();
    for await (await using asyncStreamed of asyncStream)
        asyncStreamed.use();
}

function readLocalEnum() {
    enum LocalMode {
        A = 1,
        B = A + 1,
        default = 3,
        "a-b" = 4
    }
    return LocalMode.B + LocalMode.default + LocalMode["a-b"];
}

try {
    maybeThrow();
} catch (error: unknown) {
    console.log(error);
}

for (const [key, entryValue]: [string, number] of entries) {
    console.log(key, entryValue);
}

for (const { x, y }: { x: number; y: number } of points) {
    console.log(x, y);
}

for (let index: number = 0; index < 3; index++) {
    console.log(index);
}

for (const key in object as Record<string, unknown>) {
    console.log(key);
}
