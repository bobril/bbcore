type Props<T> = {
    value: T;
    render(value: T): JSX.Element;
};

const identity = <const T extends string>(value: T): T => value;
const pick = <T extends { id: string },>(value: T): T => value;

const Component = <T extends { id: string }>(props: Props<T>) => (
    <section data-id={props.value.id}>{props.render(props.value)}</section>
);

const element = (
    <Component<{ id: string; label?: string }>
        value={pick({ id: identity("item"), label: "Item" } satisfies { id: string; label?: string })}
        render={(item) => <span>{item.id}</span>}
    />
);

export default element;
