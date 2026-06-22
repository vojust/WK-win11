# PCScheduler

<img src="https://img.shields.io/badge/version-v26.622.1629-blue?style=flat-square" alt="version"/>
<img src="https://img.shields.io/github/actions/workflow/status/vojust/WK-win11/.github%2Fworkflows%2Fbuild.yml?branch=main&style=flat-square&label=build" alt="build"/>
<img src="https://img.shields.io/badge/updated-22.06.2026 16:30-lightgrey?style=flat-square" alt="updated"/>

Планировщик включения/выключения ПК по расписанию. C# WPF (.NET 8), интеграция с Task Scheduler. Версия в названии файла: PCScheduler-vYY.Mdd.HHmm.exe.

## Скачать

[📥 PCScheduler-v26.622.1629.exe](https://github.com/vojust/WK-win11/releases/download/v26.622.1629/PCScheduler-v26.622.1629.exe) (single-file, ~50 MB)

## Возможности

- **Сон / Гибернация / Пробуждение** по расписанию
- **Предупреждение** за 5 минут до выключения (всплывающее окно)
- **Повтор**: ежедневно, по будням, еженедельно, один раз
- **Тест пробуждения** (создаёт задачу на +2 мин)
- **Таймер до события** в реальном времени
- **Трей**: сворачивается в системный лоток
- **Лог**: история операций

## Как это работает

- **Сон/гибернация** — через schtasks.exe + rundll32.exe powrprof.dll,SetSuspendState
- **Пробуждение** — через PowerShell Register-ScheduledTask -WakeToRun
- **Конфиг**: %LOCALAPPDATA%\PCScheduler\schedules.json
- **Кэш NuGet** в CI для быстрой сборки

## Последний релиз

| Версия | Дата |
|---|---|
| v26.622.1629 | 22.06.2026 16:30 |
