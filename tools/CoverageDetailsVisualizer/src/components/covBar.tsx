import * as b from "bobril";
import * as model from "../model/index";
import { cover100 } from "../styles";

export function CovBar({
    value,
    partial,
    full,
    total,
    maxhits,
}: {
    value: string;
    partial?: number;
    full: number;
    total: number;
    maxhits: number;
}) {
    var store = b.useContext(model.CoverageContext);
    if (value == "N/A")
        return (
            <div style={{ width: "100%", overflow: "visible", backgroundColor: "rgba(0,0,0,20%)" }}>
                <div
                    style={{
                        width: "4rem",
                        overflow: "visible",
                        height: "1.2rem",
                        display: "flex",
                        justifyContent: "center",
                        alignItems: "center",
                        borderWidth: 1,
                        borderStyle: "solid",
                        borderColor: "rgba(0,0,0,20%)",
                    }}
                >
                    N/A
                </div>
            </div>
        );
    let p = parseInt(value);
    let color = p >= 80 ? "rgba(0,255,0,20%)" : p >= 50 ? "rgba(240,240,0,40%)" : "rgba(255,0,0,20%)";
    return (
        <div
            onClick={store.rotateCovBarDisplayType}
            style={{ width: value, overflow: "visible", backgroundColor: color }}
        >
            {(store.covBarDisplayType == model.CovBarDisplayType.Percentages && (
                <div
                    style={{
                        width: "4rem",
                        height: "1.2rem",
                        textAlign: "right",
                        display: "flex",
                        justifyContent: "center",
                        alignItems: "center",
                        borderColor: color,
                        borderWidth: 1,
                        borderStyle: "solid",
                    }}
                >
                    {value}
                </div>
            )) ||
                (store.covBarDisplayType == model.CovBarDisplayType.RealNumbers && (
                    <div
                        style={{
                            width: "4rem",
                            height: "1.2rem",
                            fontSize: "0.5rem",
                            display: "flex",
                            justifyContent: "center",
                            alignItems: "center",
                            borderColor: color,
                            borderWidth: 1,
                            borderStyle: "solid",
                        }}
                    >
                        {partial != undefined && partial + "/"}
                        {full}/{total}
                    </div>
                )) || (
                    <div
                        style={{
                            width: "4rem",
                            height: "1.2rem",
                            textAlign: "right",
                            display: "flex",
                            justifyContent: "center",
                            alignItems: "center",
                            borderColor: color,
                            borderWidth: 1,
                            borderStyle: "solid",
                        }}
                    >
                        {maxhits}
                    </div>
                )}
        </div>
    );
}

export function CovBarStmn({ value }: { value: model.CoverageDetail }) {
    return (
        <CovBar
            value={value.statements}
            full={value.statementsCovered}
            total={value.statementsTotal}
            maxhits={value.statementsMaxHits}
        />
    );
}

export function CovBarCond({ value }: { value: model.CoverageDetail }) {
    return (
        <CovBar
            value={value.conditions}
            partial={value.conditionsCoveredPartially}
            full={value.conditionsCoveredFully}
            total={value.conditionsTotal}
            maxhits={value.conditionsMaxHits}
        />
    );
}

export function CovBarCase({ value }: { value: model.CoverageDetail }) {
    return (
        <CovBar
            value={value.switchBranches}
            full={value.switchBranchesCovered}
            total={value.switchBranchesTotal}
            maxhits={value.switchBranchesMaxHits}
        />
    );
}

export function CovBarFunc({ value }: { value: model.CoverageDetail }) {
    return (
        <CovBar
            value={value.functions}
            full={value.functionsCovered}
            total={value.functionsTotal}
            maxhits={value.functionsMaxHits}
        />
    );
}

export function CovBarLine({ value }: { value: model.CoverageDetail }) {
    return (
        <CovBar
            value={value.lines}
            partial={value.linesCoveredPartially}
            full={value.linesCoveredFully}
            total={value.linesTotal}
            maxhits={value.linesMaxHits}
        />
    );
}
