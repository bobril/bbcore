"use strict";
var ClientsPageStore = /** @class */ (function () {
    function ClientsPageStore() {
    }
    ClientsPageStore.prototype.clear = function () {
        exports.clientsPageStore = new ClientsPageStore();
    };
    return ClientsPageStore;
}());
exports.ClientsPageStore = ClientsPageStore;
exports.clientsPageStore = new ClientsPageStore();
