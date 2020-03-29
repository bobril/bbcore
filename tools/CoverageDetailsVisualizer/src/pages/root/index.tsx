import * as b from "bobril";
import * as model from "../../model/index";
import { cover100 } from "../../styles";

export function RootPage(data: { page: () => b.IBobrilChildren }): b.IBobrilNode {
    var store = b.useStore(() => new model.CoverageAppModel());
    store.update();
    b.useProvideContext(model.CoverageContext, store);
    if (store.json.busy) return <div>Loading coverage details ...</div>;
    var result = store.json.result;
    if (result instanceof Error) {
        return <div>Error loading: {result.message}</div>;
    }
    return <div style={cover100}>{data.page()}</div>;
}
