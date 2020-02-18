import * as b from "bobril";
import * as g from "bobril-g11n";
import * as deep from "bobril-g11n/src/jsonp";
import lightSwitch from "./lightSwitch";
import * as json from "./json.json";
import "./polyfills";
import * as styles from "./styles";

declare var process: { env: Record<string, string> };
declare var DEBUG: boolean;

interface IPageCtx extends b.IBobrilCtx {
    counter: number;
}

b.asset("bootstrap/css/bootstrap.css");

let headerStyle = b.styleDef({ backgroundColor: "green", padding: 10 }, undefined, "header");

let semitransparentLight = b.sprite("light.png", "rgba(200,100,0,0.5)");
let undefinedLight = b.sprite("light.png", undefined);

function dontDoThis(cn: string) {
    b.styleDef({}, undefined, cn);
}

dontDoThis("try");

if (!b.isFunction(deep.jsonp)) console.error("deep import");

let switchValue = false;

let page = b.createVirtualComponent({
    init(ctx: IPageCtx) {
        ctx.counter = 0;
        setInterval(() => {
            ctx.counter++;
            b.invalidate();
        }, 1000);
    },
    render(ctx: IPageCtx, me: b.IBobrilNode, _oldMe?: b.IBobrilCacheNode): void {
        let m = g.getMoment();
        g.loadSerializationKeys();

        me.children = [
            b.style(
                {
                    tag: "h1",
                    children: g.t("Hello World! {c, number} {test}", { c: ctx.counter, test: json.test })
                },
                headerStyle
            ),
            {
                tag: "p",
                children: [
                    "See examples on ",
                    {
                        tag: "a",
                        attrs: { href: "https://github.com/Bobris/Bobril" },
                        children: g.t("Bobril GitHub pages")
                    }
                ]
            },
            {
                tag: "p",
                children: [
                    g.serializationKeysLoaded()
                        ? g.f(
                              g.serializeMessage(
                                  g.dt("Delayed translation message {param}", {
                                      param: g.dt("with delayed param")
                                  })
                              )
                          )
                        : "Loading ..."
                ]
            },
            {
                tag: "img",
                style: { display: "inline-block", verticalAlign: "unset" },
                attrs: { src: b.asset("light.png") }
            },
            b.styledDiv(" ", { backgroundColor: "blue", display: "inline-block" }, semitransparentLight),
            b.styledDiv(" ", { backgroundColor: "green", display: "inline-block" }, semitransparentLight),
            b.styledDiv(" ", { display: "inline-block" }, undefinedLight),
            lightSwitch({
                value: switchValue,
                onChange: () => {
                    switchValue = !switchValue;
                }
            }),
            {
                tag: "span",
                className: "glyphicon glyphicon-star",
                attrs: { ariaHidden: true }
            },
            {
                tag: "p",
                children: "DEBUG: " + DEBUG
            },
            {
                tag: "p",
                children: "Current locale: " + g.getLocale()
            },
            {
                tag: "p",
                children: "process.env.NODE_ENV: " + process.env.NODE_ENV
            },
            {
                tag: "p",
                children: "process.env.UnKnOwN: " + process.env.UnKnOwN
            },
            {
                tag: "div",
                style: { display: "table", tableLayout: "fixed", width: "100%" },
                children: {
                    tag: "div",
                    style: {
                        display: "table-cell",
                        whiteSpace: "nowrap",
                        textOverflow: "ellipsis",
                        overflow: "hidden"
                    },
                    children: "Path: " + process.env.Path
                }
            },
            {
                tag: "p",
                children: "Moment long date format L: " + (<any>m.localeData()).longDateFormat("L")
            },
            {
                tag: "p",
                children:
                    "Number 123456.789 in format 0,0.00: " +
                    g.f("{arg, number, custom, format:{0,0.00}}", { arg: 123456.789 })
            },
            b.style({ tag: "div", children: "blue on red" }, styles.style1, styles.style2),
            {
                tag: "p",
                children: "cs-CZ",
                component: {
                    onClick: () => {
                        g.setLocale("cs-CZ");
                        return true;
                    }
                }
            }
        ];
    }
});

b.init(() => page({}));
