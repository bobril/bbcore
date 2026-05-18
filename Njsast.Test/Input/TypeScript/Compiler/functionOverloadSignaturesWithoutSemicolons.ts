function foo():{a:number;}
function foo():{a:string;}
function foo():{a:any;} { return {a:1} }

export default function bar(value: number): number
export default function bar(value: string): string
export default function bar(value: string | number): string | number {
    return 1
}
