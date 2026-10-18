using BaseLib.Extensions;
using Believer.BelieverCode.Cards;
using Believer.BelieverCode.Resources;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Believer.BelieverCode.Cards.Basic;

/// <summary>
/// 7/8 凝神 / Focus - 1 cost, gain 1 Divinity, then draw 1 card (2 Divinity upgraded).
///
/// The Divinity gain goes through M1's resource API (<see cref="BelieverDivinityResource.Gain"/>),
/// which clamps the value to 7. Gaining happens before the draw, as specified.
/// </summary>
public sealed class Focus() : BelieverCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Divinity", 1m),
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Owner.GetResource<BelieverDivinityResource>()?.Gain(DynamicVars["Divinity"].IntValue);

        await CardPileCmd.Draw(choiceContext, 1, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Divinity"].UpgradeValueBy(1m);
    }
}
