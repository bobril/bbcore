export const enum Color {
    Default,
    Error,
    Success,
}

export function getColor(color: Color) {
    return color === Color.Error ? "error" : "default";
}
