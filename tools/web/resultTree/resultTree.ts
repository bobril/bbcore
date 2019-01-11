import * as b from "bobril";
import * as styles from "./styles";
import * as s from "../state";
import * as testReportAnalyzer from "../testReportAnalyzer";
import { PathNode, NestingNode, DescribeNode } from "./nodes/nestingNode";
import { ResultTypes } from "./nodes/treeNode";
import { ResultNode } from "./nodes/resultNode";

export enum NestingMethod {
    ByDescribe,
    ByPath
}

export class ResultTree {
    public static readonly ROOT_NODE_NESTING_ID: string = "ROOT_NODE";
    public static textFilter: string = "";
    public static nodesOpenByDefault: boolean = true;
    public static nestingMethod: NestingMethod = NestingMethod.ByPath;
    public static showStatus: ResultTypes = {
        failed: true,
        skipped: false,
        successful: false,
        logs: false
    };
    // for forcing DOM reload
    public static id: number = 1;
    rootNode: PathNode;

    constructor() {
        this.setNewRootNode();
    }

    public toComponent(): b.IBobrilChildren {
        return b.withKey(
            b.styledDiv(this.rootNode.toComponent(), styles.resultTree),
            "id: " +
                ResultTree.id +
                ", nestingMethod: " +
                ResultTree.nestingMethod.toString() +
                ", filterValue: " +
                ResultTree.textFilter
        );
    }

    public reloadSOTs(separatedSOTs: testReportAnalyzer.SeparatedTests) {
        this.setNewRootNode();

        this.insertSOTs(
            [].concat(separatedSOTs.failed, separatedSOTs.skipped, separatedSOTs.passed, separatedSOTs.logged)
        );
    }

    setNewRootNode() {
        this.rootNode = new PathNode(ResultTree.ROOT_NODE_NESTING_ID);
    }

    insertSOTs(SOTs: s.SuiteOrTest[]) {
        SOTs.forEach(SOT => {
            SOT &&
                (ResultTree.nestingMethod === NestingMethod.ByDescribe
                    ? this.insertByDescribe(SOT)
                    : this.insertByPath(SOT));
        });
    }

    insertByDescribe(SOT: s.SuiteOrTest, nestingNodes?: NestingNode[]) {
        if (SOT.isSuite) {
            SOT.nested.forEach(nestedSOT => {
                let updatedNestingNodes: NestingNode[] = nestingNodes ? nestingNodes.slice(0) : [];
                updatedNestingNodes.push(new DescribeNode(SOT.name));

                this.insertByDescribe(nestedSOT, updatedNestingNodes);
            });
        } else {
            this.rootNode.traversingInsert(new ResultNode(SOT), nestingNodes.slice(0));
        }
    }

    insertByPath(SOT: s.SuiteOrTest) {
        let current: s.SuiteOrTest = SOT;
        while (current && current.isSuite) {
            current = current.nested[0];
        }

        if (!current) {
            this.insertByDescribe(SOT);
            return;
        }

        let pathParts: string[] = current.stack[0].fileName.split("/");

        let nodes: PathNode[] = pathParts.map(part => new PathNode(part));

        this.insertByDescribe(SOT, nodes);
    }
}
