import * as b from "bobril";
import * as testReportAnalyzer from "./testReportAnalyzer";
import * as styles from "./styles";
import * as s from "./state";
import * as com from "./communication";
import * as bs from "bobrilstrap";
import { ResultTree, NestingMethod } from "./resultTree/resultTree";

export function clickable(content: b.IBobrilChildren, action: () => void): b.IBobrilNode {
    return {
        children: content,
        component: {
            onClick() {
                action();
                return true;
            }
        }
    };
}

function button(name: string, action: () => void, style?: any) {
    return clickable(b.styledDiv(name, [styles.button, style]), action);
}

function createOverview(): b.IBobrilChildren {
    return b.styledDiv(
        [
            createBuildStatus(),
            createAgentOverview(selectedAgentIndex, i => {
                selectedAgentIndex = i;
                ResultTree.id++;
                reloadResultTree();
            })
        ],
        styles.overview
    );
}

function createNavbar(): b.IBobrilChildren {
    return bs.Navbar({ static: bs.NavbarStatic.Top, style: styles.navbar }, [
        bs.NavbarHeader({}, [bs.NavbarBrand({ href: " ", style: styles.navbarHeader }, "Bobril-Build")]),
        bs.NavbarNav({}, [
            bs.NavbarNavItem(
                { active: s.liveReload, style: styles.navbar },
                bs.A(
                    {
                        onClick: () => {
                            com.setLiveReload(!s.liveReload);
                        },
                        style: styles.navbarItem
                    },
                    "Live reload"
                )
            ),
            bs.NavbarNavItem(
                { active: s.coverage, style: styles.navbar },
                bs.A(
                    {
                        onClick: () => {
                            com.setCoverage(!s.coverage);
                        },
                        style: styles.navbarItem
                    },
                    "Coverage"
                )
            )
        ])
    ]);
}

function createBuildStatus() {
    const lastResult = s.lastBuildResult;
    return b.styledDiv([
        s.building && b.styledDiv("Build in progress"),
        b.styledDiv([
            b.styledDiv("Last Build Results -", styles.lastBuildResultPart, styles.lastBuildResultHeader),
            "  ",
            b.styledDiv("Errors: " + lastResult.errors, styles.lastBuildResultPart, styles.lastBuildResultErrors),
            "  ",
            b.styledDiv("Warnings: " + lastResult.warnings, styles.lastBuildResultPart, styles.lastBuildResultWarnings),
            "  ",
            b.styledDiv(
                "Duration: " + lastResult.time + "ms",
                styles.lastBuildResultPart,
                styles.lastBuildResultDuration
            )
        ]),
        lastResult.messages.map(m =>
            clickable(
                b.styledDiv(
                    [
                        b.styledDiv(
                            (m.isError ? "Error: " : "Warning: ") + m.text,
                            m.isError ? styles.buildStatusErrormessage : styles.buildStatusWarningMessage
                        ),
                        b.styledDiv(`${m.fileName} (${m.pos[0]}:${m.pos[1]}-${m.pos[2]}:${m.pos[3]})`, styles.filePos)
                    ],
                    styles.buildStatusMessage
                ),
                () => {
                    com.focusPlace(m.fileName, m.pos);
                }
            )
        )
    ]);
}

function createAgentOverview(selectedAgent: number, setSelectedAgent: (index: number) => void): b.IBobrilChildren {
    return s.testSvrState.agents.map((results, index) => {
        return clickable(
            b.styledDiv(
                [
                    b.styledDiv(results.userAgent, styles.spanInfo),
                    b.styledDiv(
                        [
                            b.styledDiv(
                                "Failures: " + results.testsFailed,
                                index === selectedAgent
                                    ? styles.agentOverviewFailedCount
                                    : styles.agentOverviewFailedCountInactive
                            ),
                            "  ",
                            b.styledDiv(
                                results.testsSkipped > 0 ? " Skipped: " + results.testsSkipped : "",
                                index === selectedAgent
                                    ? styles.agentOverviewSkippedCount
                                    : styles.agentOverviewSkippedCountInactive
                            ),
                            "  ",
                            b.styledDiv(
                                " Successful & Logs: " +
                                    (results.testsFinished - results.testsFailed - results.testsSkipped),
                                index === selectedAgent
                                    ? styles.agentOverviewSuccessfulAndLogsCount
                                    : styles.agentOverviewSuccessfulAndLogsCountInactive
                            ),
                            "  "
                        ],
                        styles.spanInfo
                    ),
                    createProgressBar(index === selectedAgent, results),
                    results.running &&
                        b.styledDiv("Running " + results.testsFinished + "/" + results.totalTests, styles.spanInfo)
                ],
                index === selectedAgent ? styles.activeAgentOverview : styles.agentOverview
            ),
            () => {
                setSelectedAgent(index);
            }
        );
    });
}

