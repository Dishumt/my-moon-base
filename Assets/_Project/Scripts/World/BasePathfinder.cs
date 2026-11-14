// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Пошук шляху навколо модулів, щоб астронавти й роботи не ходили крізь стіни.
/// Працює як A* (алгоритм, що шукає найкоротший шлях по клітинках) на сітці з кроком 0,5 м навколо бази.
/// Далеко від бази перешкод немає, тож там ідемо по прямій.
/// Звичайний C#-клас (не MonoBehaviour) — його перевіряють тести 01PathfinderTests.
/// Координати — на землі: Vector2(x, z).
/// </summary>
public static class BasePathfinder
{
    public const float Cell = 0.5f;

    /// <summary>
    /// Шлях від from до to в обхід перешкод (прямокутники в метрах, x/z).
    /// area — де шукати (навколо бази). Повертає точки шляху без from, останньою — to.
    /// </summary>
    public static List<Vector2> FindPath(Vector2 from, Vector2 to, List<Rect> blockers, Rect area)
    {
        var result = new List<Vector2>();
        if (Clear(from, to, blockers)) { result.Add(to); return result; } // видно ціль — просто йдемо

        // Точки поза зоною пошуку притягуємо до її краю: там перешкод немає
        Vector2 a = Clamp(from, area), b = Clamp(to, area);
        int w = Mathf.CeilToInt(area.width / Cell), h = Mathf.CeilToInt(area.height / Cell);
        var start = ToCell(a, area, w, h);
        var goal = ToCell(b, area, w, h);

        var blocked = new bool[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            blocked[y * w + x] = Inside(CellCenter(x, y, area), blockers);
        // Старт і ціль не вважаємо зайнятими (наприклад, ціль — двері шлюзу впритул до стіни)
        blocked[start.y * w + start.x] = false;
        blocked[goal.y * w + goal.x] = false;

        var cells = AStar(start, goal, blocked, w, h);
        if (cells == null) { result.Add(to); return result; } // шляху немає — ідемо напряму

        // Клітинки → точки, і «натягуємо нитку»: прибираємо точки, між якими видно напряму
        var pts = new List<Vector2> { from };
        foreach (var c in cells) pts.Add(CellCenter(c.x, c.y, area));
        pts.Add(to);
        var smooth = new List<Vector2> { from };
        int i = 0;
        while (i < pts.Count - 1)
        {
            int j = pts.Count - 1;
            while (j > i + 1 && !Clear(pts[i], pts[j], blockers)) j--;
            smooth.Add(pts[j]);
            i = j;
        }
        smooth.RemoveAt(0);
        return smooth;
    }

    /// <summary>Чи можна пройти по прямій від p до q (не зачепивши перешкод).</summary>
    public static bool Clear(Vector2 p, Vector2 q, List<Rect> blockers)
    {
        float len = Vector2.Distance(p, q);
        int steps = Mathf.Max(1, Mathf.CeilToInt(len / (Cell * 0.5f)));
        for (int k = 1; k < steps; k++) // кінці не перевіряємо: двері й точки біля стіни
            if (Inside(Vector2.Lerp(p, q, (float)k / steps), blockers)) return false;
        return true;
    }

    static bool Inside(Vector2 p, List<Rect> blockers)
    {
        foreach (var r in blockers) if (r.Contains(p)) return true;
        return false;
    }

    static Vector2 Clamp(Vector2 p, Rect area) =>
        new Vector2(Mathf.Clamp(p.x, area.xMin + Cell, area.xMax - Cell), Mathf.Clamp(p.y, area.yMin + Cell, area.yMax - Cell));

    static Vector2Int ToCell(Vector2 p, Rect area, int w, int h) =>
        new Vector2Int(Mathf.Clamp((int)((p.x - area.xMin) / Cell), 0, w - 1), Mathf.Clamp((int)((p.y - area.yMin) / Cell), 0, h - 1));

    static Vector2 CellCenter(int x, int y, Rect area) =>
        new Vector2(area.xMin + (x + 0.5f) * Cell, area.yMin + (y + 0.5f) * Cell);

    // ---------- A* ----------

    static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
    static readonly int[] DY = { 0, 0, 1, -1, 1, -1, 1, -1 };

    static List<Vector2Int> AStar(Vector2Int s, Vector2Int g, bool[] blocked, int w, int h)
    {
        int n = w * h, si = s.y * w + s.x, gi = g.y * w + g.x;
        var cost = new float[n];
        var from = new int[n];
        var closed = new bool[n];
        for (int i = 0; i < n; i++) { cost[i] = float.MaxValue; from[i] = -1; }
        cost[si] = 0f;

        var open = new MinHeap();
        open.Push(si, Heur(s, g));
        while (open.Count > 0)
        {
            int cur = open.Pop();
            if (closed[cur]) continue;
            if (cur == gi) break;
            closed[cur] = true;
            int cx = cur % w, cy = cur / w;
            for (int k = 0; k < 8; k++)
            {
                int nx = cx + DX[k], ny = cy + DY[k];
                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                int ni = ny * w + nx;
                if (blocked[ni] || closed[ni]) continue;
                // по діагоналі не зрізаємо кути стін
                if (k >= 4 && (blocked[cy * w + nx] || blocked[ny * w + cx])) continue;
                float step = k >= 4 ? 1.4142f : 1f;
                if (cost[cur] + step < cost[ni])
                {
                    cost[ni] = cost[cur] + step;
                    from[ni] = cur;
                    open.Push(ni, cost[ni] + Heur(new Vector2Int(nx, ny), g));
                }
            }
        }
        if (from[gi] < 0 && gi != si) return null;

        var path = new List<Vector2Int>();
        for (int c = gi; c != si && c >= 0; c = from[c]) path.Add(new Vector2Int(c % w, c / w));
        path.Reverse();
        return path;
    }

    static float Heur(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x), dy = Mathf.Abs(a.y - b.y);
        return Mathf.Max(dx, dy) + 0.4142f * Mathf.Min(dx, dy);
    }

    /// <summary>Купа (heap): швидко дістає клітинку з найменшою оцінкою.</summary>
    class MinHeap
    {
        readonly List<int> items = new List<int>();
        readonly List<float> keys = new List<float>();
        public int Count => items.Count;

        public void Push(int item, float key)
        {
            items.Add(item); keys.Add(key);
            int i = items.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (keys[p] <= keys[i]) break;
                Swap(i, p); i = p;
            }
        }

        public int Pop()
        {
            int top = items[0];
            int last = items.Count - 1;
            Swap(0, last);
            items.RemoveAt(last); keys.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int l = 2 * i + 1, r = l + 1, m = i;
                if (l < items.Count && keys[l] < keys[m]) m = l;
                if (r < items.Count && keys[r] < keys[m]) m = r;
                if (m == i) break;
                Swap(i, m); i = m;
            }
            return top;
        }

        void Swap(int a, int b)
        {
            (items[a], items[b]) = (items[b], items[a]);
            (keys[a], keys[b]) = (keys[b], keys[a]);
        }
    }
}
