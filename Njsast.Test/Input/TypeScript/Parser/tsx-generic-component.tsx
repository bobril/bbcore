export function Select<T extends string>(props: { value: T; options: T[] }) {
    return <select value={props.value}>{props.options.map(option => <option>{option}</option>)}</select>;
}