function createProgressBar(active: boolean, results: s.TestResultsHolder): b.IBobrilChildren {
    return b.styledDiv(
        bs.Progress({
            style: styles.progressBar,
            bars: [
                {
                    value: results.testsFailed / (results.totalTests / 100),
                    striped: results.running,
                    active: results.running,
                    style: active ? styles.progressBarFailed : styles.progressBarFailedInactive
                },
                {
                    value: results.testsSkipped / (results.totalTests / 100),
                    striped: results.running,
                    active: results.running,
                    style: active ? styles.progressBarSkipped : styles.progressBarSkippedInactive
                },
                {
                    value:
                        (results.testsFinished - (results.testsFailed + results.testsSkipped)) /
                        (results.totalTests / 100),
                    striped: results.running,
                    active: results.running,
                    style: active ? styles.progressBarSuccessful : styles.progressBarSuccessfulInactive
                }
            ]
        }),
        styles.spanInfo
    );
}

function createToolbar(): b.IBobrilChildren {
    return b.styledDiv(
        [
            createCollapseAndExpandButtons(),
            createResultTypeFilterButtons(),
            createFilter(),
            createNestingMethodSelectionButtons()
        ],
        styles.toolbar
    );
}

function createCollapseAndExpandButtons(): b.IBobrilChildren {
    return b.styledDiv(
        [
            button("Collapse", () => {
                ResultTree.nodesOpenByDefault = false;
                ResultTree.id++;
                reloadResultTree();
            }),
            button("Expand", () => {
                ResultTree.nodesOpenByDefault = true;
                ResultTree.id++;
                reloadResultTree();
            })
        ],
        styles.buttonGroup
    );
}

function createResultTypeFilterButtons(): b.IBobrilChildren {
    return b.styledDiv(
        [
            button(
                "Failed",
                () => {
                    ResultTree.showStatus.failed = !ResultTree.showStatus.failed;
                    b.invalidate();
                    b.invalidateStyles();
                },
                ResultTree.showStatus.failed ? styles.buttonFailedSelected : styles.buttonFailed
            ),
            button(
                "Skipped",
                () => {
                    ResultTree.showStatus.skipped = !ResultTree.showStatus.skipped;
                    b.invalidate();
                    b.invalidateStyles();
                },
                ResultTree.showStatus.skipped ? styles.buttonSkippedSelected : styles.buttonSkipped
            ),
            button(
                "Successful",
                () => {
                    ResultTree.showStatus.successful = !ResultTree.showStatus.successful;
                    b.invalidate();
                    b.invalidateStyles();
                },
                ResultTree.showStatus.successful ? styles.buttonSuccessfulSelected : styles.buttonSuccessful
            ),
            button(
                "Logs",
                () => {
                    ResultTree.showStatus.logs = !ResultTree.showStatus.logs;
                    b.invalidate();
                    b.invalidateStyles();
                },
                ResultTree.showStatus.logs ? styles.buttonLogsSelected : styles.buttonLogs
            )
        ],
        styles.buttonGroup
    );
}

