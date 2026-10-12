using CardRPG.Core;
using Xunit;

namespace CardRPG.Core.Tests;

public sealed class ExpeditionTests
{
    private static ExpeditionSession Start() { var s = new ExpeditionSession(); Assert.True(s.Depart()); return s; }
    private static void Win(ExpeditionSession session)
    {
        for (var round = 0; session.State.Phase == ExpeditionPhase.Battle && round < 50; round++)
        {
            for (var moves = 0; moves < 20 && session.State.Phase == ExpeditionPhase.Battle; moves++)
            {
                var s = session.State;
                var target = s.Enemies.FindIndex(e => e.Health > 0);
                var index = s.Hand.Select((id, i) => (Card: ExpeditionContent.Cards[id], Index: i))
                    .Where(x => x.Card.ActionCost <= s.ActionPoints && x.Card.ManaCost <= s.Mana)
                    .OrderByDescending(x => x.Card.Damage).Select(x => x.Index).DefaultIfEmpty(-1).First();
                if (index < 0) break;
                Assert.True(session.PlayCard(index, target));
            }
            if (session.State.Phase == ExpeditionPhase.Battle) session.EndTurn();
        }
        Assert.Equal(ExpeditionPhase.Exploring, session.State.Phase);
    }

    [Fact] public void BrowsingAndInvalidActionsNeverAdvanceTime()
    {
        var s = Start(); var before = s.ToJson();
        _ = s.Actions(); _ = s.Deck(); _ = s.Threat;
        Assert.False(s.Move("furnace")); Assert.False(s.Act("boss")); Assert.False(s.Equip("staff"));
        Assert.Equal(before, s.ToJson());
    }
    [Fact] public void NoWeaponStillAddsDefaultCardsAlongsideArmor()
    {
        var s = new ExpeditionSession(new() { Weapon = "" });
        Assert.Equal(8, s.Deck().Count); Assert.Equal(4, s.Deck().Count(x => x == "punch"));
    }
    [Fact] public void SearchWarnsThenInterruptsWithoutGeneratingLoot()
    {
        var s = Start(); s.Move("square"); s.Act("safe");
        Assert.True(s.State.Locked); Assert.Equal(1, s.State.SafeProgress);
        Assert.Equal(ExpeditionPhase.Exploring, s.State.Phase);
        s.Act("safe");
        Assert.Equal(ExpeditionPhase.Battle, s.State.Phase);
        Assert.Equal("patrol", s.State.Encounter); Assert.Equal(4, s.State.Enemies[0].Block);
        Assert.Empty(s.State.Loot); Assert.DoesNotContain("safe", s.State.Flags);
        Win(s); Assert.True(s.Act("safe")); Assert.Contains("safe", s.State.Flags);
        Assert.Single(s.State.Loot); Assert.False(s.Act("safe"));
    }
    [Fact] public void LeavingAfterWarningAvoidsBattleButReturningGivesAnotherWarning()
    {
        var s = Start(); s.Move("square"); s.Act("safe"); s.Move("scrap");
        Assert.False(s.State.Locked); Assert.Equal(ExpeditionPhase.Exploring, s.State.Phase);
        s.Move("square"); Assert.True(s.State.Locked);
        Assert.Equal(ExpeditionPhase.Exploring, s.State.Phase);
    }
    [Fact] public void DirectAttackLocksRosterBeforePatrolAdvances()
    {
        var s = Start(); s.Move("square"); s.Act("convoy");
        Assert.Single(s.State.Enemies); var steps = s.State.Steps; var eta = s.State.PatrolEta;
        s.EndTurn(); Assert.Equal(steps, s.State.Steps); Assert.Equal(eta, s.State.PatrolEta);
    }
    [Fact] public void ArrivedPatrolJoinsConvoyOnceAndOnlyParticipantsAreCleared()
    {
        var s = Start(); s.Move("square"); s.Act("safe"); s.Act("convoy");
        Assert.Equal(2, s.State.Enemies.Count); Win(s);
        Assert.Contains("convoy", s.State.Flags); Assert.Contains("patrol", s.State.Flags);
        Assert.DoesNotContain("boss", s.State.Flags); Assert.False(s.Act("convoy"));
    }
    [Fact] public void PreparationUsesRealCardAndStillDrawsOnlyFive()
    {
        var s = Start(); s.Move("square"); s.Act("prepare"); s.Act("convoy");
        Assert.Equal(5, s.State.Hand.Count); Assert.Equal("strike", s.State.Hand[0]);
        Assert.Equal(s.Deck().Order(), s.State.Hand.Concat(s.State.Draw).Order());
        Assert.Equal(3, s.State.ActionPoints); Assert.Equal(2, s.State.Mana);
    }
    [Fact] public void TurnEndsDiscardHandAndRecyclesWithoutLosingCards()
    {
        var s = Start(); s.Move("square"); s.Act("convoy"); s.EndTurn();
        Assert.Equal(5, s.State.Hand.Count);
        Assert.Equal(s.Deck().Order(), s.State.Hand.Concat(s.State.Draw).Concat(s.State.Discard).Order());
        Assert.Equal(3, s.State.ActionPoints); Assert.Equal(3, s.State.Mana); Assert.Equal(0, s.State.Block);
    }
    [Fact] public void InsufficientResourcesAndDeadTargetDoNotConsumeCards()
    {
        var s = Start(); s.Move("square"); s.Act("convoy");
        s.State.ActionPoints = 0; s.State.Mana = 0; var before = s.ToJson();
        Assert.False(s.PlayCard(0, 0)); Assert.False(s.PlayCard(-1, 0)); Assert.Equal(before, s.ToJson());
    }
    [Fact] public void SaveLoadPreservesBattlePilesAndFutureRandomness()
    {
        var s = Start(); s.Move("square"); s.Act("convoy");
        var restored = ExpeditionSession.FromJson(s.ToJson());
        s.EndTurn(); restored.EndTurn(); Assert.Equal(s.ToJson(), restored.ToJson());
    }
    [Fact] public void BlessingOffersPersistAcrossReloadAndCanOnlyBeChosenOnce()
    {
        var s = Start(); s.Move("scrap"); s.Move("workshop"); s.Act("blessing");
        Assert.Equal(2, s.State.BlessingOffers.Distinct().Count());
        var loaded = ExpeditionSession.FromJson(s.ToJson()); var chosen = loaded.State.BlessingOffers[0];
        Assert.True(loaded.ChooseBlessing(chosen)); Assert.False(loaded.ChooseBlessing(chosen));
        Assert.Equal(chosen, loaded.State.Blessing); Assert.False(loaded.Act("blessing"));
    }
    [Fact] public void WorkbenchChangesAllWeaponCardsWithoutHealing()
    {
        var s = Start(); s.Move("scrap"); s.Move("workshop"); s.State.Health = 20;
        Assert.False(s.Equip("staff")); s.Act("workshop"); Assert.True(s.Equip("staff"));
        Assert.Contains("bolt", s.Deck()); Assert.DoesNotContain("strike", s.Deck()); Assert.Equal(20, s.State.Health);
    }
    [Fact] public void ExtractionBanksLootExactlyOnceAndClearsTemporaryBlessing()
    {
        var s = Start(); s.State.Loot.AddRange(["bracer", "bracer"]); s.State.Materials = 5; s.State.Blessing = "edge";
        Assert.True(s.Act("extract")); Assert.False(s.Act("extract"));
        Assert.Contains("bracer", s.State.Owned); Assert.Equal(8, s.State.StoredMaterials);
        Assert.Empty(s.State.Loot); Assert.Equal("", s.State.Blessing);
        Assert.True(s.ReturnToCamp()); s.Equip("bracer"); Assert.Equal(10, s.Deck().Count);
    }
    [Fact] public void WearingFoundGearDoesNotProtectItFromDeath()
    {
        var s = Start(); s.State.Loot.AddRange(["breaker", "bracer"]);
        s.State.Weapon = "breaker"; s.State.WearBracer = true; s.State.Materials = 10;
        s.Move("square"); s.Act("convoy"); s.State.Health = 1; s.EndTurn();
        Assert.Equal(ExpeditionPhase.Defeated, s.State.Phase); Assert.Empty(s.State.Loot);
        Assert.Equal("hammer", s.State.Weapon); Assert.False(s.State.WearBracer);
        Assert.DoesNotContain("bracer", s.State.Owned); Assert.Equal(0, s.State.Materials);
    }
    [Fact] public void RetreatResolvesAttackAndDoesNotClearEnemyOrGiveReward()
    {
        var s = Start(); s.Move("square"); s.Act("convoy"); s.State.Materials = 5;
        s.EndTurn(true); Assert.Equal("gate", s.State.Location); Assert.Equal(2, s.State.Materials);
        Assert.Equal(36, s.State.Health); Assert.Empty(s.State.Loot); Assert.DoesNotContain("convoy", s.State.Flags);
    }
    [Fact] public void AlarmChangesBossRosterAndSecondaryExitRequiresPower()
    {
        var s = Start(); s.Move("scrap"); s.Move("exit"); Assert.False(s.Act("extract"));
        s.Move("scrap"); s.Act("power"); s.Move("workshop"); s.Move("furnace"); s.Act("alarm"); s.Act("boss");
        Assert.Single(s.State.Enemies); Assert.Equal("boss", s.State.Enemies[0].Id);
    }
    [Fact] public void CompleteLootBattleExtractionAndNextRunLoop()
    {
        var s = Start(); s.Move("square"); s.Act("convoy"); Win(s);
        s.Move("scrap"); s.Act("salvage"); s.Act("power"); s.Act("heal");
        s.Move("workshop"); s.Act("workshop"); s.Equip("breaker");
        s.Act("blessing"); s.ChooseBlessing(s.State.BlessingOffers[0]);
        s.Move("furnace"); s.Act("alarm"); s.Act("boss"); Win(s);
        s.Move("exit"); s.Act("extract");
        Assert.Equal(ExpeditionPhase.Extracted, s.State.Phase); Assert.Contains("bracer", s.State.Owned);
        s.ReturnToCamp(); s.Depart(); Assert.Empty(s.State.Flags); Assert.Empty(s.State.Loot); Assert.Equal(4, s.State.PatrolEta);
    }
    [Fact] public void MagicCardsUseManaWhileConversionCardRestoresIt()
    {
        var s = new ExpeditionSession(); s.Equip("staff"); s.Depart(); s.Move("square"); s.Act("convoy");
        s.State.Hand = ["bolt", "spark"];
        Assert.True(s.PlayCard(0, 0)); Assert.Equal(1, s.State.Mana); Assert.Equal(3, s.State.ActionPoints);
        Assert.True(s.PlayCard(0, 0)); Assert.Equal(2, s.State.Mana); Assert.Equal(2, s.State.ActionPoints);
    }
    [Fact] public void RetreatCanFailAndLoseAllUnbankedRewards()
    {
        var s = Start(); s.State.Loot.Add("bracer"); s.State.Materials = 8;
        s.Move("square"); s.Act("convoy"); s.State.Health = 1;
        s.EndTurn(true); Assert.Equal(ExpeditionPhase.Defeated, s.State.Phase);
        Assert.Empty(s.State.Loot); Assert.Equal(0, s.State.Materials); Assert.Equal(0, s.State.StoredMaterials);
    }
    [Fact] public void BattleSaveCannotBeUsedToRerollLootOrReceiveVictoryTwice()
    {
        var s = Start(); s.Move("square"); s.Act("convoy"); Win(s);
        var restored = ExpeditionSession.FromJson(s.ToJson()); var before = restored.ToJson();
        Assert.False(restored.PlayCard(0, 0)); Assert.False(restored.EndTurn()); Assert.False(restored.Act("convoy"));
        Assert.Equal(before, restored.ToJson());
    }
    [Theory] [InlineData("null")] [InlineData("{\"Version\":2}")] [InlineData("{\"Hand\":[\"bad\"]}")]
    public void UnsupportedOrInvalidSaveIsRejected(string json) => Assert.Throws<InvalidDataException>(() => ExpeditionSession.FromJson(json));
}
