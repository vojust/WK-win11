import tkinter as tk
from tkinter import ttk, messagebox, simpledialog
import json
import os
import subprocess
from dataclasses import dataclass, field, asdict
from datetime import datetime, time
from typing import List, Optional
from enum import Enum

# ── Models ────────────────────────────────────────────────────────────

class ScheduleType(Enum):
    SLEEP = "sleep"
    WAKE = "wake"

class RepeatType(Enum):
    DAILY = "daily"
    WEEKDAYS = "weekdays"
    WEEKLY = "weekly"
    ONCE = "once"

DAY_NAMES_RU = ["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс"]
DAY_NAMES_EN = ["MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN"]

@dataclass
class ScheduleEntry:
    id: str = ""
    type: str = "sleep"
    time: str = "08:00"
    repeat: str = "daily"
    enabled: bool = True
    days: List[str] = field(default_factory=list)

    def __post_init__(self):
        if not self.id:
            self.id = datetime.now().strftime("%H%M%S%f")

    @property
    def type_display(self):
        return "Сон" if self.type == "sleep" else "Пробуждение"

    @property
    def repeat_display(self):
        return {
            "daily": "Ежедневно",
            "weekdays": "По будням",
            "weekly": "Еженедельно (" + ", ".join(DAY_NAMES_RU[i] for i, d in enumerate(DAY_NAMES_EN) if d in self.days) + ")",
            "once": "Один раз",
        }.get(self.repeat, self.repeat)

    @property
    def status_display(self):
        return "✓" if self.enabled else "✗"

# ── Task Scheduler Service ────────────────────────────────────────────

TASK_PREFIX = "PCSched_"

class TaskSchedulerService:
    @staticmethod
    def _run(args: List[str]) -> str:
        proc = subprocess.run(
            ["schtasks.exe"] + args,
            capture_output=True, text=True, creationflags=subprocess.CREATE_NO_WINDOW
        )
        if proc.returncode != 0:
            err = (proc.stderr or "").strip()
            raise Exception(err or f"schtasks.exe завершился с кодом {proc.returncode}")
        return proc.stdout or ""

    @staticmethod
    def create_or_update(entry: ScheduleEntry):
        TaskSchedulerService.delete(entry)
        task_name = f"{TASK_PREFIX}{entry.id}"

        args = ["/create", "/tn", task_name, "/sc", "daily", "/st", entry.time, "/f"]
        if entry.type == "wake":
            args += ["/tr", "exit", "/WAKE"]
        else:
            args += ["/tr", "rundll32.exe powrprof.dll,SetSuspendState 0,1,0"]

        if entry.repeat == "weekdays":
            args += ["/d", "MON,TUE,WED,THU,FRI"]
        elif entry.repeat == "weekly" and entry.days:
            args += ["/d", ",".join(entry.days)]

        TaskSchedulerService._run(args)

    @staticmethod
    def delete(entry: ScheduleEntry):
        task_name = f"{TASK_PREFIX}{entry.id}"
        TaskSchedulerService._run(["/delete", "/tn", task_name, "/f"])

    @staticmethod
    def apply_all(entries: List[ScheduleEntry]):
        for e in entries:
            if e.enabled:
                TaskSchedulerService.create_or_update(e)
            else:
                TaskSchedulerService.delete(e)
        TaskSchedulerService.cleanup(entries)

    @staticmethod
    def cleanup(entries: List[ScheduleEntry]):
        active_ids = {f"{TASK_PREFIX}{e.id}" for e in entries}
        try:
            out = TaskSchedulerService._run(["/query", "/fo", "csv", "/nh"])
        except Exception:
            return
        for line in out.strip().split("\n"):
            line = line.strip()
            if not line:
                continue
            parts = line.split('","')
            if len(parts) < 2:
                continue
            tn = parts[0].strip('"').strip()
            if tn.startswith(TASK_PREFIX) and tn not in active_ids:
                try:
                    TaskSchedulerService._run(["/delete", "/tn", tn, "/f"])
                except Exception:
                    pass

# ── Config ────────────────────────────────────────────────────────────

def _config_path() -> str:
    base = os.environ.get("LOCALAPPDATA", os.path.expanduser("~"))
    return os.path.join(base, "PCScheduler", "schedules.json")

def load_schedules() -> List[ScheduleEntry]:
    try:
        path = _config_path()
        if os.path.exists(path):
            with open(path, "r", encoding="utf-8") as f:
                data = json.load(f)
            return [ScheduleEntry(**d) for d in data]
    except Exception as e:
        messagebox.showerror("Ошибка", f"Не удалось загрузить конфиг:\n{e}")
    return []

