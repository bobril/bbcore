"use strict";
exports.wrapWebComponent = wrapWebComponent;

const isFunc_1 = require("./isFunc");

const cssInJs_1 = require("./cssInJs");

function wrapWebComponent(name, props = [], events) {
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
            if (d.style != undefined) cssInJs_1.style(me, d.style);
        },
        handleGenericEvent(ctx, name, param) {
            let handler = ctx.data[name];
            if (isFunc_1.isFunction(handler)) {
                return handler(param);
            }
        }
    };
    if (events != undefined) {
        const eventProps = Object.keys(events);
        component.init = (ctx => {
            ctx.evMap = undefined;
        });
        component.postInitDom = component.postUpdateDom = (ctx => {
            let d = ctx.data;
            for (const n of eventProps) {
                let e = d[n];
                if (e == undefined) continue;
                let es = ctx.evMap;
                if (es == undefined) {
                    es = new Map();
                    ctx.evMap = es;
                }
                let en = events[n];
                if (!es.has(en)) {
                    let el = {
                        handleEvent(event) {
                            e(event);
                        }
                    };
                    ctx.me.element.addEventListener(en, el);
                    es.set(en, el);
                }
            }
        });
        component.destroy = (ctx => {
            let es = ctx.evMap;
            if (es != undefined) {
                es.forEach(function(value, key) {
                    this.removeEventListener(key, value);
                }, ctx.me.element);
            }
        });
    }
    return data => ({
        data: data ?? {},
        component
    });
}

