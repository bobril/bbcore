type Position = { x: number };

export function update(module: any, updated: any) {
    module.viewData.x = (updated.values.position as Position).x;
}
