type Data<T extends string | number> = { value: T };

export const create = <T extends string | number>(data: Data<T>) => {
    return <Item value={data.value} />;
};
