@echo off
REM ---------------------------------------------------------------
REM Nona Royale - folder scaffold
REM
REM Run from the ROOT of the Unity project (the folder that already
REM contains Assets\, Packages\ and ProjectSettings\), AFTER Unity
REM has created the project from the Universal 2D template.
REM
REM Safe to re-run: mkdir on an existing folder is a no-op here.
REM ---------------------------------------------------------------

echo Creating docs and tools...
mkdir docs\decisions          2>nul
mkdir docs\design             2>nul
mkdir docs\art                2>nul
mkdir tools\sim               2>nul

echo Creating Assets\_Project...
mkdir Assets\_Project\Scenes            2>nul
mkdir Assets\_Project\Settings          2>nul
mkdir Assets\_Project\Art\Board         2>nul
mkdir Assets\_Project\Art\Operators     2>nul
mkdir Assets\_Project\Art\UI            2>nul
mkdir Assets\_Project\Art\FX            2>nul
mkdir Assets\_Project\Art\Materials     2>nul
mkdir Assets\_Project\Audio\Music       2>nul
mkdir Assets\_Project\Audio\SFX         2>nul
mkdir Assets\_Project\Prefabs\Board     2>nul
mkdir Assets\_Project\Prefabs\Operators 2>nul
mkdir Assets\_Project\Prefabs\UI        2>nul
mkdir Assets\_Project\Data\Operators    2>nul
mkdir Assets\_Project\Data\Boards       2>nul

echo Creating core assembly...
mkdir Assets\_Project\Scripts\Core\Board      2>nul
mkdir Assets\_Project\Scripts\Core\Config     2>nul
mkdir Assets\_Project\Scripts\Core\Model      2>nul
mkdir Assets\_Project\Scripts\Core\Abilities  2>nul
mkdir Assets\_Project\Scripts\Core\Commands   2>nul
mkdir Assets\_Project\Scripts\Core\Events     2>nul
mkdir Assets\_Project\Scripts\Core\Services   2>nul
mkdir Assets\_Project\Scripts\Core\Rng        2>nul

echo Creating unity assembly...
mkdir Assets\_Project\Scripts\Unity\Composition 2>nul
mkdir Assets\_Project\Scripts\Unity\Data        2>nul
mkdir Assets\_Project\Scripts\Unity\View        2>nul
mkdir Assets\_Project\Scripts\Unity\Input       2>nul
mkdir Assets\_Project\Scripts\Unity\UI          2>nul

echo Creating tests...
mkdir Assets\Tests\EditMode\Board      2>nul
mkdir Assets\Tests\EditMode\Energy     2>nul
mkdir Assets\Tests\EditMode\Movement   2>nul
mkdir Assets\Tests\EditMode\Collision  2>nul
mkdir Assets\Tests\EditMode\Damage     2>nul
mkdir Assets\Tests\EditMode\Targeting  2>nul
mkdir Assets\Tests\EditMode\Status     2>nul
mkdir Assets\Tests\EditMode\Abilities  2>nul
mkdir Assets\Tests\EditMode\Win        2>nul
mkdir Assets\Tests\PlayMode            2>nul

echo.
echo Done. Next:
echo   1. Copy the four .asmdef files into:
echo        Assets\_Project\Scripts\Core\
echo        Assets\_Project\Scripts\Unity\
echo        Assets\Tests\EditMode\
echo        Assets\Tests\PlayMode\
echo   2. Copy .gitignore and .gitattributes to this folder.
echo   3. Copy the docs and tools\sim files into place.
echo   4. Switch to Unity and let it import.
echo.
