"use strict";
{
    const promises = [ Promise.resolve(0) ];
    Promise.all(promises).then(results => {
        const first = results[0];
        const second = results[1];
    });
}
