using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace Believer.BelieverCode.Dev;

/// <summary>
/// Dev-only icon preview: loads the four character UI textures through the game ResourceLoader.
/// Marker: user://believer_character_icon_test.flag. Not a production screen.
/// </summary>
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.Preload))]
internal static class BelieverCharacterIconTest
{
    private const string MarkerPath = "user://believer_character_icon_test.flag";

    [HarmonyPostfix]
    private static void Postfix()
    {
        if (!Godot.FileAccess.FileExists(MarkerPath)) return;
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.CreateTimer(3.0).Timeout += () =>
        {
            try { Run(tree); }
            catch (Exception exception) { MainFile.Logger.Error($"[CharacterIconTest] {exception}"); }
        };
    }

    private static void Run(SceneTree tree)
    {
        var overlay = new Node2D { Name = "BelieverCharacterIconTestOverlay" };
        tree.Root.AddChild(overlay);
        overlay.AddChild(new ColorRect
        {
            Color = new Color(0.04f, 0.045f, 0.06f),
            Size = new Vector2(1920f, 1080f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });

        var icons = new[]
        {
            ("select 132x195", "res://Believer/images/charui/char_select_char_name.png", new Vector2(120, 100)),
            ("locked 132x195", "res://Believer/images/charui/char_select_char_name_locked.png", new Vector2(430, 100)),
            ("ui 128x128", "res://Believer/images/charui/character_icon_char_name.png", new Vector2(740, 100)),
            ("map 128x128", "res://Believer/images/charui/map_marker_char_name.png", new Vector2(1050, 100)),
        };
        foreach (var (labelText, path, position) in icons)
        {
            var texture = ResourceLoader.Load<Texture2D>(path);
            MainFile.Logger.Info($"[CharacterIconTest] {path} loaded={texture != null}");
            if (texture == null) continue;
            overlay.AddChild(new TextureRect
            {
                Texture = texture,
                Position = position,
                Size = texture.GetSize(),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspect,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
            var label = new Label
            {
                Text = labelText,
                Position = position + new Vector2(0, texture.GetSize().Y + 12),
            };
            label.AddThemeFontSizeOverride("font_size", 18);
            overlay.AddChild(label);
        }
    }
}
