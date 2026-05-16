export const getGlobals = () => globals;

export const setGlobals = (a: boolean) => {
    globals = {
        isSortingDisabled: a,
    };
};

let globals = {
    isSortingDisabled: false,
};

export function getDescriptor() {
    return { asd: "df" };
}
