import * as b from 'bobril';

export const spanUserAgent = b.styleDef({ 
    display: "inline-block", 
    width: "30%" 
});

export const spanInfo = b.styleDef({ 
    marginLeft: 10, 
    display: "inline-block" 
});

export const selectedStyle = b.styleDef({ 
    background: "#ccddee" 
});

export const stackStyle = b.styleDef({
    marginLeft: 20
});

export const suiteDivStyle = b.styleDef({
    fontSize: "18px"
});

export const suiteChildrenIndentStyle = b.styleDef({
    marginLeft: 20
});

export const failedStyle = b.styleDef({
    color: "red"
});

export const skippedStyle = b.styleDef({
    color: "darkorange"
});

export const successfulStyle = b.styleDef({
    color: "blue"
});

export const filePos = b.styleDef({
    fontSize: "0.8em",
    color: "#444"
});

export const errorMessage = b.styleDef({
    color: "red"
});

export const warningMessage = b.styleDef({
    color: "darkorange"
});
