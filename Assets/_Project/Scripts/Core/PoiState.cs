// AI-assisted: written with Claude. See docs/AI_USE.md
using System;

/// <summary>Точка на поверхні Місяця (POI = point of interest, «цікаве місце»).</summary>
[Serializable]
public class PoiState
{
    public string Id;                // такий самий Id стоїть у PoiMarker у сцені
    public PoiType Type;
    public bool Discovered;          // пише Світ (01): інженер підійшов до точки
    public float Amount;             // скільки лишилось (льоду, зразків)
}
