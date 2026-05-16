import * as liba from './liba';
import * as libb from './libb';

let lib = Math.random()>0.5?liba:libb;

console.log(lib.fn(1,2));
