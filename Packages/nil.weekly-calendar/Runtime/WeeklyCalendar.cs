using System;

#if VRC_SDK_VRCSDK3
using VRC.SDK3.Data;
#endif

// UdonSharp requires violation of IDE0028
#pragma warning disable IDE0028

public partial class WeeklyCalendar
{
    public string Language = "en";
    public string TimeZone = "America/New_York";

#if !COMPILER_UDONSHARP
    public
#endif
    DataDictionary Zones;

#if UNITY_INCLUDE_TESTS
    public
#endif
    bool ConvertRealTimeToZone(DateTimeOffset time, string zoneName, out DateTimeOffset inZone)
    {
        inZone = default;
        var tsm = time.ToUnixTimeSeconds() / 60;
        if (!Zones.TryGetValue(zoneName, TokenType.DataDictionary, out var zonet))
        {
            LogError("Found event with missing time zone");
            return false;
        }
        var zone = zonet.DataDictionary;
        if (!zone.TryGetValue("r", TokenType.DataList, out var offsetst))
        {
            LogError("Found time zone with no offsets");
            return false;
        }
        var offsets = offsetst.DataList;
        var offsetTs = TimeSpan.Zero;
        for (var i = 0; i < offsets.Count; i += 1)
        {
            if (!offsets.TryGetValue(i, TokenType.DataDictionary, out var offsett))
            {
                LogError("Offset is not an object");
                return false;
            }
            var offset = offsett.DataDictionary;
            if (offset.TryGetValue("s", TokenType.Double, out var startt))
            {
                if (startt.Double > tsm)
                {
                    break;
                }
            }
            if (!offset.TryGetValue("o", TokenType.Double, out var valuet))
            {
                LogError("Offset has no value");
                return false;
            }
            offsetTs = TimeSpan.FromMinutes(valuet.Double);
        }
        inZone = time.ToOffset(offsetTs);
        return true;
    }

#if UNITY_INCLUDE_TESTS
    public
#endif
    bool ConvertZoneTimeToReal(DateTime zoneTime, string zoneName, out DateTimeOffset inZone)
    {
        inZone = default;

        if (!Zones.TryGetValue(zoneName, TokenType.DataDictionary, out var zonet))
        {
            LogError("Found event with missing time zone");
            return false;
        }
        var zone = zonet.DataDictionary;
        if (!zone.TryGetValue("r", TokenType.DataList, out var offsetst))
        {
            LogError("Found time zone with no offsets");
            return false;
        }
        var offsets = offsetst.DataList;

        for (var i = 0; i < offsets.Count; i++)
        {
            if (!offsets.TryGetValue(i, TokenType.DataDictionary, out var offsett))
            {
                LogError("Offset is not an object");
                return false;
            }
            var offset = offsett.DataDictionary;
            var start = DateTimeOffset.MinValue;
            if (offset.TryGetValue("s", TokenType.Double, out var startt))
            {
                start = DateTimeOffset.FromUnixTimeSeconds((long)startt.Double);
                if (start > inZone)
                {
                    break;
                }
            }
            if (!offset.TryGetValue("o", TokenType.Double, out var valuet))
            {
                LogError("Offset has no value");
                return false;
            }
            var offsetTs = TimeSpan.FromMinutes(valuet.Double);
            var differenceInOffset = offsetTs - inZone.Offset;
            inZone = new DateTimeOffset(zoneTime, offsetTs);
            var sinceTransition = zoneTime - start;
            if (differenceInOffset > sinceTransition)
            {
                // Local time advanced by more than the time since the transition.
                // This means the local time falls in the "spring forward" time and does not exist on this date.
                return false;
            }
        }

        return true;
    }

#if UNITY_INCLUDE_TESTS
    public
#endif
    bool GetEventTimeForDate(DataDictionary evt, DateTime date, string zoneName, out DateTimeOffset inZone, out DataDictionary scheduled)
    {
        inZone = default;
        scheduled = default;
        var scheduledIsOwned = false;

        if (!evt.TryGetValue("start", TokenType.Double, out var startt))
        {
            LogError("Event has no start time");
            return false;
        }
        var start = TimeSpan.FromMinutes(startt.Double);
        
        if (GetByDay(evt, date.DayOfWeek, TokenType.DataDictionary, out var dayt))
        {
            var day = dayt.DataDictionary;
            scheduled = day;
            if (day.TryGetValue("start", TokenType.Double, out var dayStartt))
            {
                start = TimeSpan.FromMinutes(dayStartt.Double);
            }
        }

        if (!ConvertZoneTimeToReal(date + start, zoneName, out inZone))
        {
            return false;
        }

        var dateStr = date.ToString("yyyy-MM-dd");
        if (evt.TryGetValue("confirmed", TokenType.DataList, out var confirmedList))
        {
            if (confirmedList.DataList.Contains(dateStr))
            {
                if (scheduled == null)
                {
                    scheduled = new DataDictionary();
                    scheduledIsOwned = true;
                }
                scheduled["confirmed"] = true;
            }
            else if (scheduled != null)
            {
                if (!scheduledIsOwned)
                {
                    scheduled = scheduled.ShallowClone();
                    scheduledIsOwned = true;
                }
                scheduled["confirmed"] = false;
            }
        }

        if ((evt.TryGetValue("startDate", TokenType.Double, out var startDatet) && inZone.ToUnixTimeSeconds() < startDatet.Double) ||
            (evt.TryGetValue("endDate", TokenType.Double, out var endDatet) && endDatet.Double < inZone.ToUnixTimeSeconds()))
        {
            if (!scheduledIsOwned)
            {
                scheduled = scheduled.ShallowClone();
                scheduledIsOwned = true;
            }
            scheduled["hide"] = true;
        }

        if (scheduled != null && !(scheduled.TryGetValue("confirmed", TokenType.Boolean, out var confirmedt) && confirmedt.Boolean) &&
            ((evt.TryGetValue("canceled", TokenType.Boolean, out var allCanceledt) && allCanceledt.Boolean) ||
            (evt.TryGetValue("canceled", TokenType.DataList, out var canceledt) && canceledt.DataList.Contains(dateStr)) ||
            (evt.TryGetValue("weeks", TokenType.DataList, out var weekst) && !weekst.DataList.Contains((double)((date.Day - 1) / 7 + 1)))))
        {
            if (!scheduledIsOwned)
            {
                scheduled = scheduled.ShallowClone();
                scheduledIsOwned = true;
            }
            scheduled["canceled"] = true;
        }

        // Prevent warning about final unused write.
        KeepAlive(scheduledIsOwned);

        return true;
    }

