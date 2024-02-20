cd ..\src\FastGateway.Client
dotnet publish -c Release -r linux-x64 -o ../../output/linux64-client /p:UseAppHost=false
