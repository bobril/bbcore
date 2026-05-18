"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
const simpleAction = (payload) => ({
    type: "SIMPLE_ACTION",
    payload
});
const thunkAction = (param1, param2) => async (dispatch, { foo }) => {
    return foo;
};
class TestComponent extends Component {
}
const mapDispatchToProps = { simpleAction, thunkAction };
const Test1 = connect(null, mapDispatchToProps)(TestComponent);
