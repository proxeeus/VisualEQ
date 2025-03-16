@echo off
setlocal enabledelayedexpansion

REM VisualEQ Zone Loader Helper Script
REM Usage: load_zone.bat [path_to_everquest] [zone_name]
REM If no arguments are provided, it will show a menu of available zones

REM Config file for storing EQ path
set "CONFIG_FILE=%~dp0eq_config.txt"

REM Check if we're loading directly with arguments
if NOT "%~1"=="" (
    if NOT "%~2"=="" (
        set "EQ_PATH=%~1"
        set "ZONE_NAME=%~2"
        set "CHAR_NAME=%~2_chr"
        REM Store the provided path
        echo %~1>"%CONFIG_FILE%"
        goto convert_zone
    )
)

:menu
cls
echo ===================================
echo VisualEQ Zone Loader
echo ===================================
echo.
echo Available Zones:
echo.

REM Count available zones
set ZONE_COUNT=0
set "ZONE_LIST="

REM Check for zones in ConverterApp directory
pushd ConverterApp
for %%f in (*_oes.zip) do (
    set "FILENAME=%%f"
    set "ZONENAME=!FILENAME:_oes.zip=!"
    if NOT "!ZONENAME!"=="!FILENAME!" (
        if NOT "!ZONENAME:_chr=!"=="!ZONENAME!" (
            REM Skip character files
        ) else (
            set /a ZONE_COUNT+=1
            set "ZONE_LIST=!ZONE_LIST!;!ZONENAME!"
            echo !ZONE_COUNT!. !ZONENAME!
        )
    )
)
popd

REM Check for zones in VisualEQ directory
pushd VisualEQ
for %%f in (*_oes.zip) do (
    set "FILENAME=%%f"
    set "ZONENAME=!FILENAME:_oes.zip=!"
    if NOT "!ZONENAME!"=="!FILENAME!" (
        if NOT "!ZONENAME:_chr=!"=="!ZONENAME!" (
            REM Skip character files
        ) else (
            REM Check if we already counted this zone
            echo !ZONE_LIST! | findstr /C:";!ZONENAME!;" > nul
            if errorlevel 1 (
                set /a ZONE_COUNT+=1
                set "ZONE_LIST=!ZONE_LIST!;!ZONENAME!;"
                echo !ZONE_COUNT!. !ZONENAME!
            )
        )
    )
)
popd

echo.
echo !ZONE_COUNT! zone(s) found
echo.
echo C. Convert a new zone
echo Q. Quit
echo.

if %ZONE_COUNT% EQU 0 (
    echo No pre-converted zones found.
    echo.
    choice /C CQ /N /M "Choose an option [C,Q]: "
    if errorlevel 2 goto :eof
    if errorlevel 1 goto convert_new
)

set /p CHOICE="Enter choice [1-%ZONE_COUNT%, C, Q]: "

if /i "%CHOICE%"=="Q" goto :eof
if /i "%CHOICE%"=="C" goto convert_new

REM Check if the input is a number and within range
set "CHOICE=%CHOICE: =%"
set /a "CHOICE_NUM=%CHOICE%" 2>nul
if "%CHOICE_NUM%"=="%CHOICE%" (
    if %CHOICE_NUM% GEQ 1 (
        if %CHOICE_NUM% LEQ %ZONE_COUNT% (
            REM Extract the zone name from the list
            set N=1
            for %%z in (%ZONE_LIST:;= %) do (
                if !N!==%CHOICE_NUM% (
                    set ZONE_NAME=%%z
                    set CHAR_NAME=%%z_chr
                    goto launch_zone
                )
                set /a N+=1
            )
        )
    )
)

echo Invalid choice.
pause
goto menu

:convert_new
cls
echo Enter the path to your EverQuest directory and the zone name.
echo.

REM Try to read the stored EQ path
if exist "%CONFIG_FILE%" (
    for /f "usebackq delims=" %%i in ("%CONFIG_FILE%") do set "STORED_PATH=%%i"
    echo Using stored EverQuest path: !STORED_PATH!
    set "EQ_PATH=!STORED_PATH!"
) else (
    set /p "EQ_PATH=EverQuest path: "
    echo !EQ_PATH!>"%CONFIG_FILE%"
)

set /p "ZONE_NAME=Zone name: "
set "CHAR_NAME=!ZONE_NAME!_chr"

:convert_zone
REM The existing conversion logic
echo === Checking if zone %ZONE_NAME% already exists ===
if exist "ConverterApp\%ZONE_NAME%_oes.zip" (
    echo Zone file already exists in ConverterApp directory, skipping conversion.
) else if exist "VisualEQ\%ZONE_NAME%_oes.zip" (
    echo Zone file already exists in VisualEQ directory, will be copied to correct location.
) else (
    echo === Converting Zone: %ZONE_NAME% ===
    pushd ConverterApp
    dotnet run "%EQ_PATH%" %ZONE_NAME%
    if errorlevel 1 (
        echo Error: Zone conversion failed!
        popd
        pause
        goto menu
    )
    popd
)

echo === Ensuring zone file is in the correct location ===
pushd ConverterApp
REM Check if the file is generated in the VisualEQ directory instead
if exist "..\VisualEQ\%ZONE_NAME%_oes.zip" (
    echo Found zone file in VisualEQ directory, copying to ConverterApp
    copy "..\VisualEQ\%ZONE_NAME%_oes.zip" ".\"
) else if not exist "%ZONE_NAME%_oes.zip" (
    echo Error: Zone file %ZONE_NAME%_oes.zip not found in either directory!
    popd
    pause
    goto menu
)

echo === Checking if character data already exists ===
if exist "%CHAR_NAME%_oes.zip" (
    echo Character file already exists in ConverterApp directory, skipping conversion.
) else if exist "..\VisualEQ\%CHAR_NAME%_oes.zip" (
    echo Character file already exists in VisualEQ directory, will be copied to correct location.
) else (
    echo === Converting Characters for Zone: %CHAR_NAME% ===
    dotnet run "%EQ_PATH%" %CHAR_NAME%
    if errorlevel 1 (
        echo Warning: Character conversion failed or not available.
    )
)

echo === Ensuring character file is in the correct location ===
REM Check if the character file is generated in the VisualEQ directory instead
if exist "..\VisualEQ\%CHAR_NAME%_oes.zip" (
    echo Found character file in VisualEQ directory, copying to ConverterApp
    copy "..\VisualEQ\%CHAR_NAME%_oes.zip" ".\"
)
popd

:launch_zone
echo === Launching VisualEQ with Zone: %ZONE_NAME% ===
pushd VisualEQ
REM Pass only the zone name - the application will append _oes.zip itself
dotnet run -c Debug --no-build %ZONE_NAME%
popd

echo Done.
pause
goto menu

:eof 