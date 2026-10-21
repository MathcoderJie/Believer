using System;
using System.Linq;
using BaseLib.Abstracts;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using Believer.BelieverCode.Cards;
using Believer.BelieverCode.Cards.Dev;
using Believer.BelieverCode.Cards.Signature;
using Believer.BelieverCode.Miracles;

namespace Believer.BelieverCode.Dev;

/// <summary>
/// Dev-only card-art gallery: renders the real 52 normal/Miracle card models with NCard so the
/// actual imported card art can be inspected in the game client. Marker:
/// user://believer_cardart_gallery.flag. It is not a production UI.
/// </summary>
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.Preload))]
internal static class BelieverCardArtGallery
{
    private const string MarkerPath = "user://believer_cardart_gallery.flag";
    private const string ExtraMarkerPath = "user://believer_missing_art_gallery.flag";

    [HarmonyPostfix]
    private static void Postfix()
    {
        var extra = Godot.FileAccess.FileExists(ExtraMarkerPath);
        if (!extra && !Godot.FileAccess.FileExists(MarkerPath)) return;
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.CreateTimer(3.0).Timeout += () =>
        {
            try { Run(extra); }
            catch (Exception exception) { MainFile.Logger.Error($"[CardArtGallery] failed: {exception}"); }
        };
    }

    private static void Run(bool extra)
    {
        var cards = extra
            ? ModelDb.CardPool<BelieverSignatureCardPool>().AllCards
                .Where(card => card is EnhancedMiracleCard || card is OmniscienceCard)
                .OrderBy(card => card is OmniscienceCard ? 0 : 1)
                .ThenBy(card => card.Id.Entry, StringComparer.Ordinal)
                .ToList()
            : ModelDb.AllCards
                .Where(card => card.Id.Entry.StartsWith("BELIEVER-", StringComparison.Ordinal))
                .Where(card => !card.Id.Entry.StartsWith("BELIEVER-ENHANCED_", StringComparison.Ordinal))
                .Where(card => card is not DevMiracleTestCard && card is not OmniscienceCard)
                .OrderBy(card => card is MiracleCard ? 1 : 0)
                .ThenBy(card => card.Id.Entry, StringComparer.Ordinal)
                .Take(52)
                .ToList();

        MainFile.Logger.Info($"[CardArtGallery] extra={extra} cards={cards.Count}");
        var tree = (SceneTree)Engine.GetMainLoop();
        var overlayName = extra ? "BelieverMissingArtGalleryOverlay" : "BelieverCardArtGalleryOverlay";
        if (tree.Root.GetNodeOrNull(overlayName) != null) return;
        var overlay = new Node2D { Name = overlayName };
        tree.Root.AddChild(overlay);

        var background = new ColorRect
        {
            Name = "CardArtGalleryBackground",
            Color = new Color(0.04f, 0.045f, 0.06f),
            Size = new Vector2(1920f, 1080f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        overlay.AddChild(background);

        var missing = 0;
        for (var i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            var col = extra ? i % 4 : i % 10;
            var row = extra ? i / 4 : i / 10;
            var holder = new Node2D
            {
                Name = $"Card_{i}",
                Position = extra
                    ? new Vector2(145 + col * 350, 55 + row * 250)
                    : new Vector2(55 + col * 103, 45 + row * 125),
            };
            overlay.AddChild(holder);

            var bigPath = card is CustomCardModel customCard ? customCard.CustomPortraitPath : card.PortraitPath;
            var smallPath = card.PortraitPath;
            var hasBig = ResourceLoader.Exists(bigPath);
            var hasSmall = ResourceLoader.Exists(smallPath);
            if (card is OmniscienceCard)
            {
                MainFile.Logger.Info($"[CardArtGallery] Omniscience title='{card.Title}'");
            }
            if (!hasBig || !hasSmall) missing++;

            try
            {
                var cardNode = NCard.Create(card);
                if (cardNode != null)
                {
                    cardNode.Scale = extra ? new Vector2(0.38f, 0.38f) : new Vector2(0.20f, 0.20f);
                    holder.AddChild(cardNode);
                    // Force a full reload after the node is inside the tree. NCard.Create may
                    // return a pooled node whose first Reload happened before its labels existed.
                    cardNode.Model = null;
                    cardNode.Model = card;
                    cardNode.UpdateVisuals(PileType.Hand, CardPreviewMode.Normal);
                }
            }
            catch (Exception exception)
            {
                MainFile.Logger.Warn($"[CardArtGallery] NCard failed for {card.Id.Entry}: {exception.Message}");
            }

            MainFile.Logger.Info(
                $"[CardArtGallery] {card.Id.Entry} big={hasBig} small={hasSmall} " +
                $"bigPath={bigPath}");
        }

        MainFile.Logger.Info($"[CardArtGallery] extra={extra} missing-art cards={missing}");
    }
}
