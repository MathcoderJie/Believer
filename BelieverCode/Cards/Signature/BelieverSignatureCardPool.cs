namespace Believer.BelieverCode.Cards.Signature;

/// <summary>
/// Card pool for the Believer's non-collectible cards.
///
/// This pool holds cards that only exist as part of the character's mechanics - the
/// <see cref="OmniscienceCard"/> ability card and the temporary 神迹 / Miracle cards granted during
/// Omniscience. They must never be offered as card rewards, shop stock, random card generation or
/// card-library entries, which is exactly what this pool guarantees:
///
/// <list type="bullet">
/// <item>No <c>CharacterModel.CardPool</c> returns this type, so it is not a character pool.</item>
/// <item><c>IsShared</c> stays <c>false</c> (inherited from <c>CustomCardPoolModel</c>), so it is not
/// registered as a shared pool either.</item>
/// <item>A pool that is neither a character pool nor a shared pool never reaches
/// <c>ModelDb.AllCardPools</c> - the collection card rewards and shops roll from.</item>
/// </list>
///
/// The pool still has to exist because <c>CardModel.Pool</c> must resolve to a real pool, so
/// <see cref="BelieverSignatureCard.Pool"/> returns this pool directly instead of going through the
/// <c>ModelDb.AllCardPools</c> search. Visual configuration is inherited from
/// <see cref="Character.BelieverCardPool"/> so signature cards render exactly like normal Believer
/// cards; only the internal title is overridden to keep the pools distinct.
/// </summary>
public class BelieverSignatureCardPool : Character.BelieverCardPool
{
    public override string Title => "BelieverSignature";
}
