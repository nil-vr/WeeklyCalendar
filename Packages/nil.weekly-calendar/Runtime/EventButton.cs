using System;
using UdonSharp;
using UnityEngine;

public class EventButton : UdonSharpBehaviour
{
    public int Time;
    public DayOfWeek Day;
    public string Event;
    public WeeklyCalendar Owner;

    public void Click()
    {
        Debug.Log($"Clicked {Time} {Day} {Event}");
        Owner.SelectedTime = Time;
        Owner.SelectedDay = Day;
        Owner.SelectedEvent = Event;
        Owner.ShowDetails();
    }
}
