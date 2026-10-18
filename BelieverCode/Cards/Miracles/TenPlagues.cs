using BaseLib.Utils;
using Believer.BelieverCode.Cards.Signature;
using Believer.BelieverCode.Miracles;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Believer.BelieverCode.Cards.Miracles;

/// <summary>
/// 5/12 十灾降世 / Ten Plagues - 10 independent random hits of 6 damage.
///
/// The base game's random targeting picks its target per hit inside the attack's hit loop (it is not
/// chosen once for the whole attack), so <c>WithHitCount(10)</c> + <c>TargetingRandomOpponents</c>
/// gives ten independently resolved attacks.
/// </summary>
[Pool(typeof(BelieverSignatureCardPool))]
public sealed class TenPlagues() : MiracleCard(0, CardType.Skill, CardRarity.Token, TargetType.Self)
{
    private const int HitCount = 10;
    private const decimal DamagePerHit = 6m;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var combatState = CombatState;
        if (combatState == null) return;

        await DamageCmd.Attack(DamagePerHit)
            .WithHitCount(HitCount)
            .FromCard(this)
            .TargetingRandomOpponents(combatState)
            .Execute(choiceContext);
    }
}