function createFilter(): b.IBobrilChildren {
    interface FilterCtx extends b.IBobrilCtx {
        filterStyle: any;
        setFilterStyle(): void;
        getFilterInputValue(): string;
        setFilterInputValue(value: string): void;
    }

    return b.createComponent({
        init(ctx: FilterCtx, me) {
            ctx.setFilterStyle = () => {
                if (ResultTree.textFilter === "") {
                    ctx.filterStyle = [styles.filter, styles.filterInactiveContent];
                } else {
                    ctx.filterStyle = [
                        styles.filter,
                        ResultTree.textFilter === ctx.getFilterInputValue()
                            ? styles.filterActiveContent
                            : styles.filterInactiveContent
                    ];
                }
                b.invalidateStyles();
            };

            ctx.getFilterInputValue = (): string =>
                <HTMLInputElement>document.getElementById("filter")
                    ? (<HTMLInputElement>document.getElementById("filter")).value
                    : "";

            ctx.setFilterInputValue = (value: string) =>
                ((<HTMLInputElement>document.getElementById("filter")).value = value);

            ctx.setFilterStyle();
        },
        render(ctx: FilterCtx, me) {
            me.children = bs.InputText({
                id: "filter",
                placeholder: "Filter..",
                style: ctx.filterStyle,
                attrs: { autocomplete: "off" },
                onChange: () => ctx.setFilterStyle(),
                onKeyPress: event => {
                    if (event.charCode === 13) {
                        ResultTree.textFilter = ctx.getFilterInputValue();
                        ResultTree.id++;
                        reloadResultTree();
                    }
                    ctx.setFilterStyle();
                    return false;
                }
            });

            b.style(me, styles.buttonGroup);
        },
        onFocusOut(ctx: FilterCtx) {
            ctx.setFilterInputValue(ResultTree.textFilter);
            ctx.setFilterStyle();
        }
    })();
}

function createNestingMethodSelectionButtons(): b.IBobrilChildren {
    return b.styledDiv(
        [
            button(
                "by describe",
                () => {
                    if (ResultTree.nestingMethod === NestingMethod.ByPath) {
                        ResultTree.nestingMethod = NestingMethod.ByDescribe;
                        reloadResultTree();
                        b.invalidateStyles();
                        b.invalidate();
                    }
                },
                [
                    styles.switchButton,
                    ResultTree.nestingMethod === NestingMethod.ByPath ? styles.disabledButton : styles.activeButton
                ]
            ),
            button(
                "by path",
                () => {
                    if (ResultTree.nestingMethod === NestingMethod.ByDescribe) {
                        ResultTree.nestingMethod = NestingMethod.ByPath;
                        reloadResultTree();
                        b.invalidateStyles();
                        b.invalidate();
                    }
                },
                [
                    styles.switchButton,
                    ResultTree.nestingMethod === NestingMethod.ByDescribe ? styles.disabledButton : styles.activeButton
                ]
            )
        ],
        styles.buttonGroup
    );
}

function createResultTree(): b.IBobrilChildren {
    if (selectedAgentIndex !== lastLoadedAgentIndex || lastLoadedDataVersion !== s.testSvrDataVersion) {
        reloadResultTree();
    }

    return b.styledDiv(resultTree.toComponent(), styles.results);
}

function reloadResultTree() {
    let separatedResults: testReportAnalyzer.SeparatedTests = testReportAnalyzer.analyze(
        s.testSvrState.agents[selectedAgentIndex].nested
    );

    resultTree.reloadSOTs(separatedResults);

    lastLoadedAgentIndex = selectedAgentIndex;
    lastLoadedDataVersion = s.testSvrDataVersion;

    b.invalidate();
}

let selectedAgentIndex = -1;
let lastLoadedAgentIndex = -1;

let lastLoadedDataVersion = 0;

const resultTree: ResultTree = new ResultTree();

com.reconnect();

b.init(() => {
    if (selectedAgentIndex >= s.testSvrState.agents.length) {
        selectedAgentIndex = -1;
    }
    if (selectedAgentIndex === -1 && s.testSvrState.agents.length > 0) {
        selectedAgentIndex = 0;
    }
    return b.createComponent({
        render(ctx, me) {
            me.children = [
                createNavbar(),
                createOverview(),
                selectedAgentIndex >= 0 && createToolbar(),
                selectedAgentIndex >= 0 && createResultTree()
            ];
            b.style(me, styles.page);
        },
        postInitDom() {
            document.getElementsByTagName("html")[0].style.setProperty("height", "100%");
            document.getElementsByTagName("html")[0].style.setProperty("overflow-y", "scroll");
            document.getElementsByTagName("html")[0].style.setProperty("background-color", "#5d6273");
        }
    })();
});
