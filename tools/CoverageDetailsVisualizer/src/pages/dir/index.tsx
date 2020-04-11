import * as b from "bobril";
import * as model from "../../model/index";
import { goToUp, goToDir, goToFile } from "../../model/routeTransitions";
import { clickable } from "../../styles";
import { CovBarStmn, CovBarCond, CovBarFunc, CovBarCase, CovBarLine } from "../../components/covBar";

export function DirectoryPage(data: { name: string }): b.IBobrilNode {
    var store = b.useContext(model.CoverageContext);
    var details = store.json[data.name];
    if (details == undefined) {
        b.runTransition(b.createRedirectReplace("rootdir"));
        return undefined;
    }
    const isRoot = data.name == "*";
    return (
        <>
            <div>
                <div>
                    Statements: {details.statements} {details.statementsCovered}/{details.statementsTotal} Max hits{" "}
                    {details.statementsMaxHits}
                </div>
                <div>
                    Conditions: {details.conditions} {details.conditionsCoveredPartially}/
                    {details.conditionsCoveredFully}/{details.conditionsTotal} Max hits {details.conditionsMaxHits}
                </div>
                <div>
                    Switch branches: {details.switchBranches} {details.switchBranchesCovered}/
                    {details.switchBranchesTotal} Max hits {details.switchBranchesMaxHits}
                </div>
                <div>
                    Functions: {details.functions} {details.functionsCovered}/{details.functionsTotal} Max hits{" "}
                    {details.functionsMaxHits}
                </div>
                <div>
                    Lines: {details.lines} {details.linesCoveredPartially}/{details.linesCoveredFully}/
                    {details.linesTotal} Max hits {details.linesMaxHits}
                </div>
            </div>

            <table>
                <tr>
                    <th>Name</th>
                    <th style={{ width: 50 }}>Stmn</th>
                    <th style={{ width: 50 }}>Cond</th>
                    <th style={{ width: 50 }}>Case</th>
                    <th style={{ width: 50 }}>Func</th>
                    <th style={{ width: 50 }}>Line</th>
                </tr>
                <tr>
                    <td style={isRoot || clickable} onClick={goToUp(data.name)}>
                        {data.name} {!isRoot && <span style={{ fontStyle: "italic" }}>(up)</span>}
                    </td>
                    <td>
                        <CovBarStmn value={details} />
                    </td>
                    <td>
                        <CovBarCond value={details} />
                    </td>
                    <td>
                        <CovBarCase value={details} />
                    </td>
                    <td>
                        <CovBarFunc value={details} />
                    </td>
                    <td>
                        <CovBarLine value={details} />
                    </td>
                </tr>
                {details.subDirectories?.map((n) => {
                    var subDetails = store.json[n];
                    return (
                        <tr>
                            <td style={clickable} onClick={goToDir(n)}>
                                {n}
                            </td>
                            <td>
                                <CovBarStmn value={subDetails} />
                            </td>
                            <td>
                                <CovBarCond value={subDetails} />
                            </td>
                            <td>
                                <CovBarCase value={subDetails} />
                            </td>
                            <td>
                                <CovBarFunc value={subDetails} />
                            </td>
                            <td>
                                <CovBarLine value={subDetails} />
                            </td>
                        </tr>
                    );
                })}
                {details.subFiles?.map((n) => {
                    var subDetails = store.json[n];
                    return (
                        <tr>
                            <td style={clickable} onClick={goToFile(n)}>
                                {n}
                            </td>
                            <td>
                                <CovBarStmn value={subDetails} />
                            </td>
                            <td>
                                <CovBarCond value={subDetails} />
                            </td>
                            <td>
                                <CovBarCase value={subDetails} />
                            </td>
                            <td>
                                <CovBarFunc value={subDetails} />
                            </td>
                            <td>
                                <CovBarLine value={subDetails} />
                            </td>
                        </tr>
                    );
                })}
            </table>
        </>
    );
}
