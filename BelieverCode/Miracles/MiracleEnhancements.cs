using MegaCrit.Sts2.Core.Models;

namespace Believer.BelieverCode.Miracles;

/// <summary>
/// Maps each normal Miracle's canonical model to its separate enhanced card definition. Enhanced
/// cards are temporary Omniscience-only signature cards; they are not in the normal rare pool and
/// are not part of <see cref="MiracleCatalog"/>.
/// </summary>
public static class MiracleEnhancements
{
    public static CardModel? GetEnhancedCanonical(MiracleCard miracle)
    {
        return miracle switch
        {
            Cards.Miracles.LetThereBeLight => ModelDb.Card<EnhancedLetThereBeLight>(),
            Cards.Miracles.Genesis => ModelDb.Card<EnhancedGenesis>(),
            Cards.Miracles.FourHorsemen => ModelDb.Card<EnhancedFourHorsemen>(),
            Cards.Miracles.PartingOfTheRedSea => ModelDb.Card<EnhancedPartingOfTheRedSea>(),
            Cards.Miracles.TenPlagues => ModelDb.Card<EnhancedTenPlagues>(),
            Cards.Miracles.FallOfJericho => ModelDb.Card<EnhancedFallOfJericho>(),
            Cards.Miracles.JudgmentOfFire => ModelDb.Card<EnhancedJudgmentOfFire>(),
            Cards.Miracles.SevenSeals => ModelDb.Card<EnhancedSevenSeals>(),
            Cards.Miracles.FiveLoavesAndTwoFish => ModelDb.Card<EnhancedFiveLoavesAndTwoFish>(),
            Cards.Miracles.LazarusRises => ModelDb.Card<EnhancedLazarusRises>(),
            Cards.Miracles.LordOfHosts => ModelDb.Card<EnhancedLordOfHosts>(),
            Cards.Miracles.WhiteThroneJudgment => ModelDb.Card<EnhancedWhiteThroneJudgment>(),
            _ => null,
        };
    }
}
