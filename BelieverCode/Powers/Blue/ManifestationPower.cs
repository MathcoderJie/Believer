using Believer.BelieverCode.Powers;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace Believer.BelieverCode.Powers.Blue;

/// <summary>
/// 显圣 / Manifestation power card: adds <see cref="PowerModel.Amount"/> extra cards to the draw that
/// happens when the owner enters the Omniscience form.
///
/// The activation reads this power (see <c>Mechanics.Omniscience.Activate</c>); extending the form
/// with 再创世 never re-applies the power or re-runs the activation, so the extra draw happens exactly
/// once per form entry.
/// </summary>
public sealed class ManifestationPower : BelieverPower
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;
}
