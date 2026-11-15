// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine.UIElements;

/// <summary>
/// COMMS tab: can Earth hear us now? At the Moon's south pole Earth hangs low over the horizon and the
/// mountains often hide it, so we can talk only in "comms windows". The antenna also needs power.
/// Reads CommsSystem (role 04): IsOpen, HoursUntilOpen, UpcomingWindows.
/// Not a MonoBehaviour: TabletController creates it with the "pane-comms" element.
/// </summary>
public class CommsTabView
{
    const int WindowsShown = 4;

    readonly VisualElement root, windows, led;
    readonly Label statusHead, status, say, next, antenna, windowsHead, why;

    public CommsTabView(VisualElement pane)
    {
        UiKit.Show(pane.Q("pane-comms-stub"), false);
        pane.style.alignItems = Align.Stretch;
        pane.style.justifyContent = Justify.FlexStart;
        root = UiKit.Box(pane, "gt-pane");

        var side = UiKit.Box(root, "gt-side");
        statusHead = UiKit.Label(side, "gt-head");
        var statusRow = UiKit.Box(side, "row");
        led = UiKit.Box(statusRow, "cm-led");
        status = UiKit.Label(statusRow, "gt-big");
        say = UiKit.Label(side, "gt-text");
        next = UiKit.Label(side, "gt-big gt-big--en");
        antenna = UiKit.Label(side, "gt-text");

        var main = UiKit.Box(root, "gt-main");
        windowsHead = UiKit.Label(main, "gt-head");
        windows = UiKit.Box(main, "");
        why = UiKit.Label(main, "gt-note gt-note--fact");
    }

    public void Refresh()
    {
        var s = UiKit.State;
        var comms = UiKit.Sys<CommsSystem>();
        if (s == null || comms == null) return;

        bool powered = s.Power.Comms > 0;
        bool open = comms.IsOpen(s);
        string state = !powered ? "off" : open ? "ok" : "wait";

        UiKit.SetText(statusHead, Loc.Get("com.status"));
        UiKit.SetText(status, Loc.Get("com.st_" + state));
        status.EnableInClassList("gt-big--ok", state == "ok");
        status.EnableInClassList("gt-big--warn", state == "wait");
        status.EnableInClassList("gt-big--bad", state == "off");
        led.EnableInClassList("cm-led--ok", state == "ok");
        led.EnableInClassList("cm-led--wait", state == "wait");
        led.EnableInClassList("cm-led--off", state == "off");
        UiKit.SetText(say, Loc.Get("com.say_" + state));

        var upcoming = comms.UpcomingWindows(s, WindowsShown);
        int now = CommsSystem.AbsHour(s);
        if (open && upcoming.Count > 0) UiKit.SetText(next, Loc.Get("com.open_now", upcoming[0].EndHour - now));
        else UiKit.SetText(next, Loc.Get("com.next") + " · " + Loc.Get("com.in_h", comms.HoursUntilOpen(s)));
        UiKit.SetText(antenna, Loc.Get("com.antenna") + ": " + Loc.Get("com.blocks", s.Power.Comms) + (powered ? "" : " · " + Loc.Get("com.go_power")));
        antenna.EnableInClassList("gt-text--warn", !powered);

        UiKit.SetText(windowsHead, Loc.Get("com.windows"));
        windows.Clear();
        foreach (var w in upcoming)
        {
            bool isNow = w.StartHour <= now && now < w.EndHour;
            var row = UiKit.Box(windows, "cm-window" + (isNow ? " cm-window--now" : ""));
            UiKit.Box(row, "cm-led " + (isNow ? "cm-led--ok" : "cm-led--wait"));
            string when = Loc.Get("com.day", w.StartDay) + " · " + w.StartHourOfDay.ToString("00") + ":00 – "
                        + (w.EndHour % 24).ToString("00") + ":00";
            UiKit.Label(row, "", isNow ? when + "  · " + Loc.Get("com.now") : when);
        }
        UiKit.SetText(why, Loc.Get("com.why"));
    }
}
