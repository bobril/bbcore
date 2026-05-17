declare const Label: any;
declare const text: string;

export function label() {
    return (
        <Label isSimple isItalic>
            ({text})
        </Label>
    );
}
