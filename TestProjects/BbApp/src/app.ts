import * as b from "bobril";
import * as g from "bobril-g11n";
import lightSwitch from "./lightSwitch";

interface IPageCtx extends b.IBobrilCtx {
    counter: number;
}

b.asset("bootstrap/css/bootstrap.css");

let headerStyle = b.styleDef({ backgroundColor: "green", padding: 10 }, undefined, "header");

let semitransparentLight = b.sprite("light.png", "rgba(200,100,0,0.5)");

function dontDoThis(cn: string) {
    b.styleDef({}, undefined, cn);
}

dontDoThis("try");

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

        me.children = [
            b.style({ tag: "h1", children: g.t("Hello World! {c, number}", { c: ctx.counter }) }, headerStyle),
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
                tag: "img",
                style: { display: "inline-block", verticalAlign: "unset" },
                attrs: { src: b.asset("light.png") }
            },
            b.styledDiv(" ", { backgroundColor: "blue", display: "inline-block" }, semitransparentLight),
            b.styledDiv(" ", { backgroundColor: "green", display: "inline-block" }, semitransparentLight),
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
                children: "Current locale: " + g.getLocale()
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
