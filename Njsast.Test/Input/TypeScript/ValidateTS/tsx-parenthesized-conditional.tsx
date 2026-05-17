export function makeColumn(readOnly: boolean, item: { name: string }) {
    return {
        valueGetter: () =>
            readOnly ? (
                item.name
            ) : (
                <Link text={item.name} hasEllipsis />
            ),
    };
}

export function makeDialog(isLoading: boolean, close: () => void) {
    return modal({
        headerSettings: {
            actionButtonsContent: hasActions()
                ? actionBar<Item>(
                      [
                          createButton(false, () => {
                              close();
                          }),
                      ],
                      {
                          additionalRightContent: [getFilter()],
                          actionButtonsThemeActive: true,
                      },
                  )
                : undefined,
            isDividerVisible: !hasActions(),
        },
        content: isLoading ? (
            [<Loader label="Loading" background />]
        ) : (
            <DataTable
                onContextMenu={(item) =>
                    toContextMenuContent([
                        createButton(false, () => {
                            close();
                            selectItem(item);
                        }),
                    ])
                }
                emptyState={{ message: "Empty" }}
                tooltipGetter={(item) => {
                    const text = getTooltip(item);
                    return {
                        tooltipMessage: (text === undefined ? undefined : text) ?? "",
                    };
                }}
            />
        ),
        buttons: [
            <Button label="Close" onClick={() => close()} />,
        ],
    });
}

export function renderDrag(isReadOnly: boolean) {
    return <DataTable isDraggable={isReadOnly ? undefined : () => true} />;
}

export class ColumnOwner {
    data: { isReadOnly: boolean; onItemLinkClick(id: number, type: string): void };

    getColumns() {
        return [
            {
                valueGetter: (item: { id: number; type: string; name: string }) =>
                    this.data.isReadOnly ? (
                        item.name
                    ) : (
                        <Link text={item.name} onClick={() => this.data.onItemLinkClick(item.id, item.type)} hasEllipsis />
                    ),
            },
        ];
    }
}
