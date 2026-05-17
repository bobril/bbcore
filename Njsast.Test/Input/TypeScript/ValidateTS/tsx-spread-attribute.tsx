export function renderInput(attrs: Record<string, unknown>) {
    return <input type="checkbox" {...attrs} />;
}

export function renderActions(close: () => void) {
    return [
        <Button label="Save" onClick={() => close()} />,
        <Button label="Cancel" onClick={() => close()} />,
    ];
}

export function renderChildFragment() {
    return <Stack><></><Label text="Name" /></Stack>;
}

export function renderObjectSpread(comment: string, programId: number) {
    return <Button {...{ comment, programId }} />;
}
