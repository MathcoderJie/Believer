using BaseLib.Utils;
using Believer.BelieverCode.Cards.Signature;
using Believer.BelieverCode.Miracles;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Believer.BelieverCode.Cards.Miracles;

/// <summary>
/// 4/12 红海裂天 / Parting of the Red Sea - 30 damage to all enemies, gain 40 Block, and keep that
/// Block through the next turn.
///
/// Block retention reuses the base game's own "Blur" power (BlurPower): it prevents the owner's block
/// from being cleared at the start of the turn and then decrements itself away, which is exactly the
/// vanilla "retain your Block next turn" mechanic.
/// </summary>
[Pool(typeof(BelieverSignatureCardPool))]
public sealed class PartingOfTheRedSea() : MiracleCard(0, CardType.Skill, CardRarity.Token, TargetType.Self)
{
    private const decimal BlockAmount = 40m;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var combatState = CombatState;
        if (combatState == null) return;

        await DamageCmd.Attack(30m).FromCard(this).TargetingAllOpponents(combatState).Execute(choiceContext);

        await CreatureCmd.GainBlock(Owner.Creature, new BlockVar(BlockAmount, ValueProp.Move), cardPlay);
        await PowerCmd.Apply<BlurPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }
}
