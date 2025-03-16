@echo off
setlocal enabledelayedexpansion

REM VisualEQ Character Model Lister Helper Script

REM Check if we need to build the ModelLister tool first
if not exist "ModelLister\bin\Debug\netcoreapp2.1\ModelLister.dll" (
    echo Building ModelLister tool...
    dotnet build ModelLister\ModelLister.csproj > nul
)

REM Check if we're just looking for animations of a specific model in a zone
if "%~2"=="" goto check_zone_param
set ZONE_NAME=%~1
set MODEL_NAME=%~2
set CHAR_NAME=%~1_chr

echo ===================================
echo ANIMATIONS FOR MODEL: %MODEL_NAME% in ZONE: %ZONE_NAME%
echo ===================================
echo.

set MODEL_FILE_FOUND=0

REM First check in ConverterApp directory
if exist "ConverterApp\%CHAR_NAME%_oes.zip" (
    echo Using character file: ConverterApp\%CHAR_NAME%_oes.zip
    dotnet run --project ModelLister\ModelLister.csproj "ConverterApp\%CHAR_NAME%_oes.zip" --anims-only %MODEL_NAME%
    set MODEL_FILE_FOUND=1
) 

REM Then check in VisualEQ directory
if exist "VisualEQ\%CHAR_NAME%_oes.zip" (
    if !MODEL_FILE_FOUND!==1 (
        echo.
        echo Additional character file found:
    )
    echo Using character file: VisualEQ\%CHAR_NAME%_oes.zip
    dotnet run --project ModelLister\ModelLister.csproj "VisualEQ\%CHAR_NAME%_oes.zip" --anims-only %MODEL_NAME%
    set MODEL_FILE_FOUND=1
)

if !MODEL_FILE_FOUND!==0 (
    echo No character file found for zone: %ZONE_NAME%
)

echo.
pause
exit /b 0

:check_zone_param
REM Check if we're listing models for a specific zone passed as parameter
if NOT "%~1"=="" (
    set ZONE_NAME=%~1
    set CHAR_NAME=%~1_chr
    
    echo ===================================
    echo MODELS FOR ZONE: %ZONE_NAME%
    echo ===================================
    echo.
    
    set MODEL_FILE_FOUND=0
    
    REM First check in ConverterApp directory
    if exist "ConverterApp\%CHAR_NAME%_oes.zip" (
        echo Using character file: ConverterApp\%CHAR_NAME%_oes.zip
        dotnet run --project ModelLister\ModelLister.csproj "ConverterApp\%CHAR_NAME%_oes.zip"
        set MODEL_FILE_FOUND=1
    ) 
    
    REM Then check in VisualEQ directory
    if exist "VisualEQ\%CHAR_NAME%_oes.zip" (
        if !MODEL_FILE_FOUND!==1 (
            echo.
            echo Additional character file found:
        )
        echo Using character file: VisualEQ\%CHAR_NAME%_oes.zip
        dotnet run --project ModelLister\ModelLister.csproj "VisualEQ\%CHAR_NAME%_oes.zip"
        set MODEL_FILE_FOUND=1
    )
    
    if !MODEL_FILE_FOUND!==0 (
        echo No character file found for zone: %ZONE_NAME%
        echo Expected files:
        echo - ConverterApp\%CHAR_NAME%_oes.zip
        echo - VisualEQ\%CHAR_NAME%_oes.zip
        
        echo.
        echo Try using a different zone name or convert character models first
        echo by running: load_zone.bat [eq_path] %ZONE_NAME%
    )
    
    echo.
    echo ===================================
    echo To use a model, run VisualEQ with:
    echo dotnet run -c Debug --no-build %ZONE_NAME% MODEL_NAME
    echo 
    echo Example: dotnet run -c Debug --no-build %ZONE_NAME% ORC
    echo.
    echo To view animations for a specific model:
    echo list_models.bat %ZONE_NAME% MODEL_NAME
    echo ===================================
    
    pause
    exit /b 0
)

:menu
cls
echo ===================================
echo VisualEQ Character Model Lister
echo ===================================
echo.
echo Available character files:
echo.

REM Count available character files
set CHAR_COUNT=0
set "CHAR_LIST="

REM Check for character files in ConverterApp directory
pushd ConverterApp
for %%f in (*_chr_oes.zip) do (
    set "FILENAME=%%f"
    set "CHARNAME=!FILENAME:_oes.zip=!"
    set /a CHAR_COUNT+=1
    set "CHAR_LIST=!CHAR_LIST!;!CHARNAME!"
    echo !CHAR_COUNT!. !CHARNAME!
)
popd

