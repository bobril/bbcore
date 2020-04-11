import * as b from "bobril";

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

declare var bbcoverage: CoverageDetailsJson;

export enum CovBarDisplayType {
    Percentages = 0,
    RealNumbers = 1,
    MaxHits = 2,
    TotalTypes = 3,
}

@b.bind
export class CoverageAppModel {
    constructor() {
        this.json = bbcoverage;
        this.covBarDisplayType = CovBarDisplayType.Percentages;
    }
    json: CoverageDetailsJson;
    covBarDisplayType: CovBarDisplayType;

    rotateCovBarDisplayType(): void {
        this.covBarDisplayType++;
        if (this.covBarDisplayType == CovBarDisplayType.TotalTypes) {
            this.covBarDisplayType = CovBarDisplayType.Percentages;
        }
        b.invalidate();
    }
}

export const CoverageContext = b.createContext<CoverageAppModel>(undefined);
