using BaseLib.Abstracts;
using BaseLib.Utils.NodeFactories;
using Believer.BelieverCode.Cards.Basic;
using Believer.BelieverCode.Extensions;
using Believer.BelieverCode.Relics;
using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace Believer.BelieverCode.Character;

public class Believer : PlaceholderCharacterModel
{
    public const string CharacterId = "Believer";

    /// <summary>
    /// Direct-texture mode: display the Believer's single transparent PNG through BaseLib's
    /// NCreatureVisuals texture conversion (Sprite2D), not the Ironclad Spine visual.
    /// Returns null only if the embedded PNG cannot be loaded, allowing the placeholder fallback.
    /// </summary>
    public override NCreatureVisuals? CreateCustomVisuals()
    {
        return BelieverDirectVisuals.Create();
    }
    
    public static readonly Color Color = new("ffffff");

    public override Color NameColor => Color;
    public override CharacterGender Gender => CharacterGender.Neutral;
    public override int StartingHp => 70;
    
    /// <summary>
    /// 初始卡组 / starting deck - exactly 10 cards: 5 打击, 4 防御, 1 凝神.
    ///
    /// The 全知全能 / Omniscience ability card is deliberately NOT part of the deck (the deck must be
    /// exactly the 10 basic cards). It is granted into the player's hand when a combat starts - see
    /// <c>Mechanics.OmniscienceAvailability</c> - so the core mechanic stays usable without counting
    /// against the starting deck.
    /// </summary>
    public override IEnumerable<CardModel> StartingDeck => [
        ModelDb.Card<StrikeBeliever>(),
        ModelDb.Card<StrikeBeliever>(),
        ModelDb.Card<StrikeBeliever>(),
        ModelDb.Card<StrikeBeliever>(),
        ModelDb.Card<StrikeBeliever>(),
        ModelDb.Card<DefendBeliever>(),
        ModelDb.Card<DefendBeliever>(),
        ModelDb.Card<DefendBeliever>(),
        ModelDb.Card<DefendBeliever>(),
        ModelDb.Card<Focus>()
    ];

    public override IReadOnlyList<RelicModel> StartingRelics =>
    [
        // 残缺圣印 / Incomplete Seal: gain 1 Divinity at the start of each of the owner's turns.
        ModelDb.Relic<IncompleteSeal>()
    ];
    
    public override CardPoolModel CardPool => ModelDb.CardPool<BelieverCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<BelieverRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<BelieverPotionPool>();
    
    /*  PlaceholderCharacterModel will utilize placeholder basegame assets for most of your character assets until you
        override all the other methods that define those assets. 
        These are just some of the simplest assets, given some placeholders to differentiate your character with. 
        You don't have to, but you're suggested to rename these images. */
    public override Control CustomIcon
    {
        get
        {
            var icon = NodeFactory<Control>.CreateFromResource(CustomIconTexturePath);
            icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            return icon;
        }
    }
    public override string CustomIconTexturePath => "character_icon_char_name.png".CharacterUiPath();
    public override string CustomCharacterSelectIconPath => "char_select_char_name.png".CharacterUiPath();
    public override string CustomCharacterSelectLockedIconPath => "char_select_char_name_locked.png".CharacterUiPath();
    public override string CustomMapMarkerPath => "map_marker_char_name.png".CharacterUiPath();

    /// <summary>
    /// Custom character-select background scene. The generated wide portrait is the background;
    /// the vanilla InfoPanel remains on the right and the image deliberately leaves dark space there.
    /// </summary>
    public override string CustomCharacterSelectBg => "res://Believer/scenes/char_select_bg_believer.tscn";
}