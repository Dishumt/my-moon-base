# NASA Data in moon-company-iasasus

*Owner: role 06 (Radiation & Data). Written for the NASA Space Apps Challenge 2026 judges.*

## In one paragraph

Solar storms in the game are **real 2024 events from NASA DONKI**, including the May 2024 storm series (active region 13664). Radiation doses are built on **real ISS dose numbers** (NASA RadLab / OSDR) and the **NASA career dose limit**. Health facts come from NASA spaceflight research (OSDR). The south-pole map is based on **NASA Moon Trek**, and food growing on **CSA** materials. We compress time so a child can see the effect in minutes, and we say so openly in the game.

## Datasets we use

| Source | What we took | Where in the game | File |
|---|---|---|---|
| **NASA DONKI** (Space Weather Database Of Notifications, Knowledge, Information) | 15 real 2024 events: solar flares (FLR, X-class), solar energetic particle events (SEP), the G5 geomagnetic storm of 10 May 2024 (GST) | Storm warnings show the real date, flare class and source link. Chapter 3 replays the **May 2024** series; chapter 2 has one weak storm (X1.8, 9 Oct 2024) | `Assets/_Project/Resources/Data/Nasa/donki_storms.json`, `storm_schedule.json` |
| **NASA OSDR RadLab** (ISS radiation data) | Typical ISS dose: ~0.5 mSv/day (range 0.44–0.88 mSv/day) | Base hourly dose of every astronaut | `Resources/Data/Nasa/radlab_dose.json` |
| **NASA-STD-3001** (crew health standard) | Career limit **600 mSv** | Dose bar = % of this limit; at 100 % the astronaut flies home | `radlab_dose.json` |
| **NASA OSDR** (Open Science Data Repository) | 8 kid-friendly facts: bones, muscles, sleep, radiation, food, plants, fluids, immune system | Fact cards / "NASA Data" screen | `Resources/Data/Nasa/facts.json` |
| **NASA Moon Trek** | Lunar south pole / Shackleton crater imagery (LRO) | Reference for the surface texture and map (role 02 / 01) | link below |
| **CSA** (Canadian Space Agency) | How plants grow in space | Greenhouse (role 03) | see role 03 |
| **NASA DONKI live API** (optional) | Flares of the last 7 days | Main menu "Space weather today", marked **LIVE**; if offline, the newest event from our file marked **ARCHIVE** | `Scripts/Radiation/SpaceWeather*.cs` |

### Links

- DONKI web service (no key needed): https://ccmc.gsfc.nasa.gov/DONKI/WS/get/FLR?startDate=2024-05-08&endDate=2024-05-15 (the same API is also served from `kauai.ccmc.gsfc.nasa.gov`, which some networks block)
- DONKI via api.nasa.gov: https://api.nasa.gov/ (DONKI section)
- RadLab data API: https://visualization.osdr.nasa.gov/radlab/gui/data-api/
- NASA-STD-3001: https://www.nasa.gov/ochmo/nasa-standard-3001/
- OSDR: https://osdr.nasa.gov/bio/repo/
- Moon Trek: https://trek.nasa.gov/moon/ (search "Shackleton"; LRO LROC / LOLA south-polar mosaics)

## How the data drives the game

1. **Hourly dose.** `dose per hour = ISS daily dose / 24 / 600 mSv × 100 % × time compression × place`.
   Place: outside ×4, inside ×1, module covered with regolith ×0.5.
2. **Dose zones.** 0–50 % green; 50–100 % yellow (the astronaut may only work inside); 100 % red (flies home with the next supply ship; a reserve astronaut can replace them).
3. **Solar storms.** Each storm is a real DONKI event. The player gets a warning **6 game hours** before, with the real date and class, and must bring everyone inside and give the **shelter** power. During the storm: powered shelter = small dose, other module = medium, outside = large. Real particles arrive minutes to hours after a flare, so warnings matter — this is the lesson.
4. **Storm strength 1–3** comes from the real event:
   - flare below X2 → 1, X2–X4.9 → 2, X5 and above → 3;
   - SEP (proton event) → 2, or 3 if linked to an X5+ flare;
   - geomagnetic storm Kp ≥ 8 → 3.
5. **Health.** A high dose (yellow zone) or hunger doubles the chance to get sick. Every sick astronaut needs medicine (med kits). Nobody dies.

