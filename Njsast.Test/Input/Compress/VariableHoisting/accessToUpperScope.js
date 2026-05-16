var registryEvents;
function addEvent(name_index) {
    if (registryEvents == null) registryEvents = {};
    var list = registryEvents[name_index] || [];
    registryEvents[name_index] = list;
}