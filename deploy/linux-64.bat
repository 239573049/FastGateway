cd ..\src\FastGateway
dotnet publish -c Release -r linux-x64  -o ../../output/linux64  /p:UseAppHost=false
