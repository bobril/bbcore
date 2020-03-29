import * as b from "bobril";
import * as model from "../../model/index";
import { goToUp, goToDir, goToFile } from "../../model/routeTransitions";
import { clickable } from "../../styles";
import { CovBar } from "../../components/covBar";

export function DirectoryPage(data: { name: string }): b.IBobrilNode {
    var store = b.useContext(model.CoverageContext);
    var details = store.json.result[data.name] as model.CoverageDetail;
    if (details == undefined) {
        b.runTransition(b.createRedirectReplace("rootdir"));
        return undefined;
    }
    const isRoot = data.name == "*";
    return (
        <>
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
                        <CovBar value={details.statements} />
                    </td>
                    <td>
                        <CovBar value={details.conditions} />
                    </td>
                    <td>
                        <CovBar value={details.switchBranches} />
                    </td>
                    <td>
                        <CovBar value={details.functions} />
                    </td>
                    <td>
                        <CovBar value={details.lines} />
                    </td>
                </tr>
                {details.subDirectories?.map(n => {
                    var subDetails = store.json.result[n] as model.CoverageDetail;
                    return (
                        <tr>
                            <td style={clickable} onClick={goToDir(n)}>
                                {n}
                            </td>
                            <td>
                                <CovBar value={subDetails.statements} />
                            </td>
                            <td>
                                <CovBar value={subDetails.conditions} />
                            </td>
                            <td>
                                <CovBar value={subDetails.switchBranches} />
                            </td>
                            <td>
                                <CovBar value={subDetails.functions} />
                            </td>
                            <td>
                                <CovBar value={subDetails.lines} />
                            </td>
                        </tr>
                    );
                })}
                {details.subFiles?.map(n => {
                    var subDetails = store.json.result[n] as model.CoverageDetail;
                    return (
                        <tr>
                            <td style={clickable} onClick={goToFile(n)}>
                                {n}
                            </td>
                            <td>
                                <CovBar value={subDetails.statements} />
                            </td>
                            <td>
                                <CovBar value={subDetails.conditions} />
                            </td>
                            <td>
                                <CovBar value={subDetails.switchBranches} />
                            </td>
                            <td>
                                <CovBar value={subDetails.functions} />
                            </td>
                            <td>
                                <CovBar value={subDetails.lines} />
                            </td>
                        </tr>
                    );
                })}
            </table>
        </>
    );
}
