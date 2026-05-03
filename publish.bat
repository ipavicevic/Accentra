@echo off
setlocal

echo Publishing Accentra EXE (Release, win-x64, self-contained, single-file)...
echo (For MSI, push a v* tag — GitHub Actions builds the MSI)
echo.

dotnet publish Accentra.csproj ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -o publish

if errorlevel 1 (
    echo.
    echo Publish failed.
    exit /b 1
)

echo.
echo Done. Output: %~dp0publish\Accentra.exe
endlocal
