export function createCleanup(cbs: () => (() => void)[]) {
    return {
        add: (cb: () => void): (() => void) => {
            cbs().push(cb);
            return () => cb();
        }
    };
}
