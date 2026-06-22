param($Tag)

$date = (Get-Date).ToString('dd.MM.yyyy HH:mm')
$download = "https://github.com/vojust/WK-win11/releases/download/$Tag/PCScheduler-$Tag.exe"

@"
# PCScheduler

<img src="https://img.shields.io/badge/version-$Tag-blue?style=flat-square" alt="version"/>
<img src="https://img.shields.io/github/actions/workflow/status/vojust/WK-win11/.github%2Fworkflows%2Fbuild.yml?branch=main&style=flat-square&label=build" alt="build"/>
<img src="https://img.shields.io/badge/updated-$date-lightgrey?style=flat-square" alt="updated"/>

Планировщик включения/выключения ПК по расписанию. C# WPF (.NET 8), интеграция с Task Scheduler. Версия в названии файла: `PCScheduler-vYY.Mdd.HHmm.exe`.

## Скачать

[📥 PCScheduler-$Tag.exe]($download) (single-file, ~50 MB)

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
| $Tag | $date |
"@
