using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Players;

namespace Believer.BelieverCode.Mechanics;

/// <summary>
/// Tracks how many times a player has GAINED Divinity during the current player turn.
///
/// Used by cards/powers that react to Divinity being gained (追击 / 信标 / 施予, and later 圣约):
/// the amount itself lives in M1's <see cref="Resources.BelieverDivinityResource"/> and is never
/// duplicated here - this class only remembers the per-turn gain count and notifies listeners.
///
/// How gains are observed: a Harmony postfix on <c>BelieverDivinityResource.Gain</c> reports every
/// actual increase (the M1 method is not modified; the patch only reads the before/after amount, so a
/// gain that is clamped by the 7 point cap does not count). The relic and every card gain through that
/// same M1 API, so all sources are covered.
///
/// The count is per <see cref="PlayerCombatState"/> and keyed by its <c>TurnNumber</c>, so it resets
/// automatically at the start of each player turn and disappears with the combat. Nested gains (a
/// listener that grants more Divinity while handling a gain) are still counted but do not notify
/// listeners again, which is what prevents effects like 圣约 from looping forever.
/// </summary>
public static class DivinityGainTracker
{
    private sealed class TurnState
    {
        public int TurnNumber = -1;
        public int GainCount;

        /// <summary>Listeners are per combat state, so they are collected together with the combat.</summary>
        public Action<Player, int, int>? Gained;
    }

    private static readonly ConditionalWeakTable<PlayerCombatState, TurnState> States = new();

    private static bool _dispatching;

    /// <summary>Number of times the player has gained Divinity during their current turn.</summary>
    public static int GainCountThisTurn(Player? player)
    {
        var playerCombatState = player?.PlayerCombatState;
        if (playerCombatState == null) return 0;

        var state = GetState(playerCombatState);
        return state.TurnNumber == playerCombatState.TurnNumber ? state.GainCount : 0;
    }

    /// <summary>True when the player has gained any Divinity during their current turn.</summary>
    public static bool HasGainedThisTurn(Player? player) => GainCountThisTurn(player) > 0;

    /// <summary>
    /// Subscribes to all Divinity gains of the given combat state. The handler receives
    /// (player, amountGained, gainCountThisTurn).
    /// </summary>
    public static void AddListener(PlayerCombatState playerCombatState, Action<Player, int, int> handler)
        => GetState(playerCombatState).Gained += handler;

    /// <summary>Removes a listener added through <see cref="AddListener"/>.</summary>
    public static void RemoveListener(PlayerCombatState playerCombatState, Action<Player, int, int> handler)
        => GetState(playerCombatState).Gained -= handler;

    /// <summary>Called by the observation patch after a real (not cap-clamped) Divinity increase.</summary>
    internal static void RecordGain(Player player, int amount)
    {
        if (amount <= 0) return;

        var playerCombatState = player.PlayerCombatState;
        if (playerCombatState == null) return;

        var state = GetState(playerCombatState);
        if (state.TurnNumber != playerCombatState.TurnNumber)
        {
            state.TurnNumber = playerCombatState.TurnNumber;
            state.GainCount = 0;
        }

        state.GainCount++;

        if (_dispatching) return;

        _dispatching = true;
        try
        {
            state.Gained?.Invoke(player, amount, state.GainCount);
        }
        finally
        {
            _dispatching = false;
        }
    }

    private static TurnState GetState(PlayerCombatState playerCombatState)
        => States.GetValue(playerCombatState, _ => new TurnState());
}
