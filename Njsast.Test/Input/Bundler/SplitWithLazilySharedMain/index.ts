import * as shared from "./shared";

shared.shared();

import("./lib").then(lib => {
    console.log(lib.hello());
});
