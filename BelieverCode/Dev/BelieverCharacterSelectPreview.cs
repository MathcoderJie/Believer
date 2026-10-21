using System;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace Believer.BelieverCode.Dev;

/// <summary>
/// Dev-only preview of the real character-select screen with the Believer selected.
/// Marker: user://believer_charselect_preview.flag. Does not alter gameplay or saves.
/// </summary>
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.Preload))]
internal static class BelieverCharacterSelectPreview
{
    private const string MarkerPath = "user://believer_charselect_preview.flag";

    [HarmonyPostfix]
    private static void Postfix()
    {
        if (!Godot.FileAccess.FileExists(MarkerPath)) return;
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.CreateTimer(3.0).Timeout += () =>
        {
            try { Run(); }
            catch (Exception exception) { MainFile.Logger.Error($"[CharSelectPreview] failed: {exception}"); }
        };
    }

    private static void Run()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var screen = NCharacterSelectScreen.Create();
        if (screen == null)
        {
            MainFile.Logger.Error("[CharSelectPreview] screen create returned null");
            return;
        }
        tree.Root.AddChild(screen);
        screen.InitializeSingleplayer();
        MainFile.Logger.Info("[CharSelectPreview] screen added and initialized");

        var believer = ModelDb.Character<global::Believer.BelieverCode.Character.Believer>();
        if (believer == null)
        {
            MainFile.Logger.Error("[CharSelectPreview] Believer character not found");
            return;
        }

        var buttons = Descendants(screen).OfType<NCharacterSelectButton>().ToList();
        MainFile.Logger.Info($"[CharSelectPreview] buttons={buttons.Count}: {string.Join(", ", buttons.Select(b => b.Name.ToString()))}");
        var button = buttons.FirstOrDefault(candidate =>
            candidate.Name.ToString().StartsWith("Believer", StringComparison.OrdinalIgnoreCase));
        if (button == null)
        {
            MainFile.Logger.Error("[CharSelectPreview] Believer button not found");
            return;
        }
        screen.SelectCharacter(button, believer);
        MainFile.Logger.Info($"[CharSelectPreview] selected {believer.Id.Entry} bg={believer.CharacterSelectBg}");
        foreach (var iconPath in new[]
                 {
                     believer.CustomCharacterSelectIconPath,
                     believer.CustomCharacterSelectLockedIconPath,
                     believer.CustomIconTexturePath,
                     believer.CustomMapMarkerPath,
                 })
        {
            MainFile.Logger.Info($"[CharSelectPreview] asset exists={ResourceLoader.Exists(iconPath)} path={iconPath}");
        }
    }

    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
