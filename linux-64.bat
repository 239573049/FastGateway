cd src\Gateway.Client
dotnet publish -c Release -r linux-x64  /p:DebugType=None /p:DebugSymbols=false -o ../../output/linux64
