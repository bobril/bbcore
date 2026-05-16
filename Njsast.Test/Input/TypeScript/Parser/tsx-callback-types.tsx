export const Button = (props: { onClick(value: string): void }) =>
    <button onClick={(event: MouseEvent) => props.onClick(event.type)}>Save</button>;