REM Check for character files in VisualEQ directory
pushd VisualEQ
for %%f in (*_chr_oes.zip) do (
    set "FILENAME=%%f"
    set "CHARNAME=!FILENAME:_oes.zip=!"
    REM Check if we already counted this file
    echo !CHAR_LIST! | findstr /C:";!CHARNAME!;" > nul
    if errorlevel 1 (
        set /a CHAR_COUNT+=1
        set "CHAR_LIST=!CHAR_LIST!;!CHARNAME!;"
        echo !CHAR_COUNT!. !CHARNAME!
    )
)
popd

echo.
echo !CHAR_COUNT! character file(s) found
echo.
echo L. List available zones with character files
echo C. Specify a custom character file path
echo Q. Quit
echo.

if %CHAR_COUNT% EQU 0 (
    echo No character files found in ConverterApp or VisualEQ directories.
    echo.
    choice /C LCQ /N /M "Choose an option [L,C,Q]: "
    if errorlevel 3 goto :eof
    if errorlevel 2 goto custom_file
    if errorlevel 1 goto list_zones
)

set /p CHOICE="Enter choice [1-%CHAR_COUNT%, L, C, Q]: "

if /i "%CHOICE%"=="Q" goto :eof
if /i "%CHOICE%"=="C" goto custom_file
if /i "%CHOICE%"=="L" goto list_zones

REM Check if the input is a number and within range
set "CHOICE=%CHOICE: =%"
set /a "CHOICE_NUM=%CHOICE%" 2>nul
if "%CHOICE_NUM%"=="%CHOICE%" (
    if %CHOICE_NUM% GEQ 1 (
        if %CHOICE_NUM% LEQ %CHAR_COUNT% (
            REM Extract the character file name from the list
            set N=1
            for %%z in (%CHAR_LIST:;= %) do (
                if !N!==%CHOICE_NUM% (
                    set CHAR_NAME=%%z
                    goto find_and_list
                )
                set /a N+=1
            )
        )
    )
)

echo Invalid choice.
pause
goto menu

:list_zones
cls
echo ===================================
echo Available Zones with Character Files
echo ===================================
echo.

set ZONE_COUNT=0
set "ZONE_LIST="

REM Check for character files in ConverterApp directory
pushd ConverterApp 2>nul
if not errorlevel 1 (
    for %%f in (*_chr_oes.zip) do (
        set "FILENAME=%%f"
        set "ZONENAME=!FILENAME:_chr_oes.zip=!"
        echo !ZONE_COUNT!. !ZONENAME!
        set "ZONE_LIST=!ZONE_LIST!;!ZONENAME!"
        set /a ZONE_COUNT+=1
    )
    popd
)

REM Check for zones in VisualEQ directory that weren't already listed
pushd VisualEQ 2>nul
if not errorlevel 1 (
    for %%f in (*_chr_oes.zip) do (
        set "FILENAME=%%f"
        set "ZONENAME=!FILENAME:_chr_oes.zip=!"
        echo !ZONE_LIST! | findstr /C:";!ZONENAME!;" > nul
        if errorlevel 1 (
            echo !ZONE_COUNT!. !ZONENAME!
            set "ZONE_LIST=!ZONE_LIST!;!ZONENAME!"
            set /a ZONE_COUNT+=1
        )
    )
    popd
)

echo.
echo !ZONE_COUNT! zone(s) found with character files
echo.
echo B. Back to main menu
echo Q. Quit
echo.

set /p CHOICE="Enter zone number or option [B,Q]: "

if /i "%CHOICE%"=="Q" goto :eof
if /i "%CHOICE%"=="B" goto menu

REM Check if the input is a number and within range
set "CHOICE=%CHOICE: =%"
set /a "CHOICE_NUM=%CHOICE%" 2>nul
if "%CHOICE_NUM%"=="%CHOICE%" (
    if %CHOICE_NUM% GEQ 0 (
        if %CHOICE_NUM% LSS %ZONE_COUNT% (
            REM Extract the zone name from the list
            set N=0
            for %%z in (%ZONE_LIST:;= %) do (
                if !N!==%CHOICE_NUM% (
                    set ZONE_NAME=%%z
                    set CHAR_NAME=!ZONE_NAME!_chr
                    goto list_zone_models
                )
                set /a N+=1
            )
        )
    )
)

echo Invalid choice.
pause
goto list_zones

:list_zone_models
cls
echo ===================================
echo MODELS FOR ZONE: %ZONE_NAME%
echo ===================================
echo.

