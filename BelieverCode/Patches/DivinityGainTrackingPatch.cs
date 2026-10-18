using Believer.BelieverCode.Mechanics;
using Believer.BelieverCode.Resources;
using HarmonyLib;

namespace Believer.BelieverCode.Patches;

/// <summary>
/// Observation-only patch for M1's Divinity resource.
///
/// It does NOT change how Divinity works: it records the amount before/after a
/// <c>BelieverDivinityResource.Gain</c> call and reports a real increase (a gain clamped by the 7
/// point cap is not an increase, so it does not count as "gained") to
/// <see cref="DivinityGainTracker"/>. All Divinity gains - cards, the M3 relic, future effects - go
/// through this single M1 API, which is why the tracker can stay accurate without touching M1 code.
/// </summary>
[HarmonyPatch(typeof(BelieverDivinityResource), nameof(BelieverDivinityResource.Gain))]
internal static class DivinityGainTrackingPatch
{
    [HarmonyPrefix]
    private static void Prefix(BelieverDivinityResource __instance, ref int __state)
    {
        __state = __instance.Amount;
    }

    [HarmonyPostfix]
    private static void Postfix(BelieverDivinityResource __instance, int __state)
    {
        var gained = __instance.Amount - __state;
        if (gained <= 0) return;

        var player = __instance.Owner;
        if (player == null) return;

        DivinityGainTracker.RecordGain(player, gained);
    }
}
