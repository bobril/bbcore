import("./lib").then(lib => {
    console.log(lib.hello());
});

import("./lib2").then(lib => {
    console.log(lib.world());
});