def save_schedules(entries: List[ScheduleEntry]):
    path = _config_path()
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump([asdict(e) for e in entries], f, ensure_ascii=False, indent=2)

# ── GUI ───────────────────────────────────────────────────────────────

class EditDialog(tk.Toplevel):
    def __init__(self, parent, entry: Optional[ScheduleEntry] = None):
        super().__init__(parent)
        self.entry = entry
        self.result: Optional[ScheduleEntry] = None
        self.title("Расписание" if not entry else "Изменить расписание")
        self.geometry("380x340")
        self.resizable(False, False)
        self.transient(parent)
        self.grab_set()

        pad = {"padx": 10, "pady": 4}

        row = 0
        tk.Label(self, text="Тип:").grid(row=row, column=0, sticky="w", **pad)
        self.type_var = tk.StringVar(value="sleep")
        self.type_cb = ttk.Combobox(self, textvariable=self.type_var, values=["sleep", "wake"], state="readonly", width=15)
        self.type_cb.grid(row=row, column=1, sticky="ew", **pad)
        self.type_cb.bind("<<ComboboxSelected>>", self._on_type_change)

        row += 1
        tk.Label(self, text="Время (ЧЧ:ММ):").grid(row=row, column=0, sticky="w", **pad)
        self.time_entry = ttk.Entry(self, width=18)
        self.time_entry.grid(row=row, column=1, sticky="ew", **pad)
        self.time_entry.insert(0, "08:00")

        row += 1
        tk.Label(self, text="Повтор:").grid(row=row, column=0, sticky="w", **pad)
        self.repeat_var = tk.StringVar(value="daily")
        self.repeat_cb = ttk.Combobox(self, textvariable=self.repeat_var,
                                       values=["daily", "weekdays", "weekly", "once"],
                                       state="readonly", width=15)
        self.repeat_cb.grid(row=row, column=1, sticky="ew", **pad)
        self.repeat_cb.bind("<<ComboboxSelected>>", self._on_repeat_change)

        row += 1
        tk.Label(self, text="Дни недели:").grid(row=row, column=0, sticky="nw", **pad)
        self.day_vars = {}
        days_frame = ttk.Frame(self)
        days_frame.grid(row=row, column=1, sticky="w", **pad)
        for i, name in enumerate(DAY_NAMES_RU):
            var = tk.BooleanVar()
            cb = ttk.Checkbutton(days_frame, text=name, variable=var)
            cb.grid(row=0, column=i, padx=1)
            self.day_vars[DAY_NAMES_EN[i]] = var

        row += 1
        self.enabled_var = tk.BooleanVar(value=True)
        ttk.Checkbutton(self, text="Включено", variable=self.enabled_var).grid(row=row, column=0, columnspan=2, sticky="w", **pad)

        row += 1
        btn_frame = ttk.Frame(self)
        btn_frame.grid(row=row, column=0, columnspan=2, pady=16)
        ttk.Button(btn_frame, text="Сохранить", command=self._save).pack(side="left", padx=4)
        ttk.Button(btn_frame, text="Отмена", command=self.destroy).pack(side="left", padx=4)

        self.columnconfigure(1, weight=1)
        self._on_repeat_change()
        if entry:
            self._load_entry(entry)

    def _on_type_change(self, event=None):
        pass

    def _on_repeat_change(self, event=None):
        is_weekly = self.repeat_var.get() == "weekly"
        for var in self.day_vars.values():
            pass  # disabled state managed via state
        for cb in self.day_vars.values():
            pass  # we don't store cb widgets directly; use frame children
        for child in self.winfo_children():
            if isinstance(child, ttk.Frame):
                for c in child.winfo_children():
                    if isinstance(c, ttk.Checkbutton):
                        c.state(["!disabled" if is_weekly else "disabled"])

    def _load_entry(self, entry: ScheduleEntry):
        self.type_var.set(entry.type)
        self.time_entry.delete(0, "end")
        self.time_entry.insert(0, entry.time)
        self.repeat_var.set(entry.repeat)
        self.enabled_var.set(entry.enabled)
        for d in DAY_NAMES_EN:
            self.day_vars[d].set(d in entry.days)
        self._on_repeat_change()

    def _save(self):
        time_str = self.time_entry.get().strip()
        try:
            h, m = time_str.split(":")
            int(h); int(m)
        except:
            messagebox.showwarning("Ошибка", "Введите корректное время (ЧЧ:ММ)", parent=self)
            return

        days = []
        if self.repeat_var.get() == "weekly":
            days = [d for d in DAY_NAMES_EN if self.day_vars[d].get()]
            if not days:
                messagebox.showwarning("Ошибка", "Выберите хотя бы один день недели", parent=self)
                return

        self.result = ScheduleEntry(
            id=self.entry.id if self.entry else "",
            type=self.type_var.get(),
            time=time_str,
            repeat=self.repeat_var.get(),
            enabled=self.enabled_var.get(),
            days=days,
        )
        self.destroy()


