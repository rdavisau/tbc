dotnet publish -c Release -o output/win -r win-x64 -f net7.0 -p:PublishSingleFile=true -p:PackAsTool=false -p:DebugType=None --self-contained src/heads/tbc.host.console/tbc.host.console.csproj
dotnet publish -c Release -o output/macos-x64 -r osx-x64 -f net7.0 -p:PublishSingleFile=true -p:PackAsTool=false -p:DebugType=None --self-contained src/heads/tbc.host.console/tbc.host.console.csproj
dotnet publish -c Release -o output/macos-arm64 -r osx-arm64 -f net7.0 -p:PublishSingleFile=true -p:PackAsTool=false -p:DebugType=None --self-contained src/heads/tbc.host.console/tbc.host.console.csproj

dotnet build -c Release src/components/tbc.core/tbc.core.csproj
dotnet build -c Release src/components/tbc.target/tbc.target.csproj
dotnet build -c Release src/components/tbc.host/tbc.host.csproj
dotnet build -c Release src/heads/tbc.host.console/tbc.host.console.csproj

dotnet pack -c Release -o output/nuget /p:PackageVersion=1.0.$BUILD_BUILDNUMBER src/components/tbc.core/tbc.core.csproj 
dotnet pack -c Release -o output/nuget /p:PackageVersion=1.0.$BUILD_BUILDNUMBER src/components/tbc.target/tbc.target.csproj 
dotnet pack -c Release -o output/nuget /p:PackageVersion=1.0.$BUILD_BUILDNUMBER src/components/tbc.host/tbc.host.csproj 
dotnet pack -c Release -o output/dotnet_tool /p:PackageVersion=1.0.$BUILD_BUILDNUMBER src/heads/tbc.host.console/tbc.host.console.csproj
