"use strict";
exports.renderNode = renderNode;

exports.renderWrapper = renderWrapper;

function renderNode(flag) {
    return flag ? b.createElement(Panel, {
        title: "Ready"
    }) : b.createElement(Loader, {
        label: "Loading"
    });
}

function renderWrapper(Component, Parent) {
    return Parent ? b.createElement(Parent, null, b.createElement(Component, null)) : b.createElement(Component, null);
}
