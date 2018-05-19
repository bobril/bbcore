import * as b from 'bobril';

const iconShine = b.sprite("light.png", "#80ff80");
const iconOff = b.sprite("light.png", "#e03030");

export interface IData {
    value: boolean;
    onChange(value: boolean): void;
}

interface ICtx extends b.IBobrilCtx {
    data: IData;
}

export default b.createComponent<IData>({
    render(ctx: ICtx, me: b.IBobrilNode) {
        b.style(me, ctx.data.value ? iconShine : iconOff);
    },
    onClick(ctx: ICtx): boolean {
        ctx.data.onChange(!ctx.data.value);
        return true;
    }
});
