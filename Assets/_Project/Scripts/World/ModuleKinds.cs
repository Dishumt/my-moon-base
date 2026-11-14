// AI-assisted: written with Claude. See docs/AI_USE.md

/// <summary>
/// Які модулі — кімнати (з дахом, шлюзом і людьми всередині),
/// а які — обладнання надворі (сонячна панель, батарея, антена...): у них не заходять.
/// </summary>
public static class ModuleKinds
{
    public static bool IsRoom(ModuleType type)
    {
        switch (type)
        {
            case ModuleType.Habitat:
            case ModuleType.Greenhouse:
            case ModuleType.Shelter:
            case ModuleType.Medbay:
            case ModuleType.Lab:
                return true;
            default:
                return false; // Solar, Battery, Antenna, Relay, Reactor
        }
    }
}
