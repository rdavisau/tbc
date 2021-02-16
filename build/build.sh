dotnet publish -c Release -o output/win -r win-x64 -p:PublishSingleFile=true -p:DebugType=None src/heads/tbc.host.console/tbc.host.console.csproj
dotnet publish -c Release -o output/macos -r osx-x64 -p:PublishSingleFile=true -p:DebugType=None src/heads/tbc.host.console/tbc.host.console.csproj

dotnet build -c Release src/components/tbc.target/tbc.target.csproj
dotnet pack -c Release -o output/nuget /p:PackageVersion=0.0.$BUILD_BUILDNUMBER src/components/tbc.target/tbc.target.csproj 