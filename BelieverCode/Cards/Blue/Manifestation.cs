using Believer.BelieverCode.Cards;
using Believer.BelieverCode.Powers.Blue;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Believer.BelieverCode.Cards.Blue;

/// <summary>
/// 23/24 蓝卡 · 显圣 / Manifestation - 2 cost power: whenever you enter the Omniscience form, draw 2
/// extra cards (3 upgraded).
///
/// The Omniscience activation reads this power's amount when it draws its cards; extending the form
/// with 再创世 / Genesis never re-applies the power or re-runs the activation, so Manifestation cannot
/// trigger twice from an extension.
/// </summary>
public sealed class Manifestation() : BelieverCard(2, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new CardsVar(2),
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<ManifestationPower>(
            choiceContext, Owner.Creature, DynamicVars.Cards.BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
