import * as b from "bobril";

const iconLight = b.sprite("light.png");

export default b.createComponent({
    render(ctx, me) {
        const color = ctx.data.value ? "#80ff80" : "#e03030";
        b.style(me, b.spriteWithColor(iconLight, color));
    },
    onClick(ctx) {
        ctx.data.onChange(!ctx.data.value);
        return true;
    }
});

