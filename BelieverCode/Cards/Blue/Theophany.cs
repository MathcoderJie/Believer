using BaseLib.Extensions;
using Believer.BelieverCode.Cards;
using Believer.BelieverCode.Resources;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Believer.BelieverCode.Cards.Blue;

/// <summary>
/// 21/24 蓝卡 · 神临 / Theophany - 2 cost: gain 3 Divinity, 15 Block, draw 1 card
/// (4 Divinity / 18 Block upgraded; draw unchanged).
/// </summary>
public sealed class Theophany() : BelieverCard(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Divinity", 3m),
        new BlockVar(15m, ValueProp.Move),
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Owner.GetResource<BelieverDivinityResource>()?.Gain(DynamicVars["Divinity"].IntValue);

        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await CardPileCmd.Draw(choiceContext, 1, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Divinity"].UpgradeValueBy(1m);
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}
