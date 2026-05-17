"use strict";
exports.ColumnOwner = void 0;

exports.makeColumn = makeColumn;

exports.makeDialog = makeDialog;

exports.renderDrag = renderDrag;

function makeColumn(readOnly, item) {
    return {
        valueGetter: () => readOnly ? item.name : b.createElement(Link, {
            text: item.name,
            hasEllipsis: true
        })
    };
}

function makeDialog(isLoading, close) {
    return modal({
        headerSettings: {
            actionButtonsContent: hasActions() ? actionBar([ createButton(false, () => {
                close();
            }) ], {
                additionalRightContent: [ getFilter() ],
                actionButtonsThemeActive: true
            }) : undefined,
            isDividerVisible: !hasActions()
        },
        content: isLoading ? [ b.createElement(Loader, {
            label: "Loading",
            background: true
        }) ] : b.createElement(DataTable, {
            onContextMenu: item => toContextMenuContent([ createButton(false, () => {
                close();
                selectItem(item);
            }) ]),
            emptyState: {
                message: "Empty"
            },
            tooltipGetter: item => {
                const text = getTooltip(item);
                return {
                    tooltipMessage: (text === undefined ? undefined : text) ?? ""
                };
            }
        }),
        buttons: [ b.createElement(Button, {
            label: "Close",
            onClick: () => close()
        }) ]
    });
}

function renderDrag(isReadOnly) {
    return b.createElement(DataTable, {
        isDraggable: isReadOnly ? undefined : () => true
    });
}

class ColumnOwner {
    getColumns() {
        return [ {
            valueGetter: item => this.data.isReadOnly ? item.name : b.createElement(Link, {
                text: item.name,
                onClick: () => this.data.onItemLinkClick(item.id, item.type),
                hasEllipsis: true
            })
        } ];
    }
}

exports.ColumnOwner = ColumnOwner;
