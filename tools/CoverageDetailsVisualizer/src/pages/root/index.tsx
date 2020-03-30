import * as b from "bobril";
import * as model from "../../model/index";
import { cover100 } from "../../styles";

export function RootPage(data: { page: () => b.IBobrilChildren }): b.IBobrilNode {
    var store = b.useStore(() => new model.CoverageAppModel());

    b.useProvideContext(model.CoverageContext, store);
    return <div style={cover100}>{data.page()}</div>;
}
