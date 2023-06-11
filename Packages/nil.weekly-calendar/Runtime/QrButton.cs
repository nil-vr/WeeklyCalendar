
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class QrButton : UdonSharpBehaviour
{
    public WeeklyCalendar Owner;
    public string Title;
    public string Description;
    public string Link;

    public void Click()
    {
        Owner.ActiveLinkTitle = Title;
        Owner.ActiveLinkDescription = Description;
        Owner.ActiveLink = Link;
        Owner.ShowQr();
    }
}
