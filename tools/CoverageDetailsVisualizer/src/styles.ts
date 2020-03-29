import * as b from "bobril";

export const cover100 = b.styleDef({
    width: "100%",
    height: "100%",
    overflow: "hidden"
});

export const clickable = b.styleDef({
    cursor: "pointer"
});

export const fullyCoveredStyle = b.styleDef({ backgroundColor: "rgba(0,255,0,20%)" });
export const partiallyCoveredStyle = b.styleDef({
    backgroundColor: "rgba(255,255,0,40%)"
});
export const notCoveredStyle = b.styleDef({ backgroundColor: "rgba(255,0,0,20%)" });
