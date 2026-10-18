using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using Believer.BelieverCode.Cards;

namespace Believer.BelieverCode.Cards.Blue;

/// <summary>
/// 17/24 蓝卡 · 赦免 / Absolution - 1 cost: remove ALL removable debuffs from yourself, then gain
/// 8 Block (12 upgraded).
///
/// "Removable debuff" follows the base game's only available classification (same as M2 and 净化):
/// a VISIBLE power whose current type is <see cref="PowerType.Debuff"/>; removal goes through the
/// game's <c>PowerCmd.Remove</c> path so <c>AfterRemoved</c> still runs. Hidden bookkeeping powers and
/// buffs are never touched.
/// </summary>
public sealed class Absolution() : BelieverCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new BlockVar(8m, ValueProp.Move),
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var debuffs = Owner.Creature.Powers
            .Where(power => power != null && power.IsVisible && power.TypeForCurrentAmount == PowerType.Debuff)
            .OrderBy(power => power.Id.Entry, StringComparer.Ordinal)
            .ToList();

        foreach (var debuff in debuffs)
        {
            await PowerCmd.Remove(debuff);
        }

        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(4m);
    }
}
