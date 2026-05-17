export function createValue() {
    return 1;
}

export type CreatedValue = ReturnType<typeof createValue>;
export default CreatedValue;
