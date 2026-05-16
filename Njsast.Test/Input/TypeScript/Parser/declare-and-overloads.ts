declare const DEBUG: boolean;
declare function external(value: string): number;

function format(value: string): string;
function format(value: number): string;
function format(value: string | number): string {
    return String(value);
}

export const result = format(1);
