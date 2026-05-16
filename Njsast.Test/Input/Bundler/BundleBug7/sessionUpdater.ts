import { sessionTimer, setSessionTimer } from "./sessionTimer";

const updateSessionInterval = 60 * 1000;

export const start = function (): void {
    if (!sessionTimer) {
        setSessionTimer(setInterval(() => {}, updateSessionInterval));
    }
};
