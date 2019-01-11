import * as b from "bobril";
import * as treeStyles from "./resultTree/styles";

export const html = b.styleDef({
    userSelect: "none",
    display: "inline-block",
    width: "100%",
    height: "100%",
    backgroundColor: "black"
});

export const page = {
    fontFamily:
        '-apple-system, BlinkMacSystemFont, "Segoe UI", Helvetica, Arial, sans-serif, "Apple Color Emoji", "Segoe UI Emoji", "Segoe UI Symbol" !important',
    backgroundColor: "#5d6273",
    height: "100%"
};

export const navbar = b.styleDef({
    backgroundColor: "#4c5060",
    border: "0px",
    color: "#ececed",
    padding: "0",
    margin: "0"
});

export const navbarHeader = b.styleDef({
    textTransform: "uppercase",
    letterSpacing: "1px",
    fontWeight: "600"
});

export const navbarItem = b.styleDef({
    cursor: "pointer"
});

export const overview = b.styleDef({
    backgroundColor: "#6d748a",
    color: "#ececed",
    padding: "20px 10px 10px 10px"
});

export const header = b.styleDef({
    fontSize: "34px"
});

export const lastBuildResultPart = b.styleDef({
    display: "inline-block",
    fontSize: "12px"
});

export const lastBuildResultHeader = b.styleDef({
    fontWeight: "bold"
});

export const lastBuildResultErrors = b.styleDef({
    color: "#bf6f72"
});

export const lastBuildResultWarnings = b.styleDef({
    color: "#a2a23f"
});

export const lastBuildResultDuration = b.styleDef({
    color: "#909090"
});

export const agentOverviewFailedCount = b.styleDef({
    color: treeStyles.failureColor,
    display: "inline-block"
});

export const agentOverviewSkippedCount = b.styleDef({
    color: treeStyles.skippedColor,
    display: "inline-block"
});

export const agentOverviewSuccessfulAndLogsCount = b.styleDef({
    color: treeStyles.successfulColor,
    display: "inline-block"
});

export const agentOverviewFailedCountInactive = b.styleDef({
    color: "#bf6f72",
    display: "inline-block"
});

export const agentOverviewSkippedCountInactive = b.styleDef({
    color: "#4163ce",
    display: "inline-block"
});

export const agentOverviewSuccessfulAndLogsCountInactive = b.styleDef({
    color: "#73af75",
    display: "inline-block"
});
export const progressBar = b.styleDef({
    boxShadow: "1px 1px 1px 0px rgba(22, 24, 26, 0.18), 2px 2px 3px 0 rgba(22, 24, 26, 0.15)",
    borderRadius: "2px",
    verticalAlign: "top",
    display: "inline-block",
    width: "100%",
    margin: "0",
    padding: "0"
});

export const progressBarFailed = b.styleDef({
    backgroundColor: treeStyles.failureColor
});

export const progressBarSkipped = b.styleDef({
    backgroundColor: treeStyles.skippedColor
});

export const progressBarSuccessful = b.styleDef({
    backgroundColor: treeStyles.successfulColor
});

export const progressBarFailedInactive = b.styleDef({
    backgroundColor: "#bf6f72"
});

export const progressBarSkippedInactive = b.styleDef({
    backgroundColor: "#4163ce"
});

export const progressBarSuccessfulInactive = b.styleDef({
    backgroundColor: "#73af75"
});

export const toolbar = b.styleDef({
    display: "flex",
    justifyContent: "space-evenly",
    textAlign: "center",
    width: "100%",
    padding: "1% 0 1% 0"
});

export const selectedAgentHeader = b.styleDef({
    textAlign: "center",
    fontSize: "25px",
    fontWeight: "bold"
});

export const results = b.styleDef({
    color: "#ececed",
    backgroundColor: "#5d6273",
    padding: "15px 30px 30px 30px",
    wordBreak: "break-all"
});

export const activeElement = b.styleDef({
    boxShadow: "0 0 8px rgba(255, 255, 255, 0.6)"
});

