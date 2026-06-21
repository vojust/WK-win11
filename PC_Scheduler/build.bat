@echo off
chcp 65001 >nul
echo Сборка PC Scheduler...

pip install -r requirements.txt
pyinstaller --onefile --windowed --name "PC_Scheduler" main.py

echo.
echo Готово! exe в папке dist\
pause
