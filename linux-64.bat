cd src\Gateway
dotnet publish -c Release -r linux-x64  -o ../../output/linux64  /p:UseAppHost=false
