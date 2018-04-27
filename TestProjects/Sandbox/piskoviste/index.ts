import * as b from 'bobril';
import { value } from "../temp/temp";

b.init(() => {

    console.log(value);

    return { tag: 'h1', children: 'Hello World!' };
});