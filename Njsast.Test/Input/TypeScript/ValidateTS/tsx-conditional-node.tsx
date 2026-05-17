export function renderNode(flag: boolean) {
    return flag ? (
        <Panel title="Ready" />
    ) : (
        <Loader label="Loading" />
    );
}

export function renderWrapper(Component: () => unknown, Parent?: (props: unknown) => unknown) {
    return Parent ? <Parent><Component /></Parent> : <Component />;
}
