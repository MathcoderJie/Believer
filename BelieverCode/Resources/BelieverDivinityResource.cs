using BaseLib.Abstracts;
using BaseLib.BaseLibScenes;
using BaseLib.Patches.UI;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;

namespace Believer.BelieverCode.Resources;

/// <summary>
/// 「神性 / Divinity」— the Believer's combat-scoped resource.
///
/// Max 7, starts every combat at 0, and is never written to the save file: BaseLib keeps one
/// instance per <see cref="PlayerCombatState"/> (see <see cref="CustomResources{T}"/>), so the
/// instance and its value live and die with the combat.
///
/// This type needs no manual registration - BaseLib's PostModInitPatch discovers every
/// non-abstract <see cref="CustomResource"/> in the mod assembly and registers it.
/// </summary>
public class BelieverDivinityResource : CustomResource
{
    /// <summary>Unique resource id. BaseLib also derives this resource's localization keys from it.</summary>
    public const string ResourceId = "BELIEVER-DIVINITY";

    /// <summary>Divinity can never exceed this value.</summary>
    public const int MaxAmount = 7;

    /// <summary>Gap in pixels between the counter's bottom edge and the top of the energy counter.</summary>
    private const float VerticalGap = 8f;

    /// <summary>Required: BaseLib creates one instance per combat through <c>new()</c>.</summary>
    public BelieverDivinityResource() : base(ResourceId)
    {
    }

    /// <summary>
    /// Hard-clamps every write so no caller can push the value outside [0, <see cref="MaxAmount"/>].
    ///
    /// Delegating to <c>base.Amount</c> is deliberate: the base setter is what raises
    /// <see cref="CustomResource.AmountChanged"/>, and that event can only be raised from inside
    /// <see cref="CustomResource"/>. Assigning through the base property therefore keeps BaseLib's
    /// event, its CombatStateTracker subscription and the HUD update working - no second event
    /// system is introduced here.
    /// </summary>
    public override int Amount
    {
        get => base.Amount;
        set => base.Amount = Math.Clamp(value, 0, MaxAmount);
    }

    /// <summary>True when Divinity is at its maximum (7/7).</summary>
    public bool IsFull => Amount >= MaxAmount;

    /// <summary>
    /// Adds Divinity. Non-positive amounts change nothing, and the result is clamped to
    /// <see cref="MaxAmount"/>, so <c>Gain(100)</c> from 1 still yields 7.
    /// </summary>
    public void Gain(int amount)
    {
        if (amount <= 0) return;
        Amount += amount;
    }

    /// <summary>True when <paramref name="amount"/> is positive and there is enough Divinity to spend it.</summary>
    public bool CanSpend(int amount) => amount > 0 && Amount >= amount;

    /// <summary>
    /// Spends Divinity. Returns false and changes nothing when Divinity is insufficient, so the
    /// value can never drop below 0.
    /// </summary>
    public bool TrySpend(int amount)
    {
        if (!CanSpend(amount)) return false;
        Amount -= amount;
        return true;
    }

    /// <summary>
    /// Called when the PlayerCombatState is created, i.e. at the start of each combat.
    /// Every combat therefore begins at 0/7.
    /// </summary>
    public override void PrepForCombat<T>(PlayerCombatState playerCombatState)
    {
        base.PrepForCombat<T>(playerCombatState);
        Amount = 0;
    }

    /// <summary>Rendered as "current/max" - including "0/7", never a bare "0".</summary>
    public override string GetTextForAmount(int displayAmount) => $"{displayAmount}/{MaxAmount}";

    /// <summary>
    /// Only the Believer sees this counter. The amount is intentionally not part of the check,
    /// so the counter is visible from 0/7 onwards.
    /// </summary>
    public override bool ShouldShowDisplay() =>
        Owner?.Character is global::Believer.BelieverCode.Character.Believer;

    /// <summary>
    /// Registers the combat HUD counter through BaseLib's <see cref="ExtraCombatUi"/>, placed
    /// directly above the energy counter. M1 uses the stock <see cref="NAdditionalResourceDisplay"/>
    /// and deliberately ships no custom icon.
    /// </summary>
    public override void RegisterResourceVisuals<T>()
    {
        ValidateType<T>();

        ExtraCombatUi.RegisterCombatUiElement(static (ui, player, combatState) =>
        {
            var playerCombatState = player.PlayerCombatState;
            if (playerCombatState == null) return null!;

            var resource = CustomResources<T>.Get(playerCombatState);
            if (resource == null) return null!;

            var display = new NAdditionalResourceDisplay(player, resource);
            ui.AddChild(display);

            // CombatUiPositioning only offers None / Left / AroundEnergy, and AroundEnergy lays
            // elements out on a 60px circle to the lower-left of the energy counter. To sit
            // directly above the energy counter the control is therefore left unmanaged (None) and
            // positioned here instead.
            //
            // Both this display and EnergyCounterContainer are BottomLeft-anchored children of the
            // same NCombatUi, so their Position values share one origin - exactly the assumption
            // ExtraCombatUi.VecForPosition makes when it adds the two together. The display's size
            // is fixed at construction (88x88), so no layout pass is needed first.
            var anchor = ui.EnergyCounterContainer;
            display.Position = new Vector2(
                anchor.Position.X + (anchor.Size.X - display.Size.X) * 0.5f,
                anchor.Position.Y - display.Size.Y - VerticalGap);

            return display;
        }, ExtraCombatUi.CombatUiPositioning.None);
    }
}
