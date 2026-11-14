// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>Перевіряє, що ціни модулів правильно читаються з modules.json.</summary>
public class BuildCostTableTests
{
    private const string SampleJson = @"{
        ""regolith"": { ""type"": ""RegolithShield"", ""buildRobotHours"": 12, ""materials"": 0 },
        ""items"": [
            { ""type"": ""Solar"",      ""buildRobotHours"": 6,  ""materials"": 10 },
            { ""type"": ""Greenhouse"", ""buildRobotHours"": 18, ""materials"": 15 }
        ]
    }";

    [Test]
    public void FromJson_ReadsCostsAndRegolith()
    {
        var table = BuildCostTable.FromJson(SampleJson);

        BuildCost solar;
        Assert.IsTrue(table.TryGet(ModuleType.Solar, out solar));
        Assert.AreEqual(6f, solar.BuildHours, 0.001f);
        Assert.AreEqual(10f, solar.Materials, 0.001f);

        BuildCost greenhouse;
        Assert.IsTrue(table.TryGet(ModuleType.Greenhouse, out greenhouse));
        Assert.AreEqual(18f, greenhouse.BuildHours, 0.001f);
        Assert.AreEqual(15f, greenhouse.Materials, 0.001f);

        Assert.AreEqual(12f, table.Regolith.BuildHours, 0.001f);
        Assert.AreEqual(0f, table.Regolith.Materials, 0.001f);
    }

    [Test]
    public void FromJson_UnknownTypeName_IsReportedAndSkipped()
    {
        // Саме так виглядала помилка "SolarPanel" замість "Solar".
        LogAssert.Expect(LogType.Error, new Regex("SolarPanel"));
        var table = BuildCostTable.FromJson(
            @"{ ""items"": [ { ""type"": ""SolarPanel"", ""buildRobotHours"": 6, ""materials"": 10 } ] }");

        BuildCost cost;
        Assert.IsFalse(table.TryGet(ModuleType.Solar, out cost));
    }

    [Test]
    public void RealModulesJson_HasAllEightModules()
    {
        // Читає справжній файл Resources/Data/Balance/modules.json.
        var table = BuildCostTable.Load();

        ModuleType[] expected =
        {
            ModuleType.Habitat, ModuleType.Solar, ModuleType.Battery, ModuleType.Greenhouse,
            ModuleType.Shelter, ModuleType.Medbay, ModuleType.Antenna, ModuleType.Lab
        };

        foreach (ModuleType type in expected)
        {
            BuildCost cost;
            Assert.IsTrue(table.TryGet(type, out cost), "У modules.json немає ціни для " + type);
            Assert.Greater(cost.BuildHours, 0f, type + ": buildRobotHours має бути більше 0");
        }
    }
}
