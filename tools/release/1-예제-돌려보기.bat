@echo off
chcp 65001 >nul
cd /d "%~dp0"
if exist try rmdir /s /q try
xcopy /e /i /q examples\table-compare try\table-compare >nul
echo [1/4] 분류
scorecard.exe start try\table-compare --answers try\table-compare\answers.yaml
echo.
echo [2/4] 대조와 판정
scorecard.exe check try\table-compare
echo.
echo [3/4] 스코어카드
scorecard.exe open try\table-compare
echo.
echo [4/4] 만들 단계 추천
scorecard.exe build try\table-compare
echo.
start "" "try\table-compare\out\scorecard.html"
pause
