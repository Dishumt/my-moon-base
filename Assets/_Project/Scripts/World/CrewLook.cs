// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Налаштування вигляду екіпажу й роботів: картинки кожної спеціальності, іконки над головою, швидкості, де чекати.
/// Один такий компонент на сцену (на об'єкті World, поруч з ModuleLook).
/// Картинки й іконки заповнює меню Moon → Налаштувати екіпаж (спрайти й іконки).
/// </summary>
public class CrewLook : MonoBehaviour
{
    public static CrewLook Instance { get; private set; }

    [Serializable]
    public class RoleLook
    {
        public CrewRole role;
        public EngineerController.SpriteSet suit = new EngineerController.SpriteSet();   // у скафандрі
        public EngineerController.SpriteSet indoor = new EngineerController.SpriteSet(); // без скафандра
    }

    [Header("Картинки спеціальностей")]
    public List<RoleLook> roles = new List<RoleLook>();

    [Header("Іконки над головою (видно тільки, коли щось не так)")]
    public Sprite iconHungry;      // голодний
    public Sprite iconSick;        // хворий
    public Sprite iconSevere;      // тяжкий стан
    public Sprite iconDoseYellow;  // багато радіації: тільки завдання всередині
    public Sprite iconDoseRed;     // ліміт радіації: летить додому
    [Tooltip("Показувати «голодний», коли ситість нижча за це число (0..100)")]
    public float hungryBelow = 30f;

    [Header("Рух")]
    public float crewSpeed = 3.2f;       // м/с надворі
    public float crewIndoorSpeed = 2.2f; // м/с у модулі й шлюзі
    public float robotSpeed = 2.5f;

    [Header("Де чекати без завдання")]
    [Tooltip("Екіпаж без завдання і без житлового модуля стоїть тут (центр бази — перехрестя проходів)")]
    public Vector3 crewIdleSpot = new Vector3(0f, 0f, 0f);
    [Tooltip("Роботи без роботи стоять тут (перед базою — «ангар»)")]
    public Vector3 robotHome = new Vector3(0f, 0f, -27f);

    public RoleLook Find(CrewRole role)
    {
        foreach (var r in roles) if (r.role == role) return r;
        return null;
    }

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
