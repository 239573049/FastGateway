cd src\Gateway.Client
dotnet publish -c Release -r win-x64 --self-contained true  /p:DebugType=None /p:DebugSymbols=false -o ../../output/win64-aot