set MODEL_FILE_FOUND=0

REM First check in ConverterApp directory
if exist "ConverterApp\%CHAR_NAME%_oes.zip" (
    echo Using character file: ConverterApp\%CHAR_NAME%_oes.zip
    dotnet run --project ModelLister\ModelLister.csproj "ConverterApp\%CHAR_NAME%_oes.zip"
    set MODEL_FILE_FOUND=1
) 

REM Then check in VisualEQ directory
if exist "VisualEQ\%CHAR_NAME%_oes.zip" (
    if !MODEL_FILE_FOUND!==1 (
        echo.
        echo Additional character file found:
    )
    echo Using character file: VisualEQ\%CHAR_NAME%_oes.zip
    dotnet run --project ModelLister\ModelLister.csproj "VisualEQ\%CHAR_NAME%_oes.zip"
    set MODEL_FILE_FOUND=1
)

echo.
echo ===================================
echo To use a model, run VisualEQ with:
echo dotnet run -c Debug --no-build %ZONE_NAME% MODEL_NAME
echo 
echo Example: dotnet run -c Debug --no-build %ZONE_NAME% ORC
echo.
echo To view detailed animations for a specific model:
echo list_models.bat %ZONE_NAME% MODEL_NAME
echo ===================================

pause
goto menu

:custom_file
cls
echo ===================================
echo Custom Character File
echo ===================================
echo.
echo Enter the full path to a character _chr_oes.zip file:
echo.
set /p CUSTOM_PATH="Path: "

if not exist "%CUSTOM_PATH%" (
    echo Error: File not found at %CUSTOM_PATH%
    pause
    goto menu
)

set CHAR_NAME=%CUSTOM_PATH%
goto list_models

:find_and_list
REM Find where the character file exists
if exist "ConverterApp\%CHAR_NAME%_oes.zip" (
    set CHAR_PATH=ConverterApp\%CHAR_NAME%_oes.zip
) else if exist "VisualEQ\%CHAR_NAME%_oes.zip" (
    set CHAR_PATH=VisualEQ\%CHAR_NAME%_oes.zip
) else (
    echo Error: Character file %CHAR_NAME%_oes.zip not found!
    pause
    goto menu
)

goto list_models

:list_models
cls
echo ===================================
echo Models in %CHAR_NAME%
echo ===================================
echo.

REM Run the ModelLister to display models
if "%CHAR_PATH%"=="" (
    echo Running ModelLister on %CHAR_NAME%
    dotnet run --project ModelLister\ModelLister.csproj "%CHAR_NAME%"
) else (
    echo Running ModelLister on %CHAR_PATH%
    dotnet run --project ModelLister\ModelLister.csproj "%CHAR_PATH%"
)

echo.
echo Options:
echo 1. Launch a zone with one of these models
echo 2. View detailed animations for a specific model
echo 3. Back to main menu
echo.
choice /C 123 /N /M "Choose an option [1,2,3]: "
if errorlevel 3 goto menu
if errorlevel 2 goto view_model_animations
if errorlevel 1 goto launch_zone_prompt

:view_model_animations
echo.
set /p MODEL_NAME="Enter model name to view animations: "
cls
echo ===================================
echo Animations for model: %MODEL_NAME%
echo ===================================
echo.

REM Run the ModelLister with animation filter
if "%CHAR_PATH%"=="" (
    dotnet run --project ModelLister\ModelLister.csproj "%CHAR_NAME%" --anims-only %MODEL_NAME%
) else (
    dotnet run --project ModelLister\ModelLister.csproj "%CHAR_PATH%" --anims-only %MODEL_NAME%
)

echo.
pause
goto list_models

:launch_zone_prompt
echo.
set /p MODEL_NAME="Enter model name to use: "

REM List available zones to launch with this model
cls
echo ===================================
echo Available Zones
echo ===================================
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
echo B. Back to model selection
echo Q. Quit
echo.

if %ZONE_COUNT% EQU 0 (
    echo No zones found.
    pause
    goto menu
)

set /p CHOICE="Enter choice [1-%ZONE_COUNT%, B, Q]: "

if /i "%CHOICE%"=="Q" goto :eof
if /i "%CHOICE%"=="B" goto menu

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

:launch_zone
echo === Launching VisualEQ with Zone: %ZONE_NAME% and Model: %MODEL_NAME% ===
pushd VisualEQ
dotnet run -c Debug --no-build %ZONE_NAME% %MODEL_NAME%
popd

echo Done.
pause
goto menu

:eof 