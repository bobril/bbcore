import { SuiteOrTest } from './state';

export interface SeparatedTests {
    failed: SuiteOrTest[];
    skipped: SuiteOrTest[];
    logged: SuiteOrTest[];
    passed: SuiteOrTest[];
}

export function analyze(results: SuiteOrTest[]): SeparatedTests {
    let allSuitesAndTests = getAllSuitesAndTests(results);
    let separatedTests = separateTests(allSuitesAndTests);
    return {
        failed: buildSuiteTrees(separatedTests.failed, allSuitesAndTests),
        skipped: buildSuiteTrees(separatedTests.skipped, allSuitesAndTests),
        logged: buildSuiteTrees(separatedTests.logged, allSuitesAndTests),
        passed: buildSuiteTrees(separatedTests.passed, allSuitesAndTests),
    }
}

function getAllSuitesAndTests(roots: SuiteOrTest[]): { [key: number]: SuiteOrTest } {
    let all: { [key: number]: SuiteOrTest } = {};
    function traverse(results: SuiteOrTest[]): void {
        for (let i = 0; i < results.length; i++) {
            let suiteOrTest = results[i];
            if (suiteOrTest.isSuite) {
                all[suiteOrTest.id] = suiteOrTest;
                traverse(results[i].nested);
            } else {
                all[suiteOrTest.id] = suiteOrTest;
            }
        }
    }
    traverse(roots);
    return all;
}

function separateTests(all: { [key: number]: SuiteOrTest }): SeparatedTests {
    let failed: SuiteOrTest[] = [];
    let skipped: SuiteOrTest[] = [];
    let logged: SuiteOrTest[] = [];
    let passed: SuiteOrTest[] = [];
    for (var key in all) {
        var suiteOrTest = all[key];
        if (suiteOrTest.isSuite) {
            continue;
        }
        if (suiteOrTest.failures && suiteOrTest.failures.length) {
            failed.push(suiteOrTest);
        } else if (suiteOrTest.logs && suiteOrTest.logs.length) {
            logged.push(suiteOrTest);
        } else if (suiteOrTest.skipped) {
            skipped.push(suiteOrTest);
        } else {
            passed.push(suiteOrTest);
        }
    }
    return { failed, skipped, logged, passed };
}

function buildSuiteTrees(tests: SuiteOrTest[], all: { [key: number]: SuiteOrTest }): SuiteOrTest[] {
    let nodes: { [key: number]: SuiteOrTest } = {};
    for (let i = 0; i < tests.length; i++) {
        let current = tests[i];
        nodes[current.id] = cloneSuiteOrTest(current);        
        while (current.parentId != 0) {
            if (!nodes[current.id]) {
                nodes[current.id] = cloneSuiteOrTest(current);
            }
            current = nodes[current.id];

            let parent = all[current.parentId];
            if (nodes[parent.id]) {
                if (nodes[parent.id].nested.indexOf(current) == -1) {
                    nodes[parent.id].nested.push(current);
                }
            } else {
                nodes[parent.id] = cloneSuiteOrTest(parent);
                nodes[parent.id].nested.push(current);
            }

            current = nodes[parent.id];
        }
    }
    return Object.keys(nodes).map(key => nodes[key]).filter(node => node.parentId == 0);
}

function cloneSuiteOrTest(suiteOrTest: SuiteOrTest): SuiteOrTest {
    return {
        id: suiteOrTest.id,
        parentId: suiteOrTest.parentId,
        isSuite: suiteOrTest.isSuite,
        name: suiteOrTest.name,
        skipped: suiteOrTest.skipped,
        failure: suiteOrTest.failure,
        duration: suiteOrTest.duration,
        failures: suiteOrTest.failures,
        nested: [],
        logs: suiteOrTest.logs
    };
}