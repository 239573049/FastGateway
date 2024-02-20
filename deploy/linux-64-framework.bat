cd ..\src\FastGateway
dotnet publish -c Release -r linux-x64  -o ../../output/linux64-framework  --self-contained true -p:PublishSingleFile=true  -p:PublishTrimmed=true
