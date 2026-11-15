@echo off
rem Rebuilds styles/fx.daisy.css of the vendor library (the daisyUI components with the prefix fx-) with the standalone Tailwind CLI that the build of the repository downloads to .tailwind.
rem Needs no Node.js. A new class is added to classes.txt (the CLI generates only the classes it finds in the sources); daisyui.mjs is the pinned release 5.7.50.
setlocal
set CLI=%~dp0..\..\..\..\.tailwind\tailwindcss-windows-x64-v4_3_2.exe
"%CLI%" -i "%~dp0input.css" -o "%~dp0..\..\..\..\library\styles\fx.daisy.css" --minify
endlocal
