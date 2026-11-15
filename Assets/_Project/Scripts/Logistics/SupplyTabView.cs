// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Вміст вкладки «ПОСТАВКИ» в планшеті (роль 04). Звичайний C#-клас, як BuildGridView у ролі 05.
///  - зверху: коли наступна поставка, статус замовлення, зв'язок, кнопка «Пакувати корабель →»;
///  - 5 кроків: склади список → відправ у вікно зв'язку → старт із Землі → політ → посадка;
///  - знизу: що зараз у замовленні (ракета, ящики з іконками, маса, ризик) і порада «Центру керування».
/// Кнопка відкриває екран запуску (LaunchScreen) у режимі поставки — там і вибираємо вантаж.
/// </summary>
public class SupplyTabView
{
    readonly LaunchScreen owner;
    readonly VisualElement root, stepsRow, chips, rocketPic, lightDot, linkLed;
    readonly Label nextTitle, nextValue, nextLands, statusChip, linkLabel, orderTitle, rocketName, massLabel, riskLabel, emptyLabel, tipLabel;
    readonly Button openButton;
    readonly List<(VisualElement box, Label title, Label sub)> steps = new List<(VisualElement, Label, Label)>();

    static string T(string key) => LaunchText.Get(key);
    static string T(string key, params object[] args) => LaunchText.Get(key, args);

    public SupplyTabView(VisualElement pane, LaunchScreen owner, List<StyleSheet> sheets)
    {
        this.owner = owner;
        root = New("sup-root");
        foreach (var sheet in sheets) root.styleSheets.Add(sheet);
        pane.Add(root);

        // --- верх: наступна поставка, статус, кнопка ---
        var top = New("sup-top", root);
        var next = New("sup-next", top);
        nextTitle = NewLabel("t-label", next);
        nextValue = NewLabel("sup-big", next);
        nextLands = NewLabel("t-body", next);

        var status = New("sup-status", top);
        statusChip = NewLabel("chip", status);
        var link = New("sup-link", status);
        linkLed = New("led", link);
        linkLabel = NewLabel("t-body", link);

        New("grow", top);
        openButton = new Button(() => owner.OpenSupplyOrder());
        openButton.AddToClassList("btn");
        openButton.AddToClassList("btn--primary");
        openButton.AddToClassList("sup-open");
        top.Add(openButton);

        // --- 5 кроків ---
        stepsRow = New("sup-steps", root);
        for (int i = 0; i < 5; i++)
        {
            var box = New("sup-step", stepsRow);
            steps.Add((box, NewLabel("sup-step-title", box), NewLabel("sup-step-sub", box)));
        }

        // --- що в замовленні ---
        var order = New("sup-order", root);
        order.AddToClassList("panel");
        var rocketBox = New("sup-rocket-box", order);
        rocketPic = New("sup-rocket", rocketBox);
        rocketName = NewLabel("sup-rocket-name", rocketBox);

        var middle = New("sup-order-mid", order);
        orderTitle = NewLabel("t-label", middle);
        chips = New("sup-chips", middle);
        emptyLabel = NewLabel("t-body", middle);

        var right = New("sup-order-right", order);
        massLabel = NewLabel("t-body", right);
        var risk = New("sup-risk", right);
        lightDot = New("sup-light", risk);
        riskLabel = NewLabel("t-big", risk);
        tipLabel = NewLabel("note", right);
    }

    // ---------- Оновлення ----------

