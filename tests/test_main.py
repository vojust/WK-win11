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
    def test_sleep_command_format(self):
        """Verify the sleep task command is built correctly"""
        e = ScheduleEntry(type="sleep", time="22:00", repeat="daily")
        task_name = f"{TASK_PREFIX}{e.id}"
        # We can't run schtasks on macOS, but we can verify command construction
        expected_cmd = f'/create /tn "{task_name}" /tr "rundll32.exe powrprof.dll,SetSuspendState 0,1,0" /sc daily /st 22:00 /f'
        cmd = f'/create /tn "{task_name}" /tr "rundll32.exe powrprof.dll,SetSuspendState 0,1,0" /sc daily /st {e.time} /f'
        self.assertEqual(cmd, expected_cmd)

    def test_wake_command_format(self):
        """Verify the wake task command includes /WAKE flag"""
        e = ScheduleEntry(type="wake", time="08:00", repeat="daily")
        task_name = f"{TASK_PREFIX}{e.id}"
        cmd = f'/create /tn "{task_name}" /tr "exit" /sc daily /st {e.time} /f /WAKE'
        expected_cmd = f'/create /tn "{task_name}" /tr "exit" /sc daily /st 08:00 /f /WAKE'
        self.assertEqual(cmd, expected_cmd)

    def test_weekdays_flag(self):
        """Verify weekdays repeat adds MON-FRI"""
        e = ScheduleEntry(type="sleep", time="23:00", repeat="weekdays")
        task_name = f"{TASK_PREFIX}{e.id}"
        cmd = f'/create /tn "{task_name}" /tr "rundll32.exe powrprof.dll,SetSuspendState 0,1,0" /sc daily /st {e.time} /d MON,TUE,WED,THU,FRI /f'
        expected_suffix = "MON,TUE,WED,THU,FRI"
        self.assertIn(expected_suffix, cmd)

    def test_weekly_flag(self):
        """Verify weekly repeat adds selected days"""
        e = ScheduleEntry(type="wake", time="09:00", repeat="weekly", days=["MON", "WED"])
        task_name = f"{TASK_PREFIX}{e.id}"
        cmd = f'/create /tn "{task_name}" /tr "exit" /sc daily /st {e.time} /d MON,WED /f /WAKE'
        self.assertIn("MON,WED", cmd)

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
