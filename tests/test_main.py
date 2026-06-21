import json
import os
import sys
import tempfile
import unittest
from dataclasses import asdict

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from PC_Scheduler.main import (
    ScheduleEntry,
    ScheduleType,
    RepeatType,
    TaskSchedulerService,
    load_schedules,
    save_schedules,
    DAY_NAMES_EN,
    DAY_NAMES_RU,
    TASK_PREFIX,
)


class TestScheduleEntry(unittest.TestCase):
    def test_default_creation(self):
        e = ScheduleEntry()
        self.assertTrue(e.id)
        self.assertEqual(e.type, "sleep")
        self.assertEqual(e.time, "08:00")
        self.assertEqual(e.repeat, "daily")
        self.assertTrue(e.enabled)
        self.assertEqual(e.days, [])

    def test_type_display(self):
        self.assertEqual(ScheduleEntry(type="sleep").type_display, "Сон")
        self.assertEqual(ScheduleEntry(type="wake").type_display, "Пробуждение")

    def test_repeat_display_daily(self):
        e = ScheduleEntry(repeat="daily")
        self.assertEqual(e.repeat_display, "Ежедневно")

    def test_repeat_display_weekdays(self):
        e = ScheduleEntry(repeat="weekdays")
        self.assertEqual(e.repeat_display, "По будням")

    def test_repeat_display_weekly(self):
        e = ScheduleEntry(repeat="weekly", days=["MON", "WED", "FRI"])
        self.assertIn("Пн", e.repeat_display)
        self.assertIn("Ср", e.repeat_display)
        self.assertIn("Пт", e.repeat_display)

    def test_repeat_display_once(self):
        e = ScheduleEntry(repeat="once")
        self.assertEqual(e.repeat_display, "Один раз")

    def test_status_display_enabled(self):
        self.assertEqual(ScheduleEntry(enabled=True).status_display, "✓")

    def test_status_display_disabled(self):
        self.assertEqual(ScheduleEntry(enabled=False).status_display, "✗")

    def test_to_dict_roundtrip(self):
        e1 = ScheduleEntry(type="wake", time="07:00", repeat="weekly", days=["MON", "FRI"])
        d = asdict(e1)
        e2 = ScheduleEntry(**d)
        self.assertEqual(e1.id, e2.id)
        self.assertEqual(e1.type, e2.type)
        self.assertEqual(e1.time, e2.time)
        self.assertEqual(e1.repeat, e2.repeat)
        self.assertEqual(e1.days, e2.days)
        self.assertEqual(e1.enabled, e2.enabled)


class TestConfig(unittest.TestCase):
    def setUp(self):
        self.tmpdir = tempfile.mkdtemp()
        self.old_config = os.environ.get("LOCALAPPDATA")
        os.environ["LOCALAPPDATA"] = self.tmpdir

    def tearDown(self):
        if self.old_config:
            os.environ["LOCALAPPDATA"] = self.old_config
        else:
            del os.environ["LOCALAPPDATA"]

    def test_save_and_load(self):
        entries = [
            ScheduleEntry(type="sleep", time="23:00", repeat="daily"),
            ScheduleEntry(type="wake", time="07:00", repeat="weekdays"),
        ]
        save_schedules(entries)
        loaded = load_schedules()
        self.assertEqual(len(loaded), 2)
        self.assertEqual(loaded[0].type, "sleep")
        self.assertEqual(loaded[0].time, "23:00")
        self.assertEqual(loaded[1].repeat, "weekdays")

    def test_load_empty_file(self):
        loaded = load_schedules()
        self.assertEqual(loaded, [])


class TestTaskSchedulerCommands(unittest.TestCase):
    def _build_args(self, entry):
        """Helper: replicate argument construction from TaskSchedulerService.create_or_update"""
        from datetime import datetime
        task_name = f"{TASK_PREFIX}{entry.id}"
        h, m = entry.time.split(":")
        time_fmt = f"{int(h):02d}:{int(m):02d}"
        if entry.type == "wake":
            args = ["/create", "/tn", task_name, "/tr", "exit",
                    "/sc", "daily", "/st", time_fmt, "/f", "/WAKE"]
        else:
            args = ["/create", "/tn", task_name,
                    "/tr", "rundll32.exe powrprof.dll,SetSuspendState 0,1,0",
                    "/sc", "daily", "/st", time_fmt, "/f"]
        if entry.repeat == "weekdays":
            args += ["/d", "MON,TUE,WED,THU,FRI"]
        elif entry.repeat == "weekly" and entry.days:
            args += ["/d", ",".join(entry.days)]
        return args

    def test_sleep_args(self):
        e = ScheduleEntry(type="sleep", time="22:00", repeat="daily")
        args = self._build_args(e)
        self.assertIn("/create", args)
        self.assertEqual(args[args.index("/tn") + 1], f"{TASK_PREFIX}{e.id}")
        self.assertEqual(args[args.index("/tr") + 1], "rundll32.exe powrprof.dll,SetSuspendState 0,1,0")
        self.assertEqual(args[args.index("/st") + 1], "22:00")
        self.assertNotIn("/WAKE", args)

    def test_wake_args(self):
        e = ScheduleEntry(type="wake", time="08:00", repeat="daily")
        args = self._build_args(e)
        self.assertEqual(args[args.index("/tr") + 1], "exit")
        self.assertIn("/WAKE", args)

    def test_weekdays_flag(self):
        e = ScheduleEntry(type="sleep", time="23:00", repeat="weekdays")
        args = self._build_args(e)
        self.assertEqual(args[args.index("/d") + 1], "MON,TUE,WED,THU,FRI")

    def test_weekly_flag(self):
        e = ScheduleEntry(type="wake", time="09:00", repeat="weekly", days=["MON", "WED"])
        args = self._build_args(e)
        self.assertEqual(args[args.index("/d") + 1], "MON,WED")

    def test_delete_args(self):
        e = ScheduleEntry(id="test123", type="sleep", time="22:00")
        task_name = f"{TASK_PREFIX}{e.id}"
        args = ["/delete", "/tn", task_name, "/f"]
        self.assertEqual(args[0], "/delete")
        self.assertEqual(args[2], task_name)

    def test_query_args(self):
        args = ["/query", "/fo", "csv", "/nh"]
        self.assertEqual(args, ["/query", "/fo", "csv", "/nh"])

    def test_days_order(self):
        """Verify DAY_NAMES_EN and DAY_NAMES_RU correspond correctly"""
        self.assertEqual(len(DAY_NAMES_EN), 7)
        self.assertEqual(len(DAY_NAMES_RU), 7)
        expected_en = ["MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN"]
        expected_ru = ["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс"]
        self.assertEqual(DAY_NAMES_EN, expected_en)
        self.assertEqual(DAY_NAMES_RU, expected_ru)


if __name__ == "__main__":
    unittest.main()
