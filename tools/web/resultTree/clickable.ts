import * as b from "bobril";

export function clickable(content: b.IBobrilChildren, action: () => void): b.IBobrilNode {
    return {
        children: content,
        component: {
            onClick() {
                action();
                return true;
            }
        }
    };
}
