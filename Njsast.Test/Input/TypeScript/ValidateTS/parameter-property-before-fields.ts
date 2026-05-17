export class BatchCommands {
    commands: string[] = [];

    constructor(private readonly errorCallback: () => void) {
    }
}
