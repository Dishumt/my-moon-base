// AI-assisted: written with Claude. See docs/AI_USE.md
using NUnit.Framework;

/// <summary>Тести ящиків з корабля поставки: роботи розбирають їх по одному і не везуть той самий двічі (роль 04).</summary>
public class LanderCargoTests
{
    [Test]
    public void NewCargo_NothingOnGround()
    {
        var c = new LanderCargo(3);
        Assert.AreEqual(0, c.Waiting);
        Assert.AreEqual(-1, c.Claim(0), "до посадки везти нічого");
    }

    [Test]
    public void TwoRobots_TakeDifferentCrates()
    {
        var c = new LanderCargo(3);
        c.Unload();
        Assert.AreEqual(3, c.Waiting);

        int a = c.Claim(0), b = c.Claim(1);
        Assert.AreNotEqual(-1, a);
        Assert.AreNotEqual(-1, b);
        Assert.AreNotEqual(a, b, "двоє роботів не їдуть по один ящик");
        Assert.AreEqual(a, c.Claim(0), "робот пам'ятає свій ящик");
    }

    [Test]
    public void PickUp_OnlyOwnCrate_AndOnce()
    {
        var c = new LanderCargo(3);
        c.Unload();
        int a = c.Claim(0);

        Assert.IsFalse(c.PickUp(a, 1), "чужий ящик брати не можна");
        Assert.IsTrue(c.PickUp(a, 0));
        Assert.IsFalse(c.PickUp(a, 0), "той самий ящик двічі не везуть");
        Assert.AreEqual(2, c.Waiting);
    }

    [Test]
    public void AllCratesDelivered_ThenNothingToClaim()
    {
        var c = new LanderCargo(3);
        c.Unload();
        for (int i = 0; i < 3; i++) Assert.IsTrue(c.PickUp(c.Claim(0), 0));
        Assert.AreEqual(0, c.Waiting);
        Assert.AreEqual(-1, c.Claim(0));
    }

    [Test]
    public void Release_CrateGoesToAnotherRobot()
    {
        var c = new LanderCargo(1);
        c.Unload();
        int a = c.Claim(0);
        Assert.AreEqual(-1, c.Claim(1), "єдиний ящик уже зайнятий");

        c.Release(a, 0); // роботу 0 дали будувати модуль
        Assert.AreEqual(a, c.Claim(1));
        Assert.IsFalse(c.PickUp(a, 0), "робот 0 його вже відпустив");
    }

    [Test]
    public void NewLanding_BringsCratesBack_AndChangesGeneration()
    {
        var c = new LanderCargo(2);
        c.Unload();
        int gen = c.Generation;
        c.PickUp(c.Claim(0), 0);

        c.Unload(); // наступна поставка
        Assert.AreNotEqual(gen, c.Generation, "роботи бачать, що це вже нові ящики");
        Assert.AreEqual(2, c.Waiting);

        c.Clear(); // нова гра
        Assert.AreEqual(0, c.Waiting);
    }

    [Test]
    public void WrongIndex_IsSafe()
    {
        var c = new LanderCargo(2);
        c.Unload();
        Assert.IsFalse(c.PickUp(-1, 0));
        Assert.IsFalse(c.PickUp(5, 0));
        c.Release(7, 0);
        Assert.AreEqual(LanderCargo.CrateState.Gone, c.State(9));
    }
}
