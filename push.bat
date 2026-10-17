@echo off
setlocal
rem Commits every change (files in .gitignore stay out) and pushes the current branch.
rem Usage: push.bat "commit message"    - or run it without a message and type one when asked.
cd /d "%~dp0"

set "dirty="
for /f "delims=" %%i in ('git status --porcelain') do set "dirty=1"
if not defined dirty (
    echo Nothing to commit.
    goto :push
)

git status --short
set "msg=%~1"
if not defined msg set /p "msg=Commit message: "
if not defined msg (
    echo No message given - nothing committed.
    exit /b 1
)

git add -A || exit /b 1
git commit -q -m "%msg%" || exit /b 1

:push
git push -q || (
    echo Push failed.
    exit /b 1
)
echo Pushed:
git log --oneline -1
