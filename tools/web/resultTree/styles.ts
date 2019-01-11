import * as b from "bobril";

export const failureColor: string = "#d64146";
export const skippedColor: string = "#4444de";
export const successfulColor: string = "#5fc363";
export const logColor: string = "#cac05f";

export const stack = b.styleDef(
    {
        width: "fit-content",
        fontWeight: "normal",
        fontStyle: "italic",
        color: "#909090",
        marginLeft: "10px",
        marginRight: "10px",
        fontSize: "11px"
    },
    {
        hover: {
            cursor: "pointer",
            color: "#bdbdbd"
        }
    }
);

export const resultMessageFailed = b.styleDef({
    marginLeft: "40px",
    backgroundColor: "#76556d",
    marginBottom: "5px",
    color: "#ff4945"
});

export const resultMessageLog = b.styleDef({
    marginLeft: "40px",
    marginBottom: "5px",
    backgroundColor: "#6e7156",
    color: "#fbfb6a"
});

export const treeNode = b.styleDef({
    width: "fit-content",
    marginLeft: "30px"
});

export const resultNode = b.styleDef({
    width: "fit-content",
    marginLeft: "30px",

    minWidth: "250px",
    fontWeight: "normal",
    backgroundColor: "#646d88",
    marginBottom: "2px",
    boxShadow: "3px 3px 4px 0px #555a6b"
});

export const resultNodeHeader = b.styleDef({
    cursor: "pointer",
    color: "white",
    fontSize: "13px",
    fontWeight: "bold",
    padding: "1px 3px 1px 3px"
});

export const resultNodeHeaderFailed = b.styleDef(
    {
        cursor: "pointer",
        color: "white",
        fontSize: "13px",
        fontWeight: "bold",
        padding: "1px 3px 1px 3px",

        backgroundColor: failureColor
    },
    {
        hover: {
            backgroundColor: "#ff6d71"
        }
    }
);

export const resultNodeHeaderSkipped = b.styleDef(
    {
        cursor: "pointer",
        color: "white",
        fontSize: "13px",
        fontWeight: "bold",
        padding: "1px 3px 1px 3px",

        backgroundColor: skippedColor
    },
    {
        hover: {
            backgroundColor: "#6666fd"
        }
    }
);

export const resultNodeHeaderSuccessful = b.styleDef(
    {
        cursor: "pointer",
        color: "white",
        fontSize: "13px",
        fontWeight: "bold",
        padding: "1px 3px 1px 3px",

        backgroundColor: successfulColor
    },
    {
        hover: {
            backgroundColor: "#7ae47e"
        }
    }
);

export const resultNodeHeaderLog = b.styleDef(
    {
        cursor: "pointer",
        color: "white",
        fontSize: "13px",
        fontWeight: "bold",
        padding: "1px 3px 1px 3px",
        backgroundColor: "#b1b16f"
    },
    {
        hover: {
            backgroundColor: "#d8d892"
        }
    }
);

export const resultTree = b.styleDef({});

export const pathNode = b.styleDef({});

export const pathNodeHeader = b.styleDef(
    {
        width: "fit-content",
        // marginLeft: "30px",
        cursor: "pointer",

        color: "#9e9e9e",
        fontWeight: "normal"
    },
    {
        hover: {
            color: "#d2d2d2"
        }
    }
);

export const pathNodeHeaderFailed = b.styleDef(
    {
        width: "fit-content",
        cursor: "pointer",

        color: "#ad6e6e",
        fontWeight: "normal"
    },
    {
        hover: {
            color: "#da9f9f"
        }
    }
);

export const nestingNode = b.styleDef({ marginLeft: "30px" });

export const describeNode = b.styleDef({});

export const describeNodeHeader = b.styleDef(
    {
        width: "fit-content",
        cursor: "pointer",

        color: "#c7c7c7",
        fontWeight: "bold"
    },
    {
        hover: {
            color: "#efefef"
        }
    }
);

export const describeNodeHeaderFailed = b.styleDef(
    {
        width: "fit-content",
        cursor: "pointer",

        color: "#ffa5a5",
        fontWeight: "bold"
    },
    {
        hover: {
            color: "#ffd7d7"
        }
    }
);
