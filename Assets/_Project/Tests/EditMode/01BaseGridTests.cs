// AI-assisted: written with Claude. See docs/AI_USE.md
using NUnit.Framework;
using UnityEngine;

/// <summary>Тести сітки бази (роль 01).</summary>
public class BaseGridTests
{
    [Test]
    public void EverySlot_RoundTripsThroughWorld()
    {
        for (int id = 0; id < BaseGrid.Count; id++)
            Assert.AreEqual(id, BaseGrid.FromWorld(BaseGrid.ToWorld(id)), "квадратик " + id);
    }

    [Test]
    public void Slot0_IsLeftAndNearCamera()
    {
        Vector3 p = BaseGrid.ToWorld(0);
        Assert.Less(p.x, 0f);
        Assert.Less(p.z, 0f);
    }

    [Test]
    public void FarAway_IsOutside()
    {
        Assert.AreEqual(-1, BaseGrid.FromWorld(new Vector3(1000f, 0f, 0f)));
        Assert.AreEqual(-1, BaseGrid.ToSlot(-1, 0));
        Assert.AreEqual(-1, BaseGrid.ToSlot(0, BaseGrid.Depth));
    }

    [Test]
    public void CanPlace_FreeTakenOutside()
    {
        var s = new GameState();
        Assert.IsNull(BaseGrid.CanPlace(s, 3));

        s.Modules.Add(new ModuleState { Type = ModuleType.Habitat, SlotId = 3 });
        Assert.AreEqual("error.place_taken", BaseGrid.CanPlace(s, 3));
        Assert.AreEqual("error.place_outside", BaseGrid.CanPlace(s, BaseGrid.Count));
    }
}
