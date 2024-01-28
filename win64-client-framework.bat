cd src\Gateway.Client
dotnet publish -c Release -r win-x64 -o ../../output/win64-client-framework --self-contained true -p:PublishSingleFile=true  -p:PublishTrimmed=true
