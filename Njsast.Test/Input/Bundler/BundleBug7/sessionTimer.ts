interface ISessionTimer {
    ref(): void;
    unref(): void;
}

export let sessionTimer: ISessionTimer | number | undefined;

export function setSessionTimer(newTimer: ISessionTimer | number | undefined): void {
    sessionTimer = newTimer;
}