    void KeepAlive(object _)
    {
        // UdonSharp doesn't support GC.KeepAlive.
    }

#if !COMPILER_UDONSHARP
    public
#endif
    DataDictionary ConvertSchedule(DataList events, DateTimeOffset now)
    {
        var times = new DataDictionary();

        if (!ConvertRealTimeToZone(now, TimeZone, out var localNow))
        {
            LogError("Invalid local time zone");
            return times;
        }

        for (var i = 0; i < events.Count; i++)
        {
            if (!events.TryGetValue(i, TokenType.DataDictionary, out var token))
            {
                LogError($"Event {i} is not an object");
                continue;
            }
            var evt = token.DataDictionary;

            if (!evt.TryGetValue("tz", TokenType.String, out token))
            {
                LogError($"Event {i} has no time zone");
                continue;
            }
            var zone = token.String;

            if (!ConvertRealTimeToZone(now, zone, out var evtZoneNow))
            {
                LogError($"Event {i} has invalid time zone");
                continue;
            }

            DataDictionary language = null;
            if (evt.TryGetValue("lang", TokenType.DataDictionary, out token) &&
                token.DataDictionary.TryGetValue(Language, TokenType.DataDictionary, out token))
            {
                language = token.DataDictionary;
            }

            var date = evtZoneNow.Date;
            var limit = localNow.DateTime.AddDays(7);
            for (int j = 0; j < 8; j++, date = date.AddDays(1))
            {
                if (!GetEventTimeForDate(evt, date, zone, out var datetime, out var scheduled))
                {
                    // Impossible time (daylight savings)
                    continue;
                }
                if (datetime < now)
                {
                    // Event has already occurred today
                    continue;
                }
                if (!ConvertRealTimeToZone(datetime, TimeZone, out var localTime))
                {
                    // Impossible time (daylight savings)
                    continue;
                }
                if (localTime.DateTime >= limit)
                {
                    // Reached next week
                    break;
                }
                if (scheduled == null)
                {
                    // Not scheduled for this date
                    continue;
                }
                if (scheduled.TryGetValue("hide", TokenType.Boolean, out token) && token.Boolean)
                {
                    continue;
                }

                var dayOfWeek = localTime.DayOfWeek;

                DataDictionary days;
                var timeMinutes = localTime.TimeOfDay.Hours * 60 + localTime.TimeOfDay.Minutes;
                if (times.TryGetValue(timeMinutes, TokenType.DataDictionary, out token))
                {
                    days = token.DataDictionary;
                }
                else
                {
                    days = new DataDictionary();
                    days["sunday"] = new DataList();
                    days["monday"] = new DataList();
                    days["tuesday"] = new DataList();
                    days["wednesday"] = new DataList();
                    days["thursday"] = new DataList();
                    days["friday"] = new DataList();
                    days["saturday"] = new DataList();
                    times[timeMinutes] = days;
                }
                if (!GetByDay(days, dayOfWeek, TokenType.DataList, out token))
                {
                    LogError("Day of week does not exist");
                    continue;
                }
                var day = token.DataList;
                var occurrence = new DataDictionary();

                occurrence["id"] = i;
                CopyToken(occurrence, "name", evt, language, dayOfWeek);
                CopyToken(occurrence, "duration", evt, language, dayOfWeek);
                CopyToken(occurrence, "poster", evt, language, dayOfWeek);
                CopyToken(occurrence, "desc", evt, language, dayOfWeek);
                CopyToken(occurrence, "web", evt, language, dayOfWeek);
                CopyToken(occurrence, "discord", evt, language, dayOfWeek);
                CopyToken(occurrence, "group", evt, language, dayOfWeek);
                CopyToken(occurrence, "hashtag", evt, language, dayOfWeek);
                CopyToken(occurrence, "twitter", evt, language, dayOfWeek);
                CopyToken(occurrence, "join", evt, language, dayOfWeek);
                CopyToken(occurrence, "world", evt, language, dayOfWeek);
                CopyToken(occurrence, "platforms", evt);

                day.Add(occurrence);
            }
        }

        return times;
    }

