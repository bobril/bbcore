export function apply(ctx: unknown, box: { left: number; top: number }) {
    setPosition(ctx, {
        x: box.left,
        y: box.top,
    });
}

export const { set: setPosition } = register<{ x: number; y: number }>()("position");
