import * as b from "bobril";

@b.bind
export class Store {
    private static maxValue = 1;

    zoomIn() {
        return Store.maxValue;
    }
}
