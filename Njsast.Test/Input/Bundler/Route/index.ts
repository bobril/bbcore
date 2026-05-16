import * as b from './bobril';
import * as lib from './lib';

function doit() {
	let link = b.link("hello");
	console.log(link);
}

doit();
console.log(lib.page);

