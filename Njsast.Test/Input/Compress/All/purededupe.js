let map = new Map();
let lastStyle = 0;

// PureFuncs: styleDef
function styleDef(s)
{
    let res = lastStyle++;
    map[res] = s;
    return res;
}

let s1 = styleDef({ color: "red" });
let s2 = styleDef({ color: "blue" });
let s3 = styleDef({ color: "red" });
let s4 = styleDef({ height: Math.random() });
let s5 = styleDef({ height: Math.random() });
log(s1);
log(s2);
log(s3);
log(s4);
log(s5);
