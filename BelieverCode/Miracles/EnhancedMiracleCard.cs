using Believer.BelieverCode.Cards.Signature;
using Believer.BelieverCode.Keywords;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Believer.BelieverCode.Miracles;

/// <summary>
/// Temporary enhanced Miracle used only by Omniscience. It is deliberately not a
/// <see cref="MiracleCard"/> subtype, so it never enters the normal rare pool, the Miracle catalog,
/// or the permanent deck. Its base effect is delegated to a temporary combat instance of the
/// corresponding normal Miracle, keeping base and enhanced definitions separate.
///
/// The concrete enhanced numbers are intentionally not invented yet; the class/description/effect
/// entry points are separate and can receive tuned values later without touching the base cards.
/// </summary>
public abstract class EnhancedMiracleCard(int cost, CardType type, TargetType target)
    : BelieverSignatureCard(cost, type, CardRarity.Token, target)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [BelieverKeywords.Miracle, BelieverKeywords.EnhancedMiracle];

    protected abstract MiracleCard CreateBaseCard();

    protected override bool IsPlayable
    {
        get
        {
            if (!IsMutable) return false;
            var owner = Owner;
            return owner?.Creature?.CombatState != null
                && owner.Creature.HasPower<Powers.OmnisciencePower>();
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var baseCard = CreateBaseCard();
        await baseCard.ExecuteBaseEffect(choiceContext, cardPlay);
    }
}