class MainWindow:
    def __init__(self):
        self.root = tk.Tk()
        self.root.title("Планировщик включения/выключения ПК")
        self.root.geometry("720x420")
        self.root.minsize(600, 300)

        self.entries: List[ScheduleEntry] = load_schedules()
        self.scheduler = TaskSchedulerService()

        self._build_ui()
        self._refresh_table()

    def _build_ui(self):
        top = ttk.Frame(self.root)
        top.pack(fill="x", padx=8, pady=8)

        ttk.Label(top, text="Управление расписанием", font=("", 16, "bold")).pack(anchor="w")

        cols = ("type", "time", "repeat", "status")
        self.tree = ttk.Treeview(self.root, columns=cols, show="headings", selectmode="browse")
        self.tree.heading("type", text="Тип")
        self.tree.heading("time", text="Время")
        self.tree.heading("repeat", text="Повтор")
        self.tree.heading("status", text="Статус")
        self.tree.column("type", width=120)
        self.tree.column("time", width=80)
        self.tree.column("repeat", width=300)
        self.tree.column("status", width=60)
        self.tree.pack(fill="both", expand=True, padx=8, pady=4)
        self.tree.bind("<<TreeviewSelect>>", self._on_select)

        btn_frame = ttk.Frame(self.root)
        btn_frame.pack(fill="x", padx=8, pady=8)

        self.btn_add = ttk.Button(btn_frame, text="+ Добавить", command=self._add)
        self.btn_add.pack(side="left", padx=2)

        self.btn_edit = ttk.Button(btn_frame, text="✏ Изменить", command=self._edit, state="disabled")
        self.btn_edit.pack(side="left", padx=2)

        self.btn_toggle = ttk.Button(btn_frame, text="Вкл/Выкл", command=self._toggle, state="disabled")
        self.btn_toggle.pack(side="left", padx=2)

        self.btn_delete = ttk.Button(btn_frame, text="✖ Удалить", command=self._delete, state="disabled")
        self.btn_delete.pack(side="left", padx=2)

        ttk.Button(btn_frame, text="Применить", command=self._apply).pack(side="right", padx=2)

    def _get_selected(self) -> Optional[ScheduleEntry]:
        sel = self.tree.selection()
        if not sel:
            return None
        idx = self.tree.index(sel[0])
        if 0 <= idx < len(self.entries):
            return self.entries[idx]
        return None

    def _on_select(self, event=None):
        has = self._get_selected() is not None
        self.btn_edit.state(["!disabled" if has else "disabled"])
        self.btn_toggle.state(["!disabled" if has else "disabled"])
        self.btn_delete.state(["!disabled" if has else "disabled"])

    def _refresh_table(self):
        for row in self.tree.get_children():
            self.tree.delete(row)
        for e in self.entries:
            self.tree.insert("", "end", values=(e.type_display, e.time, e.repeat_display, e.status_display))

    def _add(self):
        dlg = EditDialog(self.root)
        self.root.wait_window(dlg)
        if dlg.result:
            self.entries.append(dlg.result)
            save_schedules(self.entries)
            self._refresh_table()

    def _edit(self):
        entry = self._get_selected()
        if not entry:
            return
        idx = self.entries.index(entry)
        dlg = EditDialog(self.root, entry)
        self.root.wait_window(dlg)
        if dlg.result:
            dlg.result.id = entry.id
            self.entries[idx] = dlg.result
            save_schedules(self.entries)
            self._refresh_table()

    def _toggle(self):
        entry = self._get_selected()
        if not entry:
            return
        entry.enabled = not entry.enabled
        save_schedules(self.entries)
        self._refresh_table()

    def _delete(self):
        entry = self._get_selected()
        if not entry:
            return
        if messagebox.askyesno("Подтверждение", "Удалить выбранное расписание?"):
            self.entries.remove(entry)
            save_schedules(self.entries)
            self._refresh_table()

    def _apply(self):
        try:
            self.scheduler.apply_all(self.entries)
            messagebox.showinfo("Готово", "Расписание успешно применено!")
        except Exception as e:
            messagebox.showerror("Ошибка", f"Не удалось применить расписание:\n{e}")

    def run(self):
        self.root.mainloop()


if __name__ == "__main__":
    MainWindow().run()
