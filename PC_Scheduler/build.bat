@echo off
echo Building PC Scheduler...

:: Option 1: Framework-dependent (smaller, requires .NET 8 runtime)
dotnet publish -c Release -r win-x64 -o publish-fd

:: Option 2: Self-contained (larger, no runtime needed)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish-sc

echo Done!
pause
