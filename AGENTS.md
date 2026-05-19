@/Users/borisletocha/.codex/RTK.md

When adding a new source file under `Njsast` that is used by code linked into
`Bbcore.Lib`, also add it to `Bbcore.Lib/Bbcore.Lib.csproj` as a linked
`Compile` item and verify `rtk dotnet build Bbcore.Lib/Bbcore.Lib.csproj
--no-restore`.
