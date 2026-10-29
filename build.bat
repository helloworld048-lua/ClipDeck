@echo off
chcp 65001 >nul
where dotnet >nul 2>nul
if errorlevel 1 (
  echo Не найден .NET SDK 8. Скачайте: https://dotnet.microsoft.com/download/dotnet/8.0
  exit /b 1
)
echo Сборка автономной версии (работает на любом Windows 10/11, около 70 МБ)...
dotnet publish ClipDeck.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish
if errorlevel 1 exit /b 1
echo.
echo Готово: publish\ClipDeck.exe