## What we simplified, and why

| Simplification | Why |
|---|---|
| **Time is compressed ×50 for dose.** In reality reaching 300 mSv takes about 600 days on the ISS; in the game a player who keeps a crew member outside half of every day reaches the yellow zone in about 5 game days (≈15 minutes). | A 25-minute chapter must show the consequence of choices. The game tells the player this openly. |
| **Storm strength is 1–3**, not proton flux or flare class. | Kids 7–12 understand "weak / strong / very strong". The real class and date are still shown. |
| **Storm doses** are game numbers chosen so that shelter < module < outside. | Real dose during an event depends on spectrum, shielding and location; we keep the order and size of the effect, not exact values. |
| **ISS dose stands in for the Moon.** | ISS data is the best public long-term crew dose record. The Moon has no magnetic field, so the real dose there is higher — that is why our outside multiplier is ×4. |
| Illness is a simple chance per hour. | It teaches cause and effect (hunger, radiation → illness → medicine), not medicine itself. |

## Data status (honest note)

The event list, RadLab numbers and fact sources were first prepared during the hackathon while our build machine could not reach NASA servers. Every record has a `verified` flag:

- `donki_storms.json`: **all 15 events were checked against NASA DONKI on 2026-11-15 and are `verified: true`**. Each one now has its real DONKI id (e.g. `2024-05-11T01:10:00-FLR-001`), exact time and a link to its DONKI page; flare class, active region and the flare↔proton-event links matched what we had. The check can be repeated at any time with **Moon → 06: Check DONKI data** in Unity (it asks `ccmc.gsfc.nasa.gov` first, then `kauai.ccmc.gsfc.nasa.gov`, then the rate-limited `api.nasa.gov` mirror). If NASA DONKI cannot be reached, the tool prints a message and **leaves the file unchanged**. Events that are not found stay `false` and must be fixed or removed before submission.
- `radlab_dose.json` and `facts.json`: check against the RadLab API and OSDR studies; write the study number into `studyId`.

The game never shows archive data as live: live DONKI data is labelled **LIVE**, everything from our files **ARCHIVE**.

## Where AI was used (Space Apps rule)

- **Code** in `Assets/_Project/Scripts/Radiation/` and tests `Assets/_Project/Tests/EditMode/06*Tests.cs` were written with Claude (AI assistant) and reviewed by the team. Every file starts with an `AI-assisted` line.
- **Data files** in `Assets/_Project/Resources/Data/Nasa/` were assembled with Claude from NASA public sources (field `"_ai": "AI-assisted (Claude)"`). AI picked the events, wrote the strength rule and the kid-friendly fact texts. **The numbers themselves are NASA's**, not AI-generated; unverified records are flagged (see above).
- **This document** was drafted with Claude.
- No AI-generated images, logos or voices are used for NASA content.

See also `docs/AI_USE.md`.

## Source list for the in-game "NASA Data" screen (role 05)

| Title (EN) | Text (EN) | Badge | Link |
|---|---|---|---|
| Solar storms — NASA DONKI | Real solar flares and particle storms of 2024, including the May 2024 storms. | ARCHIVE | https://ccmc.gsfc.nasa.gov/DONKI/ |
| Space weather today — NASA DONKI | Flares of the last 7 days, if the internet works. | LIVE / ARCHIVE | https://ccmc.gsfc.nasa.gov/DONKI/ |
| Radiation on the ISS — NASA RadLab (OSDR) | How much radiation astronauts really get: about 0.5 mSv a day. | ARCHIVE | https://visualization.osdr.nasa.gov/radlab/ |
| Dose limit — NASA-STD-3001 | Every astronaut may get at most 600 mSv in a whole career. | ARCHIVE | https://www.nasa.gov/ochmo/nasa-standard-3001/ |
| Astronaut health — NASA OSDR | Facts about bones, muscles, sleep and food in space. | ARCHIVE | https://osdr.nasa.gov/bio/repo/ |
| Moon map — NASA Moon Trek | Pictures of the Moon's south pole and Shackleton crater. | ARCHIVE | https://trek.nasa.gov/moon/ |
| Space plants — CSA | How to grow food in space. | ARCHIVE | (role 03 adds the link) |
| Time note | In real life dose builds up over months. In the game time runs about 50 times faster. | — | — |