    public void Refresh(GameState s, SupplySystem sup, CommsSystem comms, MissionPlan plan)
    {
        var supply = s.Supply;
        int now = CommsSystem.AbsHour(s);
        bool firstDone = supply.FirstMissionDone;
        bool linkOk = comms != null && comms.IsOpen(s);
        int step = firstDone ? sup.Step(s) : 0;
        int launchAt = sup.ExpectedLaunchAt(s), arriveAt = sup.ExpectedArrivalAt(s);

        // наступна поставка
        nextTitle.text = T("sup.next");
        if (!firstDone)
        {
            nextValue.text = "—";
            nextLands.text = T("supply.first_mission");
        }
        else
        {
            int h = Mathf.Max(0, arriveAt - now);
            nextValue.text = T("sup.in", h / 24, h % 24);
            nextLands.text = supply.Status == SupplyStatus.Returned
                ? T("supply.returned", sup.Data.failedDelayDays)
                : sup.IsLate(s) ? T("supply.waiting") : T("supply.lands", LaunchScreen.DayTime(arriveAt));
            nextLands.EnableInClassList("c-bad", sup.IsLate(s) || supply.Status == SupplyStatus.Returned);
        }

        // статус і зв'язок
        statusChip.text = T("launch.status." + supply.Status);
        SetChip(statusChip, supply.Status == SupplyStatus.Draft ? (sup.IsLate(s) ? "chip--bad" : "chip--acc")
                          : supply.Status == SupplyStatus.Sent ? "chip--en"
                          : supply.Status == SupplyStatus.Launched ? "chip--ok" : "chip--warn");
        linkLed.EnableInClassList("led--ok", linkOk);
        linkLed.EnableInClassList("led--bad", !linkOk);
        linkLabel.text = linkOk ? T("launch.link_ok")
                       : s.Power.Comms <= 0 ? T("supply.link_nopower")
                       : T("supply.link_none", comms != null ? comms.HoursUntilOpen(s) : 0);

        openButton.text = !firstDone || supply.Status == SupplyStatus.Draft ? T("supply.open") : T("supply.view");

        // кроки
        string[] titles =
        {
            T("step.list"), T("step.send"), T("step.launch"), T("supply.step_flight", sup.FlightHours(s)), T("step.land")
        };
        string[] subs =
        {
            step == 0 ? T("step.now") : T("supply.done"),
            step > 1 ? T("supply.done") : linkOk ? T("step.now") : T("supply.link_in", comms != null ? comms.HoursUntilOpen(s) : 0),
            step > 2 ? T("supply.done") : LaunchScreen.DayTime(launchAt),
            step == 3 ? T("supply.lands_in", Mathf.Max(0, arriveAt - now)) : "",
            LaunchScreen.DayTime(arriveAt)
        };
        for (int i = 0; i < steps.Count; i++)
        {
            steps[i].title.text = titles[i];
            steps[i].sub.text = firstDone ? subs[i] : "";
            steps[i].box.EnableInClassList("sup-step--now", firstDone && i == step);
            steps[i].box.EnableInClassList("sup-step--done", firstDone && i < step);
        }

        // що в замовленні
        var rocket = sup.GetRocket(s);
        var tex = owner.RocketLook(rocket);
        rocketPic.style.backgroundImage = tex != null ? new StyleBackground(tex) : new StyleBackground(StyleKeyword.None);
        rocketName.text = rocket != null ? T(rocket.nameKey) : "";
        orderTitle.text = T("supply.order_title");

        chips.Clear();
        foreach (var line in supply.Cargo)
        {
            var item = sup.Catalog.Find(line.itemId);
            if (item == null) continue;
            var chip = New("sup-chip", chips);
            var icon = New("sup-chip-icon", chip);
            var iconTex = owner.CargoIcon(item.id);
            if (iconTex != null) icon.style.backgroundImage = new StyleBackground(iconTex);
            else icon.AddToClassList("sup-chip-icon--kit");
            NewLabel("sup-chip-text", chip).text = T(item.nameKey) + " ×" + line.count;
        }
        if (supply.Passengers.Count > 0)
            NewLabel("sup-chip-text", New("sup-chip", chips)).text = T("supply.passengers", supply.Passengers.Count);
        emptyLabel.text = supply.Cargo.Count == 0 && supply.Passengers.Count == 0 ? T("supply.empty") : "";

        var r = sup.Evaluate(s, false);
        massLabel.text = T("launch.mass", r.TotalMassKg.ToString("0"), r.CargoKg.ToString("0"));
        riskLabel.text = T("launch.risk", r.RiskPercent);
        lightDot.EnableInClassList("sup-light--green", r.Light == LaunchLight.Green);
        lightDot.EnableInClassList("sup-light--yellow", r.Light == LaunchLight.Yellow);
        lightDot.EnableInClassList("sup-light--red", r.Light == LaunchLight.Red);

        tipLabel.text = plan != null && firstDone ? T(plan.TipKey, plan.TipArgs) : "";
        tipLabel.EnableInClassList("note--warn", plan != null && plan.TipIsWarning);
        tipLabel.EnableInClassList("note--fact", plan != null && !plan.TipIsWarning);
    }

    // ---------- Допоміжне ----------

    static readonly string[] ChipKinds = { "chip--ok", "chip--warn", "chip--en", "chip--bad", "chip--acc" };

    static void SetChip(Label chip, string kind)
    {
        foreach (var k in ChipKinds) chip.EnableInClassList(k, k == kind);
    }

    static VisualElement New(string cls, VisualElement parent = null)
    {
        var e = new VisualElement();
        e.AddToClassList(cls);
        parent?.Add(e);
        return e;
    }

    static Label NewLabel(string cls, VisualElement parent)
    {
        var l = new Label();
        l.AddToClassList(cls);
        parent.Add(l);
        return l;
    }
}
