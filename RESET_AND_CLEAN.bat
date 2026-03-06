@echo off
REM Clean up settings and database to start fresh
REM This will delete:
REM - Recent library paths
REM - Watch status history
REM - All settings

echo Cleaning WatchMark data...

REM Delete settings
if exist "%LOCALAPPDATA%\WatchMark\appsettings.json" (
    echo Deleting settings file...
    del /q "%LOCALAPPDATA%\WatchMark\appsettings.json"
)

REM Delete database
if exist "%LOCALAPPDATA%\WatchMark\watchstatus.db" (
    echo Deleting database file...
    del /q "%LOCALAPPDATA%\WatchMark\watchstatus.db"
)

REM Also delete from app directory if it exists
if exist "data\watchstatus.db" (
    echo Deleting local database file...
    del /q "data\watchstatus.db"
)

echo.
echo Clean complete! Start WatchMark.exe to begin fresh.
pause
