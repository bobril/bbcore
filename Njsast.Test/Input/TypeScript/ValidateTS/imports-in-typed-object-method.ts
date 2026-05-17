import { getState } from "state-lib";
import { Cursor } from "./cursor";

interface Ctx {}
interface Node {}
interface State {
    value: string;
}

export default createComponent({
    render(ctx: Ctx, me: Node) {
        const state = getState<State>(Cursor);
        me.value = state.value;
    }
});
