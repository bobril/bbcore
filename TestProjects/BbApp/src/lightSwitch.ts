import * as b from "bobril";

const iconLight = b.sprite("light.png");

export interface IData {
    value: boolean;
    onChange(value: boolean): void;
}

interface ICtx extends b.IBobrilCtx {
    data: IData;
}

export default b.createComponent<IData>({
    render(ctx: ICtx, me: b.IBobrilNode) {
        const color = ctx.data.value ? "#80ff80" : "#e03030";
        b.style(me, b.spriteWithColor(iconLight, color));
    },
    onClick(ctx: ICtx): boolean {
        ctx.data.onChange(!ctx.data.value);
        return true;
    },
});
