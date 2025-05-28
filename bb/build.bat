dotnet publish -c Release -p:DebugType=None -p:DebugSymbols=false -r win-x64
dotnet publish -c Release -p:DebugType=None -p:DebugSymbols=false -r win-arm64
dotnet publish -c Release -p:DebugType=None -p:DebugSymbols=false -r linux-x64
dotnet publish -c Release -p:DebugType=None -p:DebugSymbols=false -r osx-x64
dotnet publish -c Release -p:DebugType=None -p:DebugSymbols=false -r osx-arm64
