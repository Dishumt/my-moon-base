// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>Звуки (SoundSynth) і тексти меню паузи та довідки. Без Unity-сцени.</summary>
public class AudioMenuTests
{
    [Test]
    public void EverySoundHasSamplesInRange()
    {
        foreach (Sfx sfx in Enum.GetValues(typeof(Sfx)))
        {
            float[] b = SoundSynth.Make(sfx);
            Assert.Greater(b.Length, 500, sfx + ": надто короткий");
            float peak = 0f;
            foreach (float v in b)
            {
                Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v), sfx + ": некоректне число");
                peak = Math.Max(peak, Math.Abs(v));
            }
            Assert.LessOrEqual(peak, 1f, sfx + ": гучніше за межі [-1; 1]");
            Assert.Greater(peak, 0.05f, sfx + ": майже не чутно");
        }
    }

    [Test]
    public void LoopingSoundsJoinWithoutAClick()
    {
        foreach (var sfx in new[] { Sfx.Ambient, Sfx.StormLoop })
        {
            float[] b = SoundSynth.Make(sfx);
            Assert.AreEqual((int)(SoundSynth.LoopSeconds * SoundSynth.Rate), b.Length, sfx + ": довжина кола");
            // кінець переходить у початок: різниця така сама мала, як між двома сусідніми відліками
            float join = Math.Abs(b[0] - b[b.Length - 1]);
            float maxStep = 0f;
            for (int i = 1; i < b.Length; i++) maxStep = Math.Max(maxStep, Math.Abs(b[i] - b[i - 1]));
            Assert.LessOrEqual(join, maxStep * 1.5f + 0.001f, sfx + ": на стику чутно клацання");
        }
    }

    [Test]
    public void SoundsAreTheSameEveryTime()
    {
        foreach (Sfx sfx in Enum.GetValues(typeof(Sfx)))
        {
            float[] a = SoundSynth.Make(sfx), b = SoundSynth.Make(sfx);
            CollectionAssert.AreEqual(a, b, sfx + ": звук щоразу різний");
        }
    }

    static readonly string[] MenuKeys =
    {
        "hud.help", "hud.menu", "hud.sound_on", "hud.sound_off",
        "pause.title", "pause.sub", "pause.sub_demo", "pause.resume", "pause.help", "pause.sound_on", "pause.sound_off",
        "pause.lang", "pause.restart", "pause.menu", "pause.sure",
        "help.title", "help.close", "help.l1", "help.l2", "help.l3", "help.l4", "help.l5", "help.l6", "help.l7",
        // вкладка MAP
        "mapv.title", "mapv.hint", "mapv.you", "mapv.base", "mapv.crew", "mapv.unknown", "mapv.dist", "mapv.storm",
        "mapv.poi.Ice", "mapv.poi.Sample", "mapv.poi.Fact", "mapv.poi.LandingPad",
    };

    [TestCase("en")]
    [TestCase("uk")]
    public void MenuTextsExistInBothLanguages(string language)
    {
        var table = DataLoader.Load<TextTable>("Text/texts_" + language);
        var keys = new HashSet<string>();
        foreach (var e in table.items)
        {
            Assert.IsNotEmpty(e.text, language + ": порожній текст " + e.key);
            keys.Add(e.key);
        }
        foreach (var key in MenuKeys) Assert.IsTrue(keys.Contains(key), language + ": немає ключа " + key);
    }
}
