// AI-assisted: written with Claude. See docs/AI_USE.md
// Усі "переліки" (enum) гри в одному місці. Enum — це список можливих значень,
// наприклад здоров'я може бути тільки Ok, Sick або Severe.
// Додаєш нове значення — додавай В КІНЕЦЬ списку, бо збереження гри пам'ятає їх як числа.

/// <summary>Спеціальність астронавта.</summary>
public enum CrewRole { Engineer, Medic, Botanist, Geologist }

/// <summary>Здоров'я: здоровий, хворий (працює повільніше), тяжкий стан (лежить у медмодулі).</summary>
public enum HealthStatus { Ok, Sick, Severe }

/// <summary>Чим займається астронавт. None — нічого не робить.</summary>
public enum CrewTask { None, Rest, MineIce, CollectSamples, Greenhouse, Lab, Heal, Repair, Medbay, Shelter }

/// <summary>Типи точок на поверхні Місяця.</summary>
public enum PoiType { Ice, Sample, Fact, LandingPad }

/// <summary>Типи модулів бази. Relay і Reactor — для розділів 4–5.</summary>
public enum ModuleType { Habitat, Solar, Battery, Greenhouse, Shelter, Medbay, Antenna, Lab, Relay, Reactor }

/// <summary>Що робить робот.</summary>
public enum RobotTask { Idle, Build, Shield }
