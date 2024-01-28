cd src\Gateway
dotnet publish -c Release -r win-x64  -o ../../output/win64-framework  --self-contained true -p:PublishSingleFile=true  -p:PublishTrimmed=true
