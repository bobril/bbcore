import { ex22 } from "External2";

import External, { ex3, ex2 } from "External";

function getDescriptor() {
    return {
        a: External,
        b: ex2,
        c: ex3,
        d: ex22
    };
}

export { getDescriptor };

