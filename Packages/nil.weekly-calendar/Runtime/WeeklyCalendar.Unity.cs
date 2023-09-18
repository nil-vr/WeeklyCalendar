#if VRC_SDK_VRCSDK3
using System;
using System.Collections;
using Nil.Qr;
using UdonSharp;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using VRC.SDK3.Data;
using VRC.SDK3.Image;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

#if UNITY_EDITOR
using UdonSharpEditor;
using UnityEditor;
#endif

public partial class WeeklyCalendar : UdonSharpBehaviour
#if UNITY_EDITOR && !COMPILER_UDONSHARP
, IPreprocessCallbackBehaviour
#endif
{
    public GameObject DayHeader;
    public GameObject RowOdd;
    public GameObject RowEven;
    public GameObject Event;
    public GameObject JoinLink;

    public Transform Header;
    public Transform DayHeaders;
    public Transform Rows;

    public Transform Details;
    public Image DetailsTitleFill;
    public Text DetailsTitleText;
    public Transform DetailsPoster;
    public Text DetailsPosterStatus;
    public Vector2 DetailsPosterMaxSize;
    public Text DetailsStartTimeText;
    public Text DetailsDurationText;
    public Text DetailsDescriptionText;
    public Transform DetailsWeb;
    public Transform DetailsGroup;
    public Transform DetailsDiscord;
    public Transform DetailsHashtag;
    public Transform DetailsTwitter;
    public Transform DetailsJoinHeading;
    public Transform DetailsJoin;
    public Transform DetailsWorld;
    public Transform Qr;
    public Image QrTitleFill;
    public Text QrTitleText;
    public InputField QrText;
    public QrCode QrCode;
    public InputField QrLinkText;
    public Transform About;
    public Image AboutTitleFill;
    public Text AboutName;
    public Text AboutDescription;
    public QrButton AboutMore;
    public Text StatusLastUpdated;
    public Text StatusError;
    public Text StatusTimeZone;

    public VRCUrl Source;
    public VRCUrl[] Images;

    public string Data;
    [NonSerialized]
    DataDictionary schedule;

#if UNITY_EDITOR
    public bool UpdateOnBuild;

    [NonSerialized]
    public bool ConfigurationShown = false;
    [NonSerialized]
    public bool PrefabsShown = false;
    [NonSerialized]
    public bool SlotsShown = false;
    [NonSerialized]
    public bool SlotsDetailsShown = false;
    [NonSerialized]
    public bool SlotsQrShown = false;
    [NonSerialized]
    public bool SlotsAboutShown = false;
    [NonSerialized]
    public bool SlotsStatusShown = false;
    [NonSerialized]
    public bool DaysShown = false;
    [NonSerialized]
    public bool ColorsShown = false;
    [NonSerialized]
    public bool ThemeShown = false;
#endif

    public string Monday = "Monday";
    public string Tuesday = "Tuesday";
    public string Wednesday = "Wednesday";
    public string Thursday = "Thursday";
    public string Friday = "Friday";
    public string Saturday = "Saturday";
    public string Sunday = "Sunday";

    public DayOfWeek FirstDayOfWeek = DayOfWeek.Monday;
    public int AutoUpdateMinutes = 24 * 60;

    public string StatusLoading = "Loading…";

    public Color[] Colors = new Color[] { Color.black };

    public Sprite Focus;
    public Sprite Blur;

    public int SelectedTime;
    public DayOfWeek SelectedDay;
    public string SelectedEvent;

    public string ActiveLinkTitle;
    public string ActiveLinkDescription;
    public string ActiveLink;

    VRCImageDownloader downloader;

    DateTime lastUpdateStart;
    public long LastUpdatedUtc;

    #if UNITY_EDITOR && !COMPILER_UDONSHARP
    public int PreprocessOrder => 0;
#endif

    public void Start()
    {
        downloader = new VRCImageDownloader();
        var now = GetTime();
        InitFromJson(Data, false, now);

        if ((now.ToUnixTimeSeconds() - LastUpdatedUtc) / 60 > AutoUpdateMinutes)
        {
            Debug.Log($"Updating because {now.UtcDateTime} - {LastUpdatedUtc}s is greater than {AutoUpdateMinutes}m");
            RequestUpdate();
        }
    }

    DateTimeOffset GetTime()
    {
        return new DateTimeOffset(Networking.GetNetworkDateTime().ToUniversalTime(), TimeSpan.Zero);
    }

    void OnDestroy()
    {
        downloader.Dispose();
    }

    // Don't call it `Update` because that's a reserved name in Unity.
    public void UpdateData()
    {
#if COMPILER_UDONSHARP
        VRCStringDownloader.LoadUrl(Source, (VRC.Udon.Common.Interfaces.IUdonEventReceiver)this);
#else
        // We can't use VRCStringDownloader in edit mode because it requires an IUdonEventReceiver
        // and UdonSharpBehavior doesn't implement it.
        IEnumerator Download(string source)
        {
            var request = UnityWebRequest.Get(source);
            yield return request.SendWebRequest();
            if (request.isNetworkError || request.isHttpError)
            {
                Debug.LogError(request.error);
            }
            else
            {
                InitFromJson(request.downloadHandler.text, true, GetTime());
            }
        }
        StartCoroutine(Download(Source.Get()));
#endif
    }

    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
        if (StatusError != null)
        {
            StatusError.text = "";
        }

        InitFromJson(result.Result, true, GetTime());
    }

    void InitFromJson(string result, bool fresh, DateTimeOffset now)
    {
        if (!VRCJson.TryDeserializeFromJson(result, out var data))
        {
            LogError($"Invalid data: {data}");
            return;
        }
        if (data.TokenType != TokenType.DataDictionary)
        {
            LogError($"Expected data to be a dictionary but found {data.TokenType}");
            return;
        }
        Data = result;
        Render(data.DataDictionary, fresh, now);
    }
    
    public override void OnStringLoadError(IVRCStringDownload result)
    {
        Debug.LogError(result.Error);
        if (StatusError != null)
        {
            StatusError.text = result.Error;
        }
    }

    public void Render(DataDictionary data, bool fresh, DateTimeOffset now)
    {
        Clear();

        var dayOfWeek = FirstDayOfWeek;
        for (var j = 0; j < 7; j++, dayOfWeek = (DayOfWeek)(((int)dayOfWeek + 1) % 7))
        {
            switch (dayOfWeek) {
                case DayOfWeek.Monday:
                    CreateDayHeader(Monday);
                    break;
                case DayOfWeek.Tuesday:
                    CreateDayHeader(Tuesday);
                    break;
                case DayOfWeek.Wednesday:
                    CreateDayHeader(Wednesday);
                    break;
                case DayOfWeek.Thursday:
                    CreateDayHeader(Thursday);
                    break;
                case DayOfWeek.Friday:
                    CreateDayHeader(Friday);
                    break;
                case DayOfWeek.Saturday:
                    CreateDayHeader(Saturday);
                    break;
                case DayOfWeek.Sunday:
                    CreateDayHeader(Sunday);
                    break;
            }
        }

        if (!data.TryGetValue("meta", TokenType.DataDictionary, out var metat))
        {
            Debug.LogError("Missing meta data");
            return;
        }
        var meta = metat.DataDictionary;

        if (!data.TryGetValue("zones", TokenType.DataDictionary, out var zonest))
        {
            Debug.LogError("Missing zone data");
            return;
        }
        Zones = zonest.DataDictionary;

        if (!data.TryGetValue("events", TokenType.DataList, out var events))
        {
            Debug.LogError("Missing event data");
            return;
        }

        schedule = ConvertSchedule(events.DataList, now);

        if (StatusTimeZone != null)
        {
            StatusTimeZone.text = TimeZone;
        }

        if (fresh)
        {
            LastUpdatedUtc = now.ToUnixTimeSeconds();
            if (StatusLastUpdated != null && ConvertRealTimeToZone(now, TimeZone, out var localNow))
            {
                StatusLastUpdated.text = localNow.ToString("yyyy-MM-dd HH:mm");
            }
        }

        //TODO: round time down to hour or something?

        string title = null;
        if (meta.TryGetValue("title", TokenType.String, out var token))
        {
            title = token.String;
        }
        if (meta.TryGetValue("lang", TokenType.DataDictionary, out token) && token.DataDictionary.TryGetValue(Language, TokenType.DataDictionary, out token) && token.DataDictionary.TryGetValue("title", TokenType.String, out token))
        {
            title = token.String;
        }
        if (title != null)
        {
            ((Text)Header.GetComponentInChildren(typeof(Text))).text = title;
            AboutName.text = title;
            AboutMore.Description = title;
        }

        string description = null;
        if (meta.TryGetValue("desc", TokenType.String, out token))
        {
            description = token.String;
        }
        if (meta.TryGetValue("lang", TokenType.DataDictionary, out token) && token.DataDictionary.TryGetValue(Language, TokenType.DataDictionary, out token) && token.DataDictionary.TryGetValue("desc", TokenType.String, out token))
        {
            description = token.String;
        }
        if (description != null)
        {
            AboutDescription.text = description;
        }

        string link = null;
        if (meta.TryGetValue("link", TokenType.String, out token))
        {
            link = token.String;
        }
        if (meta.TryGetValue("lang", TokenType.DataDictionary, out token) && token.DataDictionary.TryGetValue(Language, TokenType.DataDictionary, out token) && token.DataDictionary.TryGetValue("link", TokenType.String, out token))
        {
            link = token.String;
        }
        if (link != null)
        {
            AboutMore.Link = link;
        }

        var times = schedule.GetKeys();
        times.Sort();
        var colorIndex = new DataDictionary();
        for (var i = 0; i < times.Count; i++)
        {
            var time = times[i].Int;
            var days = schedule[time].DataDictionary;

            var timeStr = TimeSpan.FromMinutes(time).ToString(@"hh\:mm");

            GameObject rowPrefab;
            if (i % 2 == 0)
            {
                rowPrefab = RowEven;
            }
            else
            {
                rowPrefab = RowOdd;
            }

            var row = Instantiate(rowPrefab, Rows);
            row.name = timeStr;
            var rowText = (Text)row.transform.Find("Time").gameObject.GetComponentInChildren(typeof(Text));
            rowText.text = timeStr;

            var rowDays = row.transform.Find("Days");

            dayOfWeek = FirstDayOfWeek;
            for (var j = 0; j < 7; j++, dayOfWeek = (DayOfWeek)(((int)dayOfWeek + 1) % 7))
            {
                var rowDay = rowDays.GetChild(j);
                if (!GetByDay(days, dayOfWeek, TokenType.DataList, out token))
                {
                    continue;
                }
                var day = token.DataList;
                for (var k = 0; k < day.Count; k++)
                {
                    var evt = day[k].DataDictionary;
                    var id = evt["id"];

                    Color color;
                    if (colorIndex.TryGetValue(id, TokenType.Int, out token))
                    {
                        color = Colors[token.Int];
                    }
                    else
                    {
                        var index = colorIndex.Count % Colors.Length;
                        color = Colors[index];
                        colorIndex[id] = index;
                    }

                    var rowEvent = Instantiate(Event, rowDay);
                    var rowEventButton = rowEvent.GetComponentInChildren<EventButton>();
                    rowEventButton.Time = time;
                    rowEventButton.Day = dayOfWeek;
                    rowEventButton.Event = evt["name"].String;
                    rowEventButton.Owner = this;
                    var rowEventText = (Text)rowEvent.GetComponentInChildren(typeof(Text));
                    rowEventText.text = evt["name"].String;
                    rowEventText.color = color;
                }
            }
        }
    }

    public void ShowDetails()
    {
        HideQr();
        About.gameObject.SetActive(false);

        if (schedule == null)
        {
            Debug.LogError("Cannot show details because schedule is null!");
            return;
        }
        if (!schedule.TryGetValue(SelectedTime, TokenType.DataDictionary, out var token))
        {
            Debug.LogError($"Got request to show details for event at {SelectedTime} but no events are at that time");
            return;
        }
        var timeRow = token.DataDictionary;
        if (!GetByDay(timeRow, SelectedDay, TokenType.DataList, out token))
        {
            Debug.LogError($"Got request to show details for event at {SelectedTime} on {SelectedDay} but no events are at that time on that day");
            return;
        }
        var day = token.DataList;
        DataDictionary evt = null;
        for (var i = 0; i < day.Count; i++)
        {
            evt = day[i].DataDictionary;
            if (evt.TryGetValue("name", TokenType.String, out token) && token.String == SelectedEvent)
            {
                break;
            }
        }
        if (evt == null)
        {
            Debug.LogError($"Got request to show details for event {SelectedEvent} at {SelectedTime} on {SelectedDay} but no event with that name is at that time on that day");
            return;
        }

        DetailsTitleFill.sprite = Focus;
        DetailsTitleText.text = SelectedEvent;

        if (evt.TryGetValue("poster", TokenType.DataDictionary, out token))
        {
            var poster = token.DataDictionary;

            DetailsPoster.gameObject.SetActive(true);
            var layout = (LayoutElement)DetailsPoster.GetComponent(typeof(LayoutElement));

            var width = (float)poster["w"].Double;
            var height = (float)poster["h"].Double;
            if (width / DetailsPosterMaxSize.x < height / DetailsPosterMaxSize.y)
            {
                layout.preferredHeight = layout.minHeight = DetailsPosterMaxSize.y;
                layout.preferredWidth = layout.minWidth = width * DetailsPosterMaxSize.y / height;
            }
            else
            {
                layout.preferredWidth = layout.minWidth = DetailsPosterMaxSize.x;
                layout.preferredHeight = layout.minHeight = height * DetailsPosterMaxSize.x / width;
            }
            DetailsPosterStatus.gameObject.SetActive(true);
            DetailsPosterStatus.text = StatusLoading;

            var url = Images[(int)poster["n"].Double];
            var renderer = ((RawImage)DetailsPoster.GetComponent(typeof(RawImage)));
            renderer.texture = Texture2D.whiteTexture;
            var textureInfo = new TextureInfo();
            textureInfo.GenerateMipMaps = true;
            textureInfo.WrapModeU = TextureWrapMode.Clamp;
            textureInfo.WrapModeV = TextureWrapMode.Clamp;
            downloader.DownloadImage(url, renderer.material, (IUdonEventReceiver)this, textureInfo);
        }
        else
        {
            DetailsPoster.gameObject.SetActive(false);
        }

        DetailsStartTimeText.text = TimeSpan.FromMinutes(SelectedTime).ToString(@"hh\:mm");
        DetailsDurationText.text = TimeSpan.FromMinutes((int)evt["duration"].Double).ToString(@"hh\:mm");

        if (evt.TryGetValue("desc", TokenType.String, out token))
        {
            var desc = token.String;
            DetailsDescriptionText.text = desc;
            DetailsDescriptionText.gameObject.SetActive(true);
        }
        else
        {
            DetailsDescriptionText.gameObject.SetActive(false);
        }

        if (evt.TryGetValue("group", TokenType.String, out token))
        {
            var group = token.String;
            SetDetailsLink(DetailsGroup, group, $"https://vrc.group/{group}");
        }
        else
        {
            DetailsGroup.gameObject.SetActive(false);
        }

        if (evt.TryGetValue("hashtag", TokenType.String, out token))
        {
            var hashtag = token.String;
            SetDetailsLink(DetailsHashtag, $"#{hashtag}", $"https://twitter.com/hashtag/{hashtag}");
        }
        else if (evt.TryGetValue("hashtag", TokenType.DataDictionary, out token))
        {
            var hashtag = token.DataDictionary;
            SetDetailsLink(DetailsHashtag, $"#{hashtag["display"].String}", $"https://twitter.com/hashtag/{hashtag["escaped"].String}");
        }
        else
        {
            DetailsHashtag.gameObject.SetActive(false);
        }

        if (evt.TryGetValue("discord", TokenType.String, out token))
        {
            var discord = token.String;
            SetDetailsLink(DetailsDiscord, discord, $"https://discord.gg/{discord}");
        }
        else
        {
            DetailsDiscord.gameObject.SetActive(false);
        }

        if (evt.TryGetValue("twitter", TokenType.String, out token))
        {
            var twitter = token.String;
            SetDetailsLink(DetailsTwitter, twitter, $"https://twitter.com/{twitter}");
        }
        else
        {
            DetailsTwitter.gameObject.SetActive(false);
        }

        if (evt.TryGetValue("web", TokenType.String, out token))
        {
            var web = token.String;
            SetDetailsLink(DetailsWeb, web, web);
        }
        else
        {
            DetailsWeb.gameObject.SetActive(false);
        }

        if (evt.TryGetValue("world", TokenType.DataDictionary, out token))
        {
            var world = token.DataDictionary;
            SetDetailsLink(DetailsWorld, world["name"].String, $"https://vrchat.com/home/launch?worldId={world["id"].String}");
        }
        else
        {
            DetailsWorld.gameObject.SetActive(false);
        }

        for (var i = DetailsJoin.childCount - 1; i >= 0; i--)
        {
#if UNITY_EDITOR
            DestroyImmediate(DetailsJoin.GetChild(i).gameObject);
#else
            Destroy(DetailsJoin.GetChild(i).gameObject);
#endif
        }

        if (evt.TryGetValue("join", TokenType.DataList, out token))
        {
            DetailsJoinHeading.gameObject.SetActive(true);

            var join = token.DataList;
            for (var i = 0; i < join.Count; i++)
            {
                if (join.TryGetValue(i, TokenType.DataDictionary, out token))
                {
                    var user = token.DataDictionary;
                    var name = user["name"].String;
                    var id = user["id"].String;

                    var link = Instantiate(JoinLink, DetailsJoin);
                    var linkButton = link.GetComponentInChildren<QrButton>();
                    linkButton.Description = name;
                    linkButton.Link = $"https://vrchat.com/home/user/{id}";
                    linkButton.Owner = this;
                    var linkText = (Text)linkButton.gameObject.GetComponentInChildren(typeof(Text));
                    linkText.text = name;
                }
            }
        }
        else
        {
            DetailsJoinHeading.gameObject.SetActive(false);
        }

        Qr.gameObject.SetActive(false);
        Details.gameObject.SetActive(true);
    }

    public void ShowAbout()
    {
        HideQr();
        Details.gameObject.SetActive(false);
        About.gameObject.SetActive(true);
    }

    public override void OnImageLoadError(IVRCImageDownload result)
    {
        Debug.LogError($"Poster load error: {result.ErrorMessage}");

        DetailsPosterStatus.gameObject.SetActive(true);
        DetailsPosterStatus.text = result.ErrorMessage;
    }

    public override void OnImageLoadSuccess(IVRCImageDownload result)
    {
        DetailsPosterStatus.gameObject.SetActive(false);

        var renderer = ((RawImage)DetailsPoster.GetComponent(typeof(RawImage)));
        renderer.texture = null;
    }

    void SetDetailsLink(Transform root, string desc, string link)
    {
        var button = root.gameObject.GetComponentInChildren<QrButton>();
        button.Description = desc;
        button.Link = link;
        var text = (Text)button.gameObject.GetComponent(typeof(Text));
        text.text = desc;
        root.gameObject.SetActive(true);
    }

    public void ShowQr()
    {
        AboutTitleFill.sprite = Blur;
        DetailsTitleFill.sprite = Blur;

        QrTitleText.text = ActiveLinkTitle;
        QrText.text = ActiveLinkDescription;
        QrCode.SetContent(ActiveLink);
        QrLinkText.text = ActiveLink;

        Qr.gameObject.SetActive(true);
    }

    public void HideQr()
    {
        AboutTitleFill.sprite = Focus;
        DetailsTitleFill.sprite = Focus;

        Qr.gameObject.SetActive(false);
    }

    public void RequestUpdate()
    {
        var now = Networking.GetNetworkDateTime().ToUniversalTime();
        if ((now - lastUpdateStart).TotalSeconds < 5)
        {
            Debug.Log("Skipping update because the last update was less than five seconds ago");
            return;
        }
        lastUpdateStart = now;
        UpdateData();
    }

    void Clear()
    {
        for (var i = DayHeaders.childCount - 1; i >= 0; i--)
        {
#if UNITY_EDITOR
            DestroyImmediate(DayHeaders.GetChild(i).gameObject);
#else
            Destroy(DayHeaders.GetChild(i).gameObject);
#endif
        }
        for (var i = Rows.childCount - 1; i >= 0; i--)
        {
#if UNITY_EDITOR
            DestroyImmediate(Rows.GetChild(i).gameObject);
#else
            Destroy(Rows.GetChild(i).gameObject);
#endif
        }

        Details.gameObject.SetActive(false);
        Qr.gameObject.SetActive(false);
        About.gameObject.SetActive(false);
    }

    void CreateDayHeader(string name)
    {
        var header = Instantiate(DayHeader, DayHeaders);
        header.name = name;
        var text = (Text)header.GetComponentInChildren(typeof(Text));
        text.text = name;
    }

    GameObject CreateTimeRow(DataDictionary timeData)
    {
        var time = timeData["time"].Int;

        var row = Instantiate(RowOdd, Rows);
        row.name = time.ToString();
        return row;
    }

    void LogError(object o) {
        Debug.LogError(o);
    }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
    public bool OnPreprocess()
    {
        if (UpdateOnBuild)
        {
            UpdateData();
        }
        return true;
    }
