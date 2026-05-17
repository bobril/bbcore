import { getState } from "state-lib";
import { Cursor } from "./cursor";

interface State {
    value: string;
}

export const value = getState<State>(Cursor).value;