    void CopyToken(DataDictionary occurrence, string key, DataDictionary evt)
    {
        if (evt.ContainsKey(key))
        {
            occurrence[key] = evt[key];
        }
    }

    void CopyToken(DataDictionary occurrence, DataToken key, DataDictionary evt, DataDictionary language, DayOfWeek dayOfWeek)
    {
        if (language != null && GetByDay(language, dayOfWeek, TokenType.DataDictionary, out var token) && token.DataDictionary.ContainsKey(key))
        {
            occurrence[key] = token.DataDictionary[key];
            return;
        }
        if (GetByDay(evt, dayOfWeek, TokenType.DataDictionary, out token) && token.DataDictionary.ContainsKey(key))
        {
            occurrence[key] = token.DataDictionary[key];
            return;
        }
        if (language != null && language.ContainsKey(key))
        {
            occurrence[key] = language[key];
            return;
        }
        if (evt.ContainsKey(key))
        {
            occurrence[key] = evt[key];
        }
    }

#if UNITY_INCLUDE_TESTS
    public
#endif
    static bool GetByDay(DataDictionary dict, DayOfWeek day, TokenType type, out DataToken value)
    {
        var key = "";
        switch (day) {
            case DayOfWeek.Sunday: 
                key = "sunday";
                break;
            case DayOfWeek.Monday:
                key = "monday";
                break;
            case DayOfWeek.Tuesday:
                key = "tuesday";
                break;
            case DayOfWeek.Wednesday:
                key = "wednesday";
                break;
            case DayOfWeek.Thursday:
                key = "thursday";
                break;
            case DayOfWeek.Friday:
                key = "friday";
                break;
            case DayOfWeek.Saturday:
                key = "saturday";
                break;
        }
        return dict.TryGetValue(key, type, out value);
    }
}
