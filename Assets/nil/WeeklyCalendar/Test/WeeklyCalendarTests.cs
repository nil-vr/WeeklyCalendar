using System;
using NUnit.Framework;
using UnityEngine;
using VRC.SDK3.Data;

namespace Tests
{
    public class WeeklyCalendarTests
    {
        WeeklyCalendar GetCalendar()
        {
            var go = new GameObject();
            var calendar = go.AddComponent<WeeklyCalendar>();
            calendar.Zones = new DataDictionary
            {
                ["America/New_York"] = new DataDictionary
                {
                    ["r"] = new DataList
                    {
                        new DataDictionary
                        {
                            ["o"] = -240.0,
                        },
                        new DataDictionary
                        {
                            ["s"] = 1699164000.0,
                            ["o"] = -300.0,
                        },
                        new DataDictionary
                        {
                            ["s"] = 1710054000.0,
                            ["o"] = -240.0,
                        },
                    },
                },
            };
            return calendar;
        }

        [Test]
        public void ConvertRealTimeToZone_ForEarlyTime_UsesBaseOffset()
        {
            var now = DateTimeOffset.Parse("2023-05-15T22:43:11.0012313-04:00");
            var success = GetCalendar().ConvertRealTimeToZone(now.ToUniversalTime(), "America/New_York", out var same);
            Assert.IsTrue(success);
            Assert.AreEqual(now, same);
        }

        [Test]
        public void ConvertRealTimeToZone_ForLaterTime_UsesLaterOffset()
        {
            var now = DateTimeOffset.Parse("2023-11-15T21:44:46.8910277-05:00");
            var success = GetCalendar().ConvertRealTimeToZone(now.ToUniversalTime(), "America/New_York", out var same);
            Assert.IsTrue(success);
            Assert.AreEqual(now, same);
        }

        [Test]
        public void ConvertZoneTimeToReal_ForEarlyTime_UsesBaseOffset()
        {
            var now = DateTimeOffset.Parse("2023-05-15T22:43:11.0012313-04:00");
            var success = GetCalendar().ConvertZoneTimeToReal(now.Date + now.TimeOfDay, "America/New_York", out var same);
            Assert.IsTrue(success);
            Assert.AreEqual(now, same);
        }

        [Test]
        public void ConvertZoneTimeToReal_ForLaterTime_UsesLaterOffset()
        {
            var now = DateTimeOffset.Parse("2023-11-15T21:44:46.8910277-05:00");
            var success = GetCalendar().ConvertZoneTimeToReal(now.Date + now.TimeOfDay, "America/New_York", out var same);
            Assert.IsTrue(success);
            Assert.AreEqual(now, same);
        }

        [Test]
        public void ConvertZoneTimeToReal_ForImpossibleTime_ReturnsFalse()
        {
            // America/New_York goes straight from 1:59 to 3:00 on this date.
            var success = GetCalendar().ConvertZoneTimeToReal(new DateTime(2024, 3, 10, 2, 30, 0, DateTimeKind.Unspecified), "America/New_York", out _);
            Assert.IsFalse(success);
        }

        [Test]
        public void ConvertZoneTimeToReal_ForAmbiguousTime_ReturnsFirst()
        {
            // America/New_York returns straight from 1:59 to 1:00 on this date.
            var now = DateTimeOffset.Parse("2023-11-05T01:30:00.0000000-04:00");
            var success = GetCalendar().ConvertZoneTimeToReal(now.Date + now.TimeOfDay, "America/New_York", out var same);
            Assert.IsTrue(success);
            Assert.AreEqual(now, same);
        }

        [Test]
        public void GetEventTimeForDate_ReturnsTime()
        {
            var success = GetCalendar().GetEventTimeForDate(new DataDictionary
            {
                ["start"] = 1230.0,
                ["sunday"] = new DataDictionary(),
            }, DateTime.Parse("2023-05-21"), "America/New_York", out var time, out var scheduled);
            Assert.IsTrue(success);
            Assert.AreEqual(DateTimeOffset.Parse("2023-05-21T20:30:00.0000000-04:00"), time);
            Assert.AreEqual(new DataDictionary(), scheduled);
        }

        [Test]
        public void GetEventTimeForDate_WhenNotScheduled_ReturnsTime()
        {
            var success = GetCalendar().GetEventTimeForDate(new DataDictionary
            {
                ["start"] = 1230.0,
                ["sunday"] = new DataDictionary(),
            }, DateTime.Parse("2023-05-22"), "America/New_York", out var time, out var scheduled);
            Assert.IsTrue(success);
            Assert.AreEqual(DateTimeOffset.Parse("2023-05-22T20:30:00.0000000-04:00"), time);
            Assert.IsNull(scheduled);
        }

        [Test]
        public void GetEventTimeForDate_WhenExplicitlyConfirmed_ReturnsScheduled()
        {
            var success = GetCalendar().GetEventTimeForDate(new DataDictionary
            {
                ["start"] = 1230.0,
                ["sunday"] = new DataDictionary(),
                ["confirmed"] = new DataList
                {
                    "2023-05-22",
                },
            }, DateTime.Parse("2023-05-22"), "America/New_York", out var time, out var scheduled);
            Assert.IsTrue(success);
            Assert.AreEqual(DateTimeOffset.Parse("2023-05-22T20:30:00.0000000-04:00"), time);
            Assert.AreEqual(new DataDictionary
            {
                ["confirmed"] = true,
            }, scheduled);
        }

        [Test]
        public void GetEventTimeForDate_WhenNotConfirmed_ReturnsScheduled()
        {
            var success = GetCalendar().GetEventTimeForDate(new DataDictionary
            {
                ["start"] = 1230.0,
                ["sunday"] = new DataDictionary(),
                ["confirmed"] = new DataList(),
            }, DateTime.Parse("2023-05-21"), "America/New_York", out var time, out var scheduled);
            Assert.IsTrue(success);
            Assert.AreEqual(DateTimeOffset.Parse("2023-05-21T20:30:00.0000000-04:00"), time);
            Assert.AreEqual(new DataDictionary
            {
                ["confirmed"] = false,
            }, scheduled);
        }
    }
}
