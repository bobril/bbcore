import { sessionTimer, setSessionTimer } from "./sessionTimer";

export const stop = function (): void {
    if (sessionTimer) {
        clearInterval(<number>sessionTimer);
        setSessionTimer(undefined);
    }
};