export const button = b.styleDef(
    {
        padding: "8px",
        verticalAlign: "middle",
        cursor: "pointer",
        display: "inline-block",
        fontWeight: "bold",
        boxShadow: "2px 2px 1px rgba(22, 24, 26, 0.15), 0 3px 5px 0 rgba(22, 24, 26, 0.15)",
        color: "#4c5060",
        borderWidth: "0px",
        borderRadius: "2px",
        textTransform: "uppercase",
        backgroundColor: "#ececed",
        margin: "0px 5px 0px 5px",
        userSelect: "none"
    },
    {
        hover: {
            boxShadow:
                "0 0 8px #ffffff, 2px 2px 1px rgba(22, 24, 26, 0.2), 0 3px 5px 0 rgba(22, 24, 26, 0.13), inset 0 0 12px 50px #ffffff38"
        }
    }
);

export const switchButton = b.styleDef({
    margin: "0"
});

export const disabledButton = b.styleDef(
    {
        color: "#ababab",
        backgroundColor: "#676767"
    },
    {
        hover: {
            boxShadow:
                "0 0 8px #ffffff6b, 2px 2px 1px rgba(22, 24, 26, 0.2), 0 3px 5px 0 rgba(22, 24, 26, 0.13), inset 0 0 12px 50px #ffffff38"
        }
    }
);

export const activeButton = b.styleDef({
    boxShadow:
        "0 0 8px #ffffff, 2px 2px 1px rgba(22, 24, 26, 0.2), 0 3px 5px 0 rgba(22, 24, 26, 0.13), inset 0 0 12px 50px #ffffff38"
});

export const buttonGroup = b.styleDef({
    display: "inline"
});

export const buttonFailed = b.styleDef(
    {
        backgroundColor: "#76666e"
    },
    {
        hover: {
            boxShadow:
                "0 0 8px #b5adb1, 2px 2px 1px rgba(22, 24, 26, 0.2), 0 3px 5px 0 rgba(22, 24, 26, 0.13), inset 0 0 12px 50px #ffffff38"
        }
    }
);

export const buttonSkipped = b.styleDef(
    {
        backgroundColor: "#616d81"
    },
    {
        hover: {
            boxShadow:
                "0 0 8px #98a0af, 2px 2px 1px rgba(22, 24, 26, 0.2), 0 3px 5px 0 rgba(22, 24, 26, 0.13), inset 0 0 12px 50px #ffffff38"
        }
    }
);

export const buttonSuccessful = b.styleDef(
    {
        backgroundColor: "#60726b"
    },
    {
        hover: {
            boxShadow:
                "0 0 8px #98a59f, 2px 2px 1px rgba(22, 24, 26, 0.2), 0 3px 5px 0 rgba(22, 24, 26, 0.13), inset 0 0 12px 50px #ffffff38"
        }
    }
);

export const buttonLogs = b.styleDef(
    {
        backgroundColor: "#707267"
    },
    {
        hover: {
            boxShadow:
                "0 0 8px #a2a598, 2px 2px 1px rgba(22, 24, 26, 0.2), 0 3px 5px 0 rgba(22, 24, 26, 0.13), inset 0 0 12px 50px #ffffff38"
        }
    }
);

export const buttonSelectedFontColor = "white";

export const buttonSelected = b.styleDef({
    color: buttonSelectedFontColor
});

export const buttonFailedSelected = b.styleDef(
    {
        color: buttonSelectedFontColor,
        backgroundColor: treeStyles.failureColor,
        boxShadow: "2px 2px 1px rgba(22, 24, 26, 0.15), 0 3px 5px 0 rgba(22, 24, 26, 0.15), 0 0 8px #e83b41"
    },
    {
        hover: {
            boxShadow:
                "0 0 8px #f56f6f, 2px 2px 1px rgba(22, 24, 26, 0.2), 0 3px 5px 0 rgba(22, 24, 26, 0.13), inset 0 0 12px 50px #ffffff38"
        }
    }
);