#endif
}

[CustomEditor(typeof(WeeklyCalendar))]
public class WeeklyCalendarEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draws the default convert to UdonBehaviour button, program asset field, sync settings, etc.
        if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

        WeeklyCalendar behavior = (WeeklyCalendar)target;

        var newSource = EditorGUILayout.DelayedTextField("Source", behavior.Source.Get());
        if (newSource != behavior.Source.Get())
        {
            behavior.Source = new VRCUrl(newSource);

            var source = new Uri(behavior.Source.Get());
            behavior.Images = new VRCUrl[256];
            for (var i = 0; i < 256; i++)
            {
                behavior.Images[i] = new VRCUrl(new Uri(source, $"posters/{i:x2}").ToString());
            }
        }

        behavior.ConfigurationShown = EditorGUILayout.BeginFoldoutHeaderGroup(behavior.ConfigurationShown, "Configuration");
        if (behavior.ConfigurationShown)
        {
            behavior.Language = EditorGUILayout.TextField("Language (ISO 639-1)", behavior.Language);
            behavior.TimeZone = EditorGUILayout.TextField("Time Zone (IANA TZDB)", behavior.TimeZone);
            behavior.FirstDayOfWeek = (DayOfWeek)EditorGUILayout.EnumPopup("First day of week", behavior.FirstDayOfWeek);
            behavior.AutoUpdateMinutes = EditorGUILayout.IntField("Auto updated after (minutes)", behavior.AutoUpdateMinutes);
            EditorGUILayout.HelpBox("The calendar will automatically update if the time since the last update is greater than this value. Because VRChat does not allow worlds to save data, this means the time since the calendar was updated in Unity (ie the time since the world was uploaded if automatic updating on building is enabled).", MessageType.Info);
            behavior.StatusLoading = EditorGUILayout.TextField("Loading status message", behavior.StatusLoading);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        behavior.PrefabsShown = EditorGUILayout.BeginFoldoutHeaderGroup(behavior.PrefabsShown, "Prefabs");
        if (behavior.PrefabsShown)
        {
            behavior.DayHeader = (GameObject)EditorGUILayout.ObjectField("Day Header", behavior.DayHeader, typeof(GameObject), false);
            behavior.RowOdd = (GameObject)EditorGUILayout.ObjectField("Row (odd)", behavior.RowOdd, typeof(GameObject), false);
            behavior.RowEven = (GameObject)EditorGUILayout.ObjectField("Row (even)", behavior.RowEven, typeof(GameObject), false);
            behavior.Event = (GameObject)EditorGUILayout.ObjectField("Event", behavior.Event, typeof(GameObject), false);
            behavior.JoinLink = (GameObject)EditorGUILayout.ObjectField("Join Link", behavior.JoinLink, typeof(GameObject), false);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        behavior.SlotsShown = EditorGUILayout.BeginFoldoutHeaderGroup(behavior.SlotsShown, "Slots");
        if (behavior.SlotsShown)
        {
            behavior.Header = (Transform)EditorGUILayout.ObjectField("Header", behavior.Header, typeof(Transform), true);
            behavior.DayHeaders = (Transform)EditorGUILayout.ObjectField("Day Headers", behavior.DayHeaders, typeof(Transform), true);
            behavior.Rows = (Transform)EditorGUILayout.ObjectField("Rows", behavior.Rows, typeof(Transform), true);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        behavior.SlotsDetailsShown = EditorGUILayout.BeginFoldoutHeaderGroup(behavior.SlotsDetailsShown, "Slots/Details");
        if (behavior.SlotsDetailsShown)
        {
            behavior.Details = (Transform)EditorGUILayout.ObjectField("Details Window", behavior.Details, typeof(Transform), true);
            behavior.DetailsTitleFill = (Image)EditorGUILayout.ObjectField("Title Fill", behavior.DetailsTitleFill, typeof(Image), true);
            behavior.DetailsTitleText = (Text)EditorGUILayout.ObjectField("Title Text", behavior.DetailsTitleText, typeof(Text), true);
            behavior.DetailsPoster = (Transform)EditorGUILayout.ObjectField("Poster", behavior.DetailsPoster, typeof(Transform), true);
            behavior.DetailsPosterStatus = (Text)EditorGUILayout.ObjectField("Poster Status", behavior.DetailsPosterStatus, typeof(Text), true);
            behavior.DetailsPosterMaxSize = EditorGUILayout.Vector2Field("Poster max size", behavior.DetailsPosterMaxSize);
            behavior.DetailsStartTimeText = (Text)EditorGUILayout.ObjectField("Start Time", behavior.DetailsStartTimeText, typeof(Text), true);
            behavior.DetailsDurationText = (Text)EditorGUILayout.ObjectField("Duration", behavior.DetailsDurationText, typeof(Text), true);
            behavior.DetailsDescriptionText = (Text)EditorGUILayout.ObjectField("Description", behavior.DetailsDescriptionText, typeof(Text), true);
            behavior.DetailsWeb = (Transform)EditorGUILayout.ObjectField("Web", behavior.DetailsWeb, typeof(Transform), true);
            behavior.DetailsGroup = (Transform)EditorGUILayout.ObjectField("Group", behavior.DetailsGroup, typeof(Transform), true);
            behavior.DetailsDiscord = (Transform)EditorGUILayout.ObjectField("Discord", behavior.DetailsDiscord, typeof(Transform), true);
            behavior.DetailsHashtag = (Transform)EditorGUILayout.ObjectField("HashtaDetailsHashtag", behavior.DetailsHashtag, typeof(Transform), true);
            behavior.DetailsTwitter = (Transform)EditorGUILayout.ObjectField("HashtaDetailsTwitter", behavior.DetailsTwitter, typeof(Transform), true);
            behavior.DetailsWorld = (Transform)EditorGUILayout.ObjectField("HashtaDetailsWorld", behavior.DetailsWorld, typeof(Transform), true);
            behavior.DetailsJoinHeading = (Transform)EditorGUILayout.ObjectField("Join Heading", behavior.DetailsJoinHeading, typeof(Transform), true);
            behavior.DetailsJoin = (Transform)EditorGUILayout.ObjectField("Join", behavior.DetailsJoin, typeof(Transform), true);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        behavior.SlotsQrShown = EditorGUILayout.BeginFoldoutHeaderGroup(behavior.SlotsQrShown, "Slots/QR");
        if (behavior.SlotsQrShown)
        {
            behavior.Qr = (Transform)EditorGUILayout.ObjectField("QR Window", behavior.Qr, typeof(Transform), true);
            behavior.QrTitleFill = (Image)EditorGUILayout.ObjectField("Title Fill", behavior.QrTitleFill, typeof(Image), true);
            behavior.QrTitleText = (Text)EditorGUILayout.ObjectField("Title Text", behavior.QrTitleText, typeof(Text), true);
            behavior.QrText = (InputField)EditorGUILayout.ObjectField("Text", behavior.QrText, typeof(InputField), true);
            behavior.QrCode = (QrCode)EditorGUILayout.ObjectField("QR Code", behavior.QrCode, typeof(QrCode), true);
            behavior.QrLinkText = (InputField)EditorGUILayout.ObjectField("Link Text", behavior.QrLinkText, typeof(InputField), true);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        behavior.SlotsAboutShown = EditorGUILayout.BeginFoldoutHeaderGroup(behavior.SlotsAboutShown, "Slots/About");
        if (behavior.SlotsAboutShown)
        {
            behavior.About = (Transform)EditorGUILayout.ObjectField("About Window", behavior.About, typeof(Transform), true);
            behavior.AboutTitleFill = (Image)EditorGUILayout.ObjectField("Title Fill", behavior.AboutTitleFill, typeof(Image), true);
            behavior.AboutName = (Text)EditorGUILayout.ObjectField("Name", behavior.AboutName, typeof(Text), true);
            behavior.AboutDescription = (Text)EditorGUILayout.ObjectField("Description", behavior.AboutDescription, typeof(Text), true);
            behavior.AboutMore = (QrButton)EditorGUILayout.ObjectField("More", behavior.AboutMore, typeof(QrButton), true);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        behavior.SlotsStatusShown = EditorGUILayout.BeginFoldoutHeaderGroup(behavior.SlotsStatusShown, "Slots/Status");
        if (behavior.SlotsStatusShown)
        {
            behavior.StatusLastUpdated = (Text)EditorGUILayout.ObjectField("Last Updated", behavior.StatusLastUpdated, typeof(Text), true);
            behavior.StatusError = (Text)EditorGUILayout.ObjectField("Error", behavior.StatusError, typeof(Text), true);
            behavior.StatusTimeZone = (Text)EditorGUILayout.ObjectField("Time Zone", behavior.StatusTimeZone, typeof(Text), true);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        behavior.DaysShown = EditorGUILayout.BeginFoldoutHeaderGroup(behavior.DaysShown, "Days of the week");
        if (behavior.DaysShown)
        {
            behavior.Monday = EditorGUILayout.TextField("Monday", behavior.Monday);
            behavior.Tuesday = EditorGUILayout.TextField("Tuesday", behavior.Tuesday);
            behavior.Wednesday = EditorGUILayout.TextField("Wednesday", behavior.Wednesday);
            behavior.Thursday = EditorGUILayout.TextField("Thursday", behavior.Thursday);
            behavior.Friday = EditorGUILayout.TextField("Friday", behavior.Friday);
            behavior.Saturday = EditorGUILayout.TextField("Saturday", behavior.Saturday);
            behavior.Sunday = EditorGUILayout.TextField("Sunday", behavior.Sunday);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        behavior.ColorsShown = EditorGUILayout.BeginFoldoutHeaderGroup(behavior.ColorsShown, "Colors");
        if (behavior.ColorsShown)
        {
            var length = EditorGUILayout.DelayedIntField("Count", behavior.Colors.Length);
            if (length != behavior.Colors.Length)
            {
                Array.Resize(ref behavior.Colors, length);
            }
            for (var i = 0; i < length; i++)
            {
                behavior.Colors[i] = EditorGUILayout.ColorField($"Element {i}", behavior.Colors[i]);
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        behavior.ThemeShown = EditorGUILayout.BeginFoldoutHeaderGroup(behavior.ThemeShown, "Theme");
        if (behavior.ThemeShown)
        {
            behavior.Focus = (Sprite)EditorGUILayout.ObjectField("Focus", behavior.Focus, typeof(Sprite), false);
            behavior.Blur = (Sprite)EditorGUILayout.ObjectField("Blur", behavior.Blur, typeof(Sprite), false);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        behavior.UpdateOnBuild = GUILayout.Toggle(behavior.UpdateOnBuild, "Update on build");

        if (GUILayout.Button("Update now"))
        {
            behavior.UpdateData();
        }
    }
}

#endif
