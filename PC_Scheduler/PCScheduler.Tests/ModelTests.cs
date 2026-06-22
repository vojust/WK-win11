using Xunit;
using PCScheduler.Core;

namespace PCScheduler.Tests;

public class ModelTests
{
    [Fact]
    public void DefaultEntry_HasId_NotNull()
    {
        var e = new ScheduleEntry();
        Assert.NotNull(e.Id);
        Assert.Equal(8, e.Id.Length);
    }

    [Fact]
    public void SleepEntry_TypeDisplay_ReturnsRussian()
    {
        var e = new ScheduleEntry { Type = ScheduleType.Sleep };
        Assert.Equal("Сон", e.TypeDisplay);
    }

    [Fact]
    public void WakeEntry_TypeDisplay_ReturnsRussian()
    {
        var e = new ScheduleEntry { Type = ScheduleType.Wake };
        Assert.Equal("Пробуждение", e.TypeDisplay);
    }

    [Fact]
    public void HibernateEntry_TypeDisplay_ReturnsRussian()
    {
        var e = new ScheduleEntry { Type = ScheduleType.Hibernate };
        Assert.Equal("Гибернация", e.TypeDisplay);
    }

    [Fact]
    public void DailyRepeat_RepeatDisplay_ReturnsDaily()
    {
        var e = new ScheduleEntry { Repeat = RepeatType.Daily };
        Assert.Equal("Ежедневно", e.RepeatDisplay);
    }

    [Fact]
    public void WeekdaysRepeat_RepeatDisplay_ReturnsWeekdays()
    {
        var e = new ScheduleEntry { Repeat = RepeatType.Weekdays };
        Assert.Equal("По будням", e.RepeatDisplay);
    }

    [Fact]
    public void OnceRepeat_RepeatDisplay_ReturnsOnce()
    {
        var e = new ScheduleEntry { Repeat = RepeatType.Once };
        Assert.Equal("Один раз", e.RepeatDisplay);
    }

    [Fact]
    public void WeeklyRepeat_WithDays_ShowsDays()
    {
        var e = new ScheduleEntry
        {
            Repeat = RepeatType.Weekly,
            Days = new List<string> { "MON", "WED", "FRI" }
        };
        Assert.Contains("Пн", e.RepeatDisplay);
        Assert.Contains("Ср", e.RepeatDisplay);
        Assert.Contains("Пт", e.RepeatDisplay);
    }

    [Fact]
    public void Enabled_StatusDisplay_Check()
    {
        Assert.Equal("✓", new ScheduleEntry { Enabled = true }.StatusDisplay);
        Assert.Equal("✗", new ScheduleEntry { Enabled = false }.StatusDisplay);
    }

    [Fact]
    public void TimeFormatted_AddsLeadingZeros()
    {
        var e = new ScheduleEntry { Time = "8:5" };
        Assert.Equal("08:05", e.TimeFormatted);
    }

    [Fact]
    public void TimeFormatted_KeepsLeadingZeros()
    {
        var e = new ScheduleEntry { Time = "08:05" };
        Assert.Equal("08:05", e.TimeFormatted);
    }

    [Fact]
    public void TimeFormatted_PreservesNormalTime()
    {
        var e = new ScheduleEntry { Time = "23:59" };
        Assert.Equal("23:59", e.TimeFormatted);
    }

    [Fact]
    public void JsonSerialization_RoundTrip()
    {
        var entry = new ScheduleEntry
        {
            Time = "14:30",
            Type = ScheduleType.Wake,
            Repeat = RepeatType.Weekly,
            Days = new List<string> { "MON", "TUE" },
            Enabled = false,
        };
        var json = System.Text.Json.JsonSerializer.Serialize(entry);
        var back = System.Text.Json.JsonSerializer.Deserialize<ScheduleEntry>(json);
        Assert.NotNull(back);
        Assert.Equal(entry.Id, back!.Id);
        Assert.Equal(entry.Time, back.Time);
        Assert.Equal(entry.Type, back.Type);
        Assert.Equal(entry.Repeat, back.Repeat);
        Assert.Equal(entry.Enabled, back.Enabled);
        Assert.Equal(entry.Days, back.Days);
    }

    [Fact]
    public void OldConfig_Type1_DeserializesAsWake()
    {
        var json = """{"Type":1,"Time":"14:30","Repeat":1,"Enabled":true,"Days":[]}""";
        var entry = System.Text.Json.JsonSerializer.Deserialize<ScheduleEntry>(json);
        Assert.NotNull(entry);
        Assert.Equal(ScheduleType.Wake, entry!.Type);
    }

    [Fact]
    public void OldConfig_Type0_DeserializesAsSleep()
    {
        var json = """{"Type":0,"Time":"14:30","Repeat":0,"Enabled":true,"Days":[]}""";
        var entry = System.Text.Json.JsonSerializer.Deserialize<ScheduleEntry>(json);
        Assert.NotNull(entry);
        Assert.Equal(ScheduleType.Sleep, entry!.Type);
    }

    [Fact]
    public void NewConfig_WritesAsString()
    {
        var entry = new ScheduleEntry { Type = ScheduleType.Hibernate };
        var json = System.Text.Json.JsonSerializer.Serialize(entry);
        Assert.Contains("Hibernate", json);
    }

    [Fact]
    public void Days_CanBeEmpty()
    {
        var e = new ScheduleEntry { Repeat = RepeatType.Daily };
        Assert.Empty(e.Days);
    }
}
