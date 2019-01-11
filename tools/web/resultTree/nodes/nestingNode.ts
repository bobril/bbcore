import * as b from "bobril";
import * as styles from "../styles";
import * as treeNode from "./treeNode";
import { ResultTree } from "../resultTree";
import { ResultNode } from "./resultNode";
import { clickable } from "../clickable";

export abstract class NestingNode extends treeNode.TreeNode {
    readonly OPEN_SYMBOL: string = "▼ ";
    readonly CLOSED_SYMBOL: string = "■ ";
    parent: NestingNode;
    nestingNodes: NestingNode[] = [];
    resultNodes: ResultNode[] = [];
    name: string;

    constructor(name: string) {
        super(name);
        this.name = name;
        this.setIsFiltered();
    }

    abstract getHeaderStyle(): any;

    setFilteredDescendantRecursively() {
        this.hasFilteredDescendant = true;

        if (!this.parent) return;

        this.parent.hasFilteredDescendant = true;
        this.parent.setFilteredDescendantRecursively();
    }

    insertResultNodes(...resultNodes: ResultNode[]) {
        resultNodes.forEach(node => {
            if (!node.isFiltered && !this.hasFilteredAncestor && !this.isFiltered) {
                return;
            }

            node.hasFilteredAncestor = node.hasFilteredAncestor || this.hasFilteredAncestor || this.isFiltered;
            if (node.isFiltered) {
                this.setFilteredDescendantRecursively();
            }

            this.resultNodes.push(node);
        });

        this.recalculateContainedResults();
    }

    insertNestingNodes(...nestingNodes: NestingNode[]) {
        nestingNodes.forEach(node => {
            node.parent = this;

            node.hasFilteredAncestor = node.hasFilteredAncestor || this.hasFilteredAncestor || this.isFiltered;
            if (node.isFiltered) {
                this.setFilteredDescendantRecursively();
            }

            this.nestingNodes.push(node);
        });
    }

    traversingInsert(resultNode: ResultNode, nestingNodes?: NestingNode[]) {
        if (!nestingNodes || nestingNodes.length === 0) {
            this.insertResultNodes(resultNode);
        } else {
            let targetNode = this.nestingNodes.find(node => node.nestingID === nestingNodes[0].nestingID);

            if (targetNode) {
                targetNode.traversingInsert(resultNode, nestingNodes.slice(1));
            } else {
                this.insertNestingNodes(nestingNodes[0]);
                this.nestingNodes[this.nestingNodes.length - 1].traversingInsert(resultNode, nestingNodes.slice(1));
            }
        }
    }

    setIsFiltered(): void {
        this.isFiltered = this.name.includes(ResultTree.textFilter);
    }

    recalculateContainedResults(): void {
        this.containedResults = {};

        this.nestingNodes.forEach(node => this.updateContainedResults(node.containedResults));
        this.resultNodes.forEach(node => this.updateContainedResults(node.containedResults));

        this.parent && this.parent.recalculateContainedResults();
    }

    toComponent() {
        return b.withKey(createNestingNodeComponent({ node: this }), "Nesting node: " + this.name);
    }
}

export class PathNode extends NestingNode {
    getHeaderStyle: any = () => {
        return this.containedResults.failed && ResultTree.showStatus.failed
            ? styles.pathNodeHeaderFailed
            : styles.pathNodeHeader;
    };
}

export class DescribeNode extends NestingNode {
    getHeaderStyle: any = () =>
        this.containedResults.failed && ResultTree.showStatus.failed
            ? styles.describeNodeHeaderFailed
            : styles.describeNodeHeader;
}

interface NestingDataCtx extends b.IBobrilCtx {
    isOpen: boolean;

    getHeaderName(): string;
}

interface INestingNodeComponentData {
    node: NestingNode;
}

const createNestingNodeComponent = b.createComponent<INestingNodeComponentData>({
    id: "Nesting-Node",
    init(ctx: NestingDataCtx) {
        ctx.isOpen = ctx.data.node.name === ResultTree.ROOT_NODE_NESTING_ID || ResultTree.nodesOpenByDefault;

        ctx.getHeaderName = (): string =>
            ctx.data.node.name !== ResultTree.ROOT_NODE_NESTING_ID &&
            (ctx.isOpen ? ctx.data.node.OPEN_SYMBOL : ctx.data.node.CLOSED_SYMBOL) + ctx.data.node.name;
    },
    render(ctx: NestingDataCtx, me) {
        me.children = [
            ctx.data.node.name !== ResultTree.ROOT_NODE_NESTING_ID &&
                clickable(b.styledDiv(ctx.getHeaderName(), ctx.data.node.getHeaderStyle()), () => {
                    ctx.isOpen = !ctx.isOpen;

                    b.invalidate();
                }),
            ctx.isOpen && [
                ctx.data.node.nestingNodes.map(node => node.isShown() && node.toComponent()),
                ctx.data.node.resultNodes.map(node => node.isShown() && node.toComponent())
            ]
        ];
        b.style(me, [styles.nestingNode]);
    }
});
