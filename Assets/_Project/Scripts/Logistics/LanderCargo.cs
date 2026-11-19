// AI-assisted: written with Claude. See docs/AI_USE.md

/// <summary>
/// Ящики з корабля поставки біля майданчика: хто з роботів який ящик везе на базу (роль 04).
/// Тільки облік для картинки — ресурси гри вже додав SupplySystem, тут нічого не змінюється.
/// Звичайний C#-клас, тому його легко перевірити тестами. Малюють SupplyLanderView (ящики) і RobotView (робот із ящиком).
///
/// Ящик: Gone (немає) → OnGround (лежить) → Claimed (робот їде по нього) → Taken (робот везе його на базу).
/// Робот, якому дали справжню роботу, відпускає свій ящик (Release) — його потім забере інший.
/// </summary>
public class LanderCargo
{
    public enum CrateState { Gone, OnGround, Claimed, Taken }

    readonly CrateState[] state;
    readonly int[] owner;

    public LanderCargo(int count)
    {
        state = new CrateState[count];
        owner = new int[count];
    }

    public int Count => state.Length;

    /// <summary>Збільшується щоразу, коли ящики з'являються чи зникають усі разом. Робот так дізнається, що його ящика вже нема.</summary>
    public int Generation { get; private set; }

    public CrateState State(int crate) => Valid(crate) ? state[crate] : CrateState.Gone;

    /// <summary>Чи видно ящик біля майданчика (лежить або робот тільки їде по нього).</summary>
    public bool OnGround(int crate) => State(crate) == CrateState.OnGround || State(crate) == CrateState.Claimed;

    /// <summary>Скільки ящиків ще біля майданчика.</summary>
    public int Waiting
    {
        get { int n = 0; for (int i = 0; i < state.Length; i++) if (OnGround(i)) n++; return n; }
    }

    /// <summary>Корабель сів: усі ящики лежать біля нього (старі, яких не встигли забрати, теж тут).</summary>
    public void Unload() => SetAll(CrateState.OnGround);

    /// <summary>Нова гра: ящиків немає.</summary>
    public void Clear() => SetAll(CrateState.Gone);

    /// <summary>Робот бере собі ящик: той, що вже його, або перший вільний. -1 — вільних немає.</summary>
    public int Claim(int robot)
    {
        for (int i = 0; i < state.Length; i++)
            if (state[i] == CrateState.Claimed && owner[i] == robot) return i;
        for (int i = 0; i < state.Length; i++)
            if (state[i] == CrateState.OnGround) { state[i] = CrateState.Claimed; owner[i] = robot; return i; }
        return -1;
    }

    /// <summary>Чи цей ящик чекає саме на цього робота.</summary>
    public bool IsClaimedBy(int crate, int robot) => State(crate) == CrateState.Claimed && owner[crate] == robot;

    /// <summary>Робот передумав (дали роботу): ящик знову вільний.</summary>
    public void Release(int crate, int robot)
    {
        if (IsClaimedBy(crate, robot)) state[crate] = CrateState.OnGround;
    }

    /// <summary>Робот підняв ящик. false — ящик не його (або вже зник).</summary>
    public bool PickUp(int crate, int robot)
    {
        if (!IsClaimedBy(crate, robot)) return false;
        state[crate] = CrateState.Taken;
        return true;
    }

    bool Valid(int crate) => crate >= 0 && crate < state.Length;

    void SetAll(CrateState s)
    {
        for (int i = 0; i < state.Length; i++) { state[i] = s; owner[i] = -1; }
        Generation++;
    }
}
