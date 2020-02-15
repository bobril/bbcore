export interface CompilationResultMessage {
    fileName: string;
    isError: boolean;
    text: string;
    /// startLine, startCol, endLine, endCol all one based
    pos: [number, number, number, number];
}

export interface StackFrame {
    functionName?: string;
    args?: any[];
    fileName?: string;
    lineNumber?: number;
    columnNumber?: number;
}

export interface SuiteOrTest {
    isSuite: boolean;
    id: number;
    parentId: number;
    name: string;
    /// valid only for Test
    stack: StackFrame[] | null;
    skipped: boolean;
    failure: boolean;
    duration: number;
    failures: { message: string; stack: StackFrame[] }[];
    nested: SuiteOrTest[];
    logs: { message: string; stack: StackFrame[] }[];
}

export interface TestResultsHolder extends SuiteOrTest {
    userAgent: string;
    running: boolean;
    testsFailed: number;
    testsSkipped: number;
    testsFinished: number;
    totalTests: number;
}

export interface TestSvrState {
    agents: TestResultsHolder[];
}

export interface IAction {
    type: string;
    id: string;
}

export interface IActionCommand extends IAction {
    type: "command";
    enabled: boolean;
    name: string;
}

export interface IActionCombo extends IAction {
    type: "combo";
    label: string;
    selected: string;
    options: { id: string; name: string }[];
}

export let connected = false;
export let disconnected = false;
export let reconnectDelay = 0;
export let testSvrState: TestSvrState = { agents: [] };
export let testSvrDataVersion = 0;
export let building = false;
export let actions: IAction[] = [];
export let liveReload = false;
export let coverage = false;
export let lastBuildResult: {
    errors: number;
    warnings: number;
    time: number;
    messages: CompilationResultMessage[];
} = { errors: 0, warnings: 0, time: 0, messages: [] };
