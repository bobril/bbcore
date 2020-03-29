import * as b from "bobril";
import * as bobx from "bobx";
import { fetchJson } from "../fetch/index";

export interface CoverageDetail {
    statements: string;
    statementsCovered: number;
    statementsTotal: number;
    statementsMaxHits: number;
    conditions: string;
    conditionsCoveredPartially: number;
    conditionsCoveredFully: number;
    conditionsTotal: number;
    conditionsMaxHits: number;
    functions: string;
    functionsCovered: number;
    functionsTotal: number;
    functionsMaxHits: number;
    switchBranches: string;
    switchBranchesCovered: number;
    switchBranchesTotal: number;
    switchBranchesMaxHits: number;
    lines: string;
    linesCoveredPartially: number;
    linesCoveredFully: number;
    linesTotal: number;
    linesMaxHits: number;
    subDirectories?: string[];
    subFiles?: string[];
    encodedRanges?: number[];
    source?: string;
}

export interface CoverageDetailsJson {
    [name: string]: CoverageDetail;
}

var downloadCoverage = bobx.asyncComputed(function*() {
    try {
        let result = (yield fetchJson<CoverageDetailsJson>(
            b.asset("../../sampleData/coverage-details.json")
        )) as CoverageDetailsJson;
        return result;
    } catch (error) {
        return error as Error;
    }
});

export class CoverageAppModel {
    constructor() {}
    update() {
        this.json = downloadCoverage();
    }
    json: bobx.IAsyncComputed<CoverageDetailsJson | Error>;
}

export const CoverageContext = b.createContext<CoverageAppModel>(undefined);
