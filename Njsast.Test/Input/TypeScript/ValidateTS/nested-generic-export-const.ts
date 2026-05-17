import { root } from "state";

export const appCursor: Cursor<AppState<State>> = {
    key: root.key
};

export function loadLazy<
    T extends
        | { default: ((data?: T, children?: unknown) => Node) | ComponentClass<T> | ComponentFunction<T> }
        | ((data?: T, children?: unknown) => Node)
        | ComponentClass<T>
        | ComponentFunction<T>,
>(factory: () => Promise<T>): T extends { default: infer U } ? U : T {
    return factory as unknown as T extends { default: infer U } ? U : T;
}

export function createOptions(data: Data) {
    return {
        onChange: (hourValue: number, minuteValue: number): void => {
            applyChange(hourValue, minuteValue, data.onChange);
        },
        value: data.value || 0
    };
}

export function createNestedOptions(data: Data) {
    return {
        nested: data.enabled
            ? {
                  onChange: (hourValue: number, minuteValue: number): void => {
                      applyChange(hourValue, minuteValue, data.onChange);
                  },
                  value: data.value || 0
              }
            : undefined
    };
}

export const context = createContext<
    | {
          onRegistered<TKey, TContent>(cache: Cache<TKey, TContent>, key: TKey, serializedFullKey: string): void;
      }
    | undefined
>(undefined);

export function getUrl(path: `/${string}`) {
    return path;
}

export const createDialog = (
    state: State,
    names: Record<number, string>,
): {} => {
    return {
        state,
        names
    };
};

export function toProperty<T>(getCurrentValue: () => T, updateCallback: (newValue: T) => void) {
    return (newValue?) => {
        if (!newValue) {
            return getCurrentValue();
        }
        updateCallback(newValue);
        return newValue;
    };
}

export function castThis(value: unknown) {
    return (value as unknown) as Result;
}

export const thisCaster = {
    cast() {
        return (this as unknown) as Result;
    }
};
