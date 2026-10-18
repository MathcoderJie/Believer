using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Believer.BelieverCode.Powers.Miracles;

/// <summary>
/// Internal tracker for 五饼二鱼 / Five Loaves and Two Fish (M6). Keeps the temporary copies created by
/// that Miracle and removes them from every combat pile when the owner's turn ends, so the copies
/// "disappear at end of turn" no matter where they ended up (hand, draw, discard or exhaust).
///
/// Hidden power: it exists only to own the cleanup, it is not meant to be shown in the power bar.
/// </summary>
public sealed class LoavesAndFishesPower : BelieverPower
{
    private readonly List<CardModel> _copies = new();

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => false;

    /// <summary>Registers a temporary copy so it can be removed at the end of the turn.</summary>
    public void Track(CardModel copy)
    {
        if (!_copies.Contains(copy)) _copies.Add(copy);
    }

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player || !participants.Contains(Owner)) return;

        await RemoveCopies();
        await PowerCmd.Remove(this);
    }

    /// <summary>Safety net for removals that do not go through the turn-end path.</summary>
    public override async Task AfterRemoved(Creature oldOwner)
    {
        await RemoveCopies();
    }

    private async Task RemoveCopies()
    {
        if (_copies.Count == 0) return;

        var player = Owner?.Player;
        if (player == null)
        {
            _copies.Clear();
            return;
        }

        foreach (var copy in _copies.ToList())
        {
            if (copy.Pile == null) continue;
            if (copy.Pile.Type == PileType.Play) continue;

            await CardPileCmd.RemoveFromCombat(copy);
        }

        _copies.Clear();
    }
}
