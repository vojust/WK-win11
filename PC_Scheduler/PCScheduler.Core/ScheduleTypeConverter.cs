using System.Text.Json;
using System.Text.Json.Serialization;

namespace PCScheduler.Core;

public class ScheduleTypeConverter : JsonConverter<ScheduleType>
{
    public override ScheduleType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            var val = reader.GetInt32();
            return val switch
            {
                0 => ScheduleType.Sleep,
                1 => ScheduleType.Wake,  // old Wake=1 → new Wake=2, map 1→Wake
                2 => ScheduleType.Wake,
                _ => ScheduleType.Sleep
            };
        }
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            return Enum.Parse<ScheduleType>(str);
        }
        return ScheduleType.Sleep;
    }

    public override void Write(Utf8JsonWriter writer, ScheduleType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
