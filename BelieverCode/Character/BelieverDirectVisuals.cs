using System;
using System.IO;
using BaseLib.Utils.NodeFactories;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace Believer.BelieverCode.Character;

/// <summary>
/// Creates an NCreatureVisuals directly from a single transparent Believer PNG via BaseLib's
/// NodeFactory&lt;NCreatureVisuals&gt; texture conversion. No Spine skeleton or native
/// Ironclad/Silent resource is modified.
/// </summary>
internal static class BelieverDirectVisuals
{
    private const string EmbeddedPng = "Believer.BelieverCode.Character.believer_direct.png";
    private const string EmbeddedShadow = "Believer.BelieverCode.Character.believer_shadow.png";

    private static ImageTexture? _cachedTexture;
    private static ImageTexture? _cachedShadow;

    public static Texture2D? LoadTexture()
    {
        if (_cachedTexture != null)
        {
            return _cachedTexture;
        }
        using var stream = typeof(BelieverDirectVisuals).Assembly.GetManifestResourceStream(EmbeddedPng);
        if (stream == null)
        {
            MainFile.Logger.Error($"[BelieverDirect] Missing embedded PNG: {EmbeddedPng}");
            return null;
        }
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var image = new Image();
        var error = image.LoadPngFromBuffer(memory.ToArray());
        if (error != Error.Ok)
        {
            MainFile.Logger.Error($"[BelieverDirect] PNG decode failed: {error}");
            return null;
        }
        _cachedTexture = ImageTexture.CreateFromImage(image);
        MainFile.Logger.Info($"[BelieverDirect] loaded PNG {image.GetWidth()}x{image.GetHeight()}");
        return _cachedTexture;
    }

    private static ImageTexture? LoadShadow()
    {
        if (_cachedShadow != null)
        {
            return _cachedShadow;
        }
        using var stream = typeof(BelieverDirectVisuals).Assembly.GetManifestResourceStream(EmbeddedShadow);
        if (stream == null)
        {
            MainFile.Logger.Warn($"[BelieverDirect] Missing embedded shadow: {EmbeddedShadow}");
            return null;
        }
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var image = new Image();
        var error = image.LoadPngFromBuffer(memory.ToArray());
        if (error != Error.Ok)
        {
            MainFile.Logger.Warn($"[BelieverDirect] Shadow decode failed: {error}");
            return null;
        }
        _cachedShadow = ImageTexture.CreateFromImage(image);
        return _cachedShadow;
    }

    private static void AddGroundShadow(NCreatureVisuals visuals)
    {
        var shadowTexture = LoadShadow();
        if (shadowTexture == null)
        {
            return;
        }
        var shadow = new Sprite2D
        {
            Name = "BelieverGroundShadow",
            Texture = shadowTexture,
            Position = new Vector2(0f, -1f),
            Scale = new Vector2(0.78f, 0.42f),
            ZIndex = 0,
        };
        visuals.AddChild(shadow);
        visuals.MoveChild(shadow, 0);
        MainFile.Logger.Info("[BelieverDirect] ground shadow added");
    }

    public static NCreatureVisuals? Create()
    {
        var texture = LoadTexture();
        if (texture == null)
        {
            return null;
        }
        try
        {
            var visuals = NodeFactory<NCreatureVisuals>.CreateFromResource(texture);
            AddGroundShadow(visuals);
            visuals.AddChild(new BelieverDirectAnimator { Name = "BelieverDirectAnimator" });
            // Reserved future layer for divinity orbs / rings / sigil; ordinary idle does not use it.
            visuals.AddChild(new Node2D { Name = "BelieverEffectLayer", ZIndex = 2 });
            MainFile.Logger.Info($"[BelieverDirect] NCreatureVisuals created: {visuals.GetClass()}");
            return visuals;
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"[BelieverDirect] NCreatureVisuals creation failed: {exception}");
            return null;
        }
    }
}
