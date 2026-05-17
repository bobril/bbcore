import * as b from "bobril";

export function render(label: string, value: string, suffix: string) {
    return <div>{label} {value} {suffix}</div>;
}
