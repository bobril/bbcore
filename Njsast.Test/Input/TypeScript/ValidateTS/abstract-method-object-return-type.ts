abstract class VersionsStore {
    protected abstract fetchVersions(): Promise<{ versions: IVersionItem[]; totalCount: number }>;
}