export const buttonSkippedSelected = b.styleDef(
    {
        color: buttonSelectedFontColor,
        backgroundColor: treeStyles.skippedColor,
        boxShadow: "2px 2px 1px rgba(22, 24, 26, 0.21), 0 3px 5px 0 rgba(22, 24, 26, 0.15), 0 0 8px #3434ff"
    },
    {
        hover: {
            boxShadow:
                "0 0 8px #7e7eff, 2px 2px 1px rgba(22, 24, 26, 0.2), 0 3px 5px 0 rgba(22, 24, 26, 0.13), inset 0 0 12px 50px #ffffff38"
        }
    }
);

export const buttonSuccessfulSelected = b.styleDef(
    {
        color: buttonSelectedFontColor,
        backgroundColor: treeStyles.successfulColor,
        boxShadow: "2px 2px 1px rgba(22, 24, 26, 0.31), 0 3px 5px 0 rgba(22, 24, 26, 0.15), 0 0 8px #20e628"
    },
    {
        hover: {
            boxShadow:
                "0 0 8px #6de27b, 2px 2px 1px rgba(22, 24, 26, 0.2), 0 3px 5px 0 rgba(22, 24, 26, 0.13), inset 0 0 12px 50px #ffffff38"
        }
    }
);

export const buttonLogsSelected = b.styleDef(
    {
        color: buttonSelectedFontColor,
        backgroundColor: treeStyles.logColor,
        boxShadow: "2px 2px 1px rgba(22, 24, 26, 0.31), 0 3px 5px 0 rgba(22, 24, 26, 0.15), 0 0 6px #cece37"
    },
    {
        hover: {
            boxShadow:
                "0 0 8px #f7ff7e, 2px 2px 1px rgba(22, 24, 26, 0.2), 0 3px 5px 0 rgba(22, 24, 26, 0.13), inset 0 0 12px 50px #ffffff38"
        }
    }
);

export const filter = b.styleDef(
    {
        borderRadius: "2px",
        minWidth: "40ex",
        display: "inline",
        borderWidth: "0px",
        boxShadow: "inset 0 0 10px 0px #393c4a"
    },
    {
        focus: {
            boxShadow: "inset 0 0 10px 0px #444958"
        }
    }
);

export const filterInactiveContent = b.styleDef(
    {
        color: "#d8d8d8",
        backgroundColor: "#555a6a"
    },
    {
        focus: {
            boxShadow: "inset 0 0 10px 0px #444958"
        }
    }
);

export const filterActiveContent = b.styleDef({
    fontWeight: "bold",
    color: "white",
    backgroundColor: "#555a6a"
});

export const spanInfo = b.styleDef({
    display: "inline-block",
    width: "23%",
    paddingLeft: "20px"
});

export const activeAgentOverview = b.styleDef(
    { padding: "2px 0 2px 0", backgroundColor: "#8d95af", fontWeight: "bold" },
    {
        hover: {
            backgroundColor: "#9ca4bb",
            cursor: "pointer"
        }
    }
);

export const agentOverview = b.styleDef(
    { padding: "2px 0 2px 0" },
    {
        hover: {
            backgroundColor: "#777d92",
            cursor: "pointer"
        }
    }
);

export const buildStatusMessage = b.styleDef({
    backgroundColor: "#5d6273",
    margin: "0px 15px 5px 15px"
});

export const filePos = b.styleDef({
    paddingLeft: "15px",
    fontSize: "12px",
    color: "#a0a0a0"
});

export const buildStatusErrormessage = b.styleDef({
    padding: "0 5px 0 5px",
    color: "#fb2127"
});

export const buildStatusWarningMessage = b.styleDef({
    padding: "0 5px 0 5px",
    color: "#a29b3f"
});

b.selectorStyleDef(
    ".navbar-default .navbar-brand",
    { color: "#ececed" },
    {
        hover: { color: "#ececed" }
    }
);

b.selectorStyleDef(
    ".navbar-default .navbar-nav>li>a",
    { color: "#bbb" },
    {
        hover: { color: "#ececed" }
    }
);

b.selectorStyleDef(
    ".navbar-default .navbar-nav>.active>a",
    { color: "#ececed", backgroundColor: "#6d748a" },
    {
        hover: { color: "#ececed", backgroundColor: "#6d748a" }
    }
);
