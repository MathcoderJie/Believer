using BaseLib.Utils;
using Believer.BelieverCode.Cards.Signature;
using Believer.BelieverCode.Miracles;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Believer.BelieverCode.Cards.Dev;

/// <summary>
/// DEVELOPMENT-ONLY test Miracle, reused by M5 to verify the full collection/generation/use/cleanup
/// pipeline before the real 12 Miracles exist: collect it (dev console "miracle add"), enter
/// Omniscience -> it is generated into the hand -> only playable inside the form -> exhausts on play
/// -> leftovers are cleaned up when the form ends.
///
/// It lives in <see cref="BelieverSignatureCardPool"/>, which is neither a character pool nor a
/// shared pool, so it is never offered as a reward and never appears in the card library. It is NOT
/// one of the 12 official Miracles; M6 replaces it with the real Miracle cards.
/// </summary>
[Pool(typeof(BelieverSignatureCardPool))]
public sealed class DevMiracleTestCard() : MiracleCard(0, CardType.Attack, CardRarity.Token, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }
}
