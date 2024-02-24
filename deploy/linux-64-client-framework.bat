cd ..\src\FastGateway.Client
dotnet publish -c Release -r linux-x64 -o ../../output/linux64-client-framework  --self-contained true -p:PublishSingleFile=true  -p:PublishTrimmed=true
