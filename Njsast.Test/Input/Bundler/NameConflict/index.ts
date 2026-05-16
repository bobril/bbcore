import { fn as bar } from './lib';

function fn(a:number, b:number) {
    return a - b;
}

const a = 2;

console.log(bar(fn(a, 1), a));
