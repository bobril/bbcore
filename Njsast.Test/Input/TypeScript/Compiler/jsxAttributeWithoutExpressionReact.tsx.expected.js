"use strict";
// @target: es2015
//@jsx: react
b.createElement(View, null,
    b.createElement(ListView, { refreshControl: b.createElement(RefreshControl, { onRefresh: true, refreshing: true }), dataSource: this.state.ds, renderRow: true }));
