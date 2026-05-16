import { IBubblingAndBroadcastEvents, IBobrilStyles, IBobrilChildren, IBobrilNode, IBobrilCtx, IBobrilComponent, GenericEventResult } from "./core";

import { isFunction } from "./isFunc";

import { style } from "./cssInJs";

export function wrapWebComponent(name, props = [], events) {
    props = [ "id", "slot", ...props ];
    const component = {
        id: name,
        render(ctx, me) {
            let d = ctx.data;
            me.tag = name;
            let attrs = {};
            for (const n of props) {
                attrs[n] = d[n];
            }
            me.attrs = attrs;
            me.children = d.children;
            if (d.style != undefined) style(me, d.style);
        },
        handleGenericEvent(ctx, name, param) {
            let handler = ctx.data[name];
            if (isFunction(handler)) {
                return handler(param);
            }
        }
    };
}

