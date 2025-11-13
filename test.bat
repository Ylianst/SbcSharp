@echo off
echo Building SBC Library...
dotnet build SBC.csproj -c Release

if %ERRORLEVEL% NEQ 0 (
    echo Build failed!
    exit /b 1
)

echo.
echo Running WAV Test...
echo.
dotnet run --project . WavTest.cs %*

if %ERRORLEVEL% NEQ 0 (
    echo Test failed!
    exit /b 1
)

echo.
echo Test completed successfully!
