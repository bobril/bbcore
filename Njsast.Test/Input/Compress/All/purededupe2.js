// PureFuncs: styledDiv
function styledDiv(s)
{
    return { tag: "div", children: s };
}

function doIt()
{
    let d1 = styledDiv(" ");
    let d2 = styledDiv(" ");
    d1.attrs = { active: true };
    d2.attrs = { active: false };
    return [d1, d2];
}

log(doIt());
