import * as b from "bobril";
import * as styles from "../styles";
import * as s from "../../state";
import * as com from "../../communication";
import { ResultTree } from "../resultTree";
import { TreeNode, ResultTypes } from "./treeNode";
import { clickable } from "../clickable";

export class ResultNode extends TreeNode {
    SOT: s.SuiteOrTest;

    recalculateContainedResults() {}

    constructor(SOT: s.SuiteOrTest) {
        super(SOT.id);
        this.SOT = SOT;
        this.containedResults = this.getSOTResultType(SOT);
        this.setIsFiltered();
    }

    getSOTResultType(SOT: s.SuiteOrTest): ResultTypes {
        let hasLogs = SOT.logs && SOT.logs.length > 0;
        let isFailed = SOT.failures && SOT.failures.length > 0;
        let isSkipped = SOT.skipped;
        let isSuccessful = !isFailed && !hasLogs && !isSkipped;

        return { failed: isFailed, logs: hasLogs, skipped: isSkipped, successful: isSuccessful };
    }

    setIsFiltered() {
        this.isFiltered =
            this.SOT.name.includes(ResultTree.textFilter) ||
            (this.SOT.logs && this.SOT.logs.filter(log => log.message.includes(ResultTree.textFilter)).length > 0) ||
            (this.SOT.failures &&
                this.SOT.failures.filter(failure => failure.message.includes(ResultTree.textFilter)).length > 0);
    }

    stackFrameToString(stackFrame: s.StackFrame): string {
        var functionName = stackFrame.functionName || "{anonymous}";
        var args = "(" + (stackFrame.args || []).join(",") + ")";
        var fileName = stackFrame.fileName ? "@" + stackFrame.fileName : "";
        var lineNumber = stackFrame.lineNumber != null ? ":" + stackFrame.lineNumber : "";
        var columnNumber = stackFrame.columnNumber != null ? ":" + stackFrame.columnNumber : "";
        return functionName + args + fileName + lineNumber + columnNumber;
    }

    stackFramesToClickableComponent(stackFrames: s.StackFrame[]): b.IBobrilChildren {
        return b.styledDiv(
            stackFrames.map(stackFrame => {
                return clickable(b.styledDiv(this.stackFrameToString(stackFrame), styles.stack), () => {
                    com.focusPlace(stackFrame.fileName, [stackFrame.lineNumber, stackFrame.columnNumber]);
                });
            })
        );
    }

    toComponent(): b.IBobrilNode {
        return b.withKey(
            createResultNodeComponent({ node: this }),
            "Result node: " + this.SOT.name + ", at: " + this.SOT.stack[0].fileName + ":" + this.SOT.stack[0].lineNumber
        );
    }
}

interface MessageContext extends b.IBobrilCtx {
    isOpen: boolean;

    stack: b.IBobrilChildren;
    failures: b.IBobrilChildren[];
    logs: b.IBobrilChildren[];

    getResultHeaderStyle(containedResults: ResultTypes): any;
    setContent(): void;
}

interface IResultNodeComponentData {
    node: ResultNode;
}

const createResultNodeComponent = b.createComponent<IResultNodeComponentData>({
    id: "Result-Node",
    init(ctx: MessageContext) {
        ctx.isOpen = false;

        ctx.getResultHeaderStyle = (containedResults: ResultTypes): any => {
            return (
                (containedResults.failed && styles.resultNodeHeaderFailed) ||
                (containedResults.logs && styles.resultNodeHeaderLog) ||
                (containedResults.skipped && styles.resultNodeHeaderSkipped) ||
                styles.resultNodeHeaderSuccessful
            );
        };

        ctx.setContent = (): void => {
            ctx.stack = ctx.isOpen && ctx.data.node.stackFramesToClickableComponent(ctx.data.node.SOT.stack);

            ctx.failures = ctx.data.node.SOT.failures.map(failure => {
                return b.styledDiv(
                    [failure.message, ctx.isOpen && ctx.data.node.stackFramesToClickableComponent(failure.stack)],
                    styles.resultMessageFailed
                );
            });

            ctx.logs = ctx.data.node.SOT.logs.map(log => {
                return b.styledDiv(
                    [log.message, ctx.isOpen && ctx.data.node.stackFramesToClickableComponent(log.stack)],
                    styles.resultMessageLog
                );
            });
        };

        ctx.setContent();
    },
    render(ctx: MessageContext, me) {
        me.children = [
            clickable(
                b.styledDiv(
                    ctx.data.node.SOT.name + ": " + ctx.isOpen,
                    ctx.getResultHeaderStyle(ctx.data.node.containedResults)
                ),
                () => {
                    ctx.isOpen = !ctx.isOpen;
                    ctx.setContent();

                    b.invalidate();
                }
            ),
            ctx.stack,
            ctx.failures,
            ctx.logs
        ];
        b.style(me, [styles.resultNode]);
    }
});
