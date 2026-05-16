import * as sessionUpdater from "./sessionUpdater";
import * as sessionStopper from "./sessionStopper";

sessionUpdater.start();
console.log("working");
sessionStopper.stop();
