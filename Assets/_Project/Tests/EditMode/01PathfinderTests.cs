// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>Тести пошуку шляху навколо модулів (роль 01).</summary>
public class PathfinderTests
{
    static readonly Rect Area = new Rect(-20f, -20f, 40f, 40f);

    [Test]
    public void NoBlockers_GoesStraight()
    {
        var path = BasePathfinder.FindPath(new Vector2(-5, 0), new Vector2(5, 0), new List<Rect>(), Area);
        Assert.AreEqual(1, path.Count);
        Assert.AreEqual(new Vector2(5, 0), path[0]);
    }

    [Test]
    public void Wall_PathGoesAround()
    {
        var wall = new List<Rect> { new Rect(-1f, -6f, 2f, 12f) }; // стіна посередині
        var from = new Vector2(-5, 0);
        var to = new Vector2(5, 0);
        var path = BasePathfinder.FindPath(from, to, wall, Area);

        Assert.Greater(path.Count, 1, "шлях має обходити стіну");
        Assert.AreEqual(to, path[path.Count - 1]);
        // жоден відрізок шляху не перетинає стіну
        var prev = from;
        foreach (var p in path)
        {
            Assert.IsTrue(BasePathfinder.Clear(prev, p, wall), $"відрізок {prev} → {p} іде крізь стіну");
            prev = p;
        }
    }

    [Test]
    public void FarOutside_StillReachesTarget()
    {
        var wall = new List<Rect> { new Rect(-1f, -6f, 2f, 12f) };
        var to = new Vector2(100, 0); // далеко поза зоною пошуку
        var path = BasePathfinder.FindPath(new Vector2(-5, 0), to, wall, Area);
        Assert.AreEqual(to, path[path.Count - 1]);
    }
}
