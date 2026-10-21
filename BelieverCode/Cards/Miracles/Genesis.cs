using BaseLib.Utils;
using Believer.BelieverCode.Cards.Signature;
using Believer.BelieverCode.Mechanics;
using Believer.BelieverCode.Miracles;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Believer.BelieverCode.Cards.Miracles;

/// <summary>
/// 2/12 再创世 / Genesis - extends the current Omniscience form by one player turn.
///
/// Only asks the form to extend once; the form itself (OmnisciencePower) enforces "max one extension
/// per form", keeps damage immunity / 0-cost cards / miracle usability alive, and retains the
/// unplayed miracles for the extra turn. No re-draw, no second debuff cleanse, no re-generation.
/// </summary>
public sealed class Genesis() : MiracleCard(3, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner == null) return Task.CompletedTask;

        if (!Omniscience.RequestFormExtension(Owner))
        {
            MainFile.Logger.Warn("[Miracles] Genesis: this Omniscience form was already extended; no effect.");
        }

        return Task.CompletedTask;
    }
}
