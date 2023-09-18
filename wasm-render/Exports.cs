using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Text.Json;

// We don't need namespaces for one function in a WASM file.
#pragma warning disable CA1050

public partial class Exports
{
    [DynamicDependency(nameof(Main))]
    public Exports()
    {
    }

    [JSExport]
    public static string Render(string input, string settings)
    {
        using var doc = JsonDocument.Parse(input);
        var data = JsonToData(doc.RootElement).DataDictionary;

        var settingsStruct = JsonSerializer.Deserialize(settings, SourceGenerationContext.Default.Settings);

        var calendar = new WeeklyCalendar();

        calendar.Language = settingsStruct.Language ?? calendar.Language;
        calendar.TimeZone = settingsStruct.TimeZone ?? calendar.TimeZone;

        if (!data.TryGetValue("meta", TokenType.DataDictionary, out var metat))
        {
            throw new Exception("Missing meta data");
        }
        var meta = metat.DataDictionary;

        if (!data.TryGetValue("zones", TokenType.DataDictionary, out var zonest))
        {
            throw new Exception("Missing zone data");
        }
        calendar.Zones = zonest.DataDictionary;

        if (!data.TryGetValue("events", TokenType.DataList, out var events))
        {
            throw new Exception("Missing event data");
        }

        var times = calendar.ConvertSchedule(events.DataList, DateTimeOffset.Now);
        var converted = new DataList();
        foreach (var time in times)
        {
            var timeDict = new DataDictionary();
            timeDict["time"] = time.Key;
            timeDict["days"] = time.Value;
            converted.Add(timeDict);
        }

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            DataToJson(converted, writer);
        }
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    static DataToken JsonToData(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Undefined  => new DataToken { TokenType = TokenType.Null },
            JsonValueKind.Object => JsonToData(element.EnumerateObject()),
            JsonValueKind.Array => JsonToData(element.EnumerateArray()),
            JsonValueKind.String => element.GetString()!,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => new DataToken { TokenType = TokenType.Null },
            _ => throw new ArgumentOutOfRangeException(nameof(element), $"Unexpected value kind {element.ValueKind}"),
        };
    }

    static DataToken JsonToData(JsonElement.ObjectEnumerator o)
    {
        var dictionary = new DataDictionary();
        foreach (var p in o)
        {
            dictionary[p.Name] = JsonToData(p.Value);
        }
        return dictionary;
    }

    static DataToken JsonToData(JsonElement.ArrayEnumerator o)
    {
        var list = new DataList();
        foreach (var p in o)
        {
            list.Add(JsonToData(p));
        }
        return list;
    }

    static void DataToJson(DataToken token, Utf8JsonWriter writer)
    {
        switch (token.TokenType)
        {
            case TokenType.Null:
                writer.WriteNullValue();
                break;
            case TokenType.String:
                writer.WriteStringValue(token.String);
                break;
            case TokenType.DataList:
                DataToJson(token.DataList, writer);
                break;
            case TokenType.DataDictionary:
                DataToJson(token.DataDictionary, writer);
                break;
            case TokenType.Double:
                writer.WriteNumberValue(token.Double);
                break;
            case TokenType.Boolean:
                writer.WriteBooleanValue(token.Boolean);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(token), $"Unexpected token type {token.TokenType}");
        };
    }

    static void DataToJson(DataDictionary dictionary, Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        foreach (var p in dictionary)
        {
            writer.WritePropertyName(p.Key.String);
            DataToJson(p.Value, writer);
        }
        writer.WriteEndObject();
    }

    static void DataToJson(DataList list, Utf8JsonWriter writer)
    {
        writer.WriteStartArray();
        foreach (var v in list)
        {
            DataToJson(v, writer);
        }
        writer.WriteEndArray();
    }

    public static void Main()
    {
    }
}
