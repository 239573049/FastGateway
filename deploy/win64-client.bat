cd ..\src\Gateway.Client
dotnet publish -c Release -r win-x64 -o ../../output/win64-client   /p:UseAppHost=false
