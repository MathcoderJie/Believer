using BaseLib.Utils;
using Believer.BelieverCode.Cards.Miracles;
using Believer.BelieverCode.Cards.Signature;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace Believer.BelieverCode.Miracles;

/// <summary>
/// The 12 temporary enhanced Miracle definitions. Each owns only identity/description and delegates
/// its base effect to the matching normal Miracle via <see cref="EnhancedMiracleCard"/>.
/// </summary>
[Pool(typeof(BelieverSignatureCardPool))]
public sealed class EnhancedLetThereBeLight() : EnhancedMiracleCard(0, CardType.Skill, TargetType.Self)
{
    protected override MiracleCard CreateBaseCard() =>
        (MiracleCard)Owner!.Creature!.CombatState!.CreateCard(ModelDb.Card<LetThereBeLight>(), Owner);
}

[Pool(typeof(BelieverSignatureCardPool))]
public sealed class EnhancedGenesis() : EnhancedMiracleCard(0, CardType.Skill, TargetType.Self)
{
    protected override MiracleCard CreateBaseCard() =>
        (MiracleCard)Owner!.Creature!.CombatState!.CreateCard(ModelDb.Card<Genesis>(), Owner);
}

[Pool(typeof(BelieverSignatureCardPool))]
public sealed class EnhancedFourHorsemen() : EnhancedMiracleCard(0, CardType.Skill, TargetType.Self)
{
    protected override MiracleCard CreateBaseCard() =>
        (MiracleCard)Owner!.Creature!.CombatState!.CreateCard(ModelDb.Card<FourHorsemen>(), Owner);
}

[Pool(typeof(BelieverSignatureCardPool))]
public sealed class EnhancedPartingOfTheRedSea() : EnhancedMiracleCard(0, CardType.Skill, TargetType.Self)
{
    protected override MiracleCard CreateBaseCard() =>
        (MiracleCard)Owner!.Creature!.CombatState!.CreateCard(ModelDb.Card<PartingOfTheRedSea>(), Owner);
}

[Pool(typeof(BelieverSignatureCardPool))]
public sealed class EnhancedTenPlagues() : EnhancedMiracleCard(0, CardType.Skill, TargetType.Self)
{
    protected override MiracleCard CreateBaseCard() =>
        (MiracleCard)Owner!.Creature!.CombatState!.CreateCard(ModelDb.Card<TenPlagues>(), Owner);
}

[Pool(typeof(BelieverSignatureCardPool))]
public sealed class EnhancedFallOfJericho() : EnhancedMiracleCard(0, CardType.Skill, TargetType.Self)
{
    protected override MiracleCard CreateBaseCard() =>
        (MiracleCard)Owner!.Creature!.CombatState!.CreateCard(ModelDb.Card<FallOfJericho>(), Owner);
}

[Pool(typeof(BelieverSignatureCardPool))]
public sealed class EnhancedJudgmentOfFire() : EnhancedMiracleCard(0, CardType.Skill, TargetType.AnyEnemy)
{
    protected override MiracleCard CreateBaseCard() =>
        (MiracleCard)Owner!.Creature!.CombatState!.CreateCard(ModelDb.Card<JudgmentOfFire>(), Owner);
}

[Pool(typeof(BelieverSignatureCardPool))]
public sealed class EnhancedSevenSeals() : EnhancedMiracleCard(0, CardType.Skill, TargetType.Self)
{
    protected override MiracleCard CreateBaseCard() =>
        (MiracleCard)Owner!.Creature!.CombatState!.CreateCard(ModelDb.Card<SevenSeals>(), Owner);
}

[Pool(typeof(BelieverSignatureCardPool))]
public sealed class EnhancedFiveLoavesAndTwoFish() : EnhancedMiracleCard(0, CardType.Skill, TargetType.Self)
{
    protected override MiracleCard CreateBaseCard() =>
        (MiracleCard)Owner!.Creature!.CombatState!.CreateCard(ModelDb.Card<FiveLoavesAndTwoFish>(), Owner);
}

[Pool(typeof(BelieverSignatureCardPool))]
public sealed class EnhancedLazarusRises() : EnhancedMiracleCard(0, CardType.Skill, TargetType.Self)
{
    protected override MiracleCard CreateBaseCard() =>
        (MiracleCard)Owner!.Creature!.CombatState!.CreateCard(ModelDb.Card<LazarusRises>(), Owner);
}

[Pool(typeof(BelieverSignatureCardPool))]
public sealed class EnhancedLordOfHosts() : EnhancedMiracleCard(0, CardType.Skill, TargetType.Self)
{
    protected override MiracleCard CreateBaseCard() =>
        (MiracleCard)Owner!.Creature!.CombatState!.CreateCard(ModelDb.Card<LordOfHosts>(), Owner);
}

[Pool(typeof(BelieverSignatureCardPool))]
public sealed class EnhancedWhiteThroneJudgment() : EnhancedMiracleCard(0, CardType.Skill, TargetType.Self)
{
    protected override MiracleCard CreateBaseCard() =>
        (MiracleCard)Owner!.Creature!.CombatState!.CreateCard(ModelDb.Card<WhiteThroneJudgment>(), Owner);
}
