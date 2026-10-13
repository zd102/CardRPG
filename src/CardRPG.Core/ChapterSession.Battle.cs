namespace CardRPG.Core;

public sealed partial class ChapterSession
{
    public static string EnemyName(string id) => id switch
    { "E01" => "搬运傀儡", "E02" => "铆盾守卫", "E03" => "警铃巡视者", "E13" => "军需领班", "dummy" => "台架练习靶", _ => id };
    public static string EnemyIntent(ChapterEnemy e, int count = 2) => e.Health == 0 ? "已击败" : e.Id switch
    {
        "E01" => (e.Cycle % 3) switch { 0 => "攻击 6", 1 => "抬臂准备 · 不攻击", _ => "重砸 10" },
        "E02" => e.Cycle % 2 == 0 ? "攻击 5" : "护盾设为 6",
        "E03" => (e.Cycle % 3) switch { 0 => "给生命最低的同伴 4 护盾", 1 => "攻击 4", _ => e.Signaled ? "信号已用尽" : count >= 3 ? "全体同伴获得 3 护盾" : "召来一名搬运傀儡（仅一次）" },
        "E13" => (e.Cycle % 3) switch { 0 => "攻击 8", 1 => "获得 8 护盾", _ => "攻击 11" },
        _ => "静止 · 无伤害，可随时结束试用"
    };
    public static int EnemyDamage(ChapterEnemy e) => e.Health == 0 ? 0 : e.Id switch
    {
        "E01" => (e.Cycle % 3) switch { 0 => 6, 2 => 10, _ => 0 },
        "E02" => e.Cycle % 2 == 0 ? 5 : 0,
        "E03" => e.Cycle % 3 == 1 ? 4 : 0,
        "E13" => (e.Cycle % 3) switch { 0 => 8, 2 => 11, _ => 0 }, _ => 0
    };
    private static ChapterEnemy Enemy(string id)
    {
        var health = id switch { "E01" => 24, "E02" => 26, "E03" => 18, "E13" => 48, _ => 999 };
        return new() { Id = id, Health = health, MaxHealth = health, Shield = id switch { "E02" => 6, "E13" => 4, _ => 0 } };
    }
    private void StartBattle(string id)
    {
        if (State.Paused.Remove(id, out var paused))
        {
            State.Battle = paused; State.Phase = ChapterPhase.Battle; paused.ReturnPlace = State.Previous;
            BeginRound(paused); Note("恢复先前遭遇，敌人、牌区与魔法保持，不重抽开场。"); return;
        }
        var b = new ChapterBattle { Id = id, ReturnPlace = State.Previous, Equipment = Equipped.ToList(), Draw = Deck() };
        string[] enemies = id switch
        {
            "K01" => ["E01", "E02"], "K02" => ["E01"], "K09" => ["E03", "E01"],
            "E13" => ["E13", "E02"], _ => throw new ArgumentException("未知遭遇", nameof(id))
        };
        b.Enemies = enemies.Select(Enemy).ToList();
        if (id == "E13" && State.Pressure >= 26) b.Enemies[0].Shield += 4;
        Shuffle(b.Draw); State.Battle = b; State.Phase = ChapterPhase.Battle; BeginRound(b);
        Note("进入战斗。敌人意图公开，战斗思考不推进区域压力。");
    }
    public bool StartTrial()
    {
        if (State.Phase != ChapterPhase.Camp || !State.Done.Contains("Q02")) return false;
        var b = new ChapterBattle { Id = "trial", ReturnPlace = "A01", Equipment = ["G01", "G09", "G26"], TrialRandomState = State.RandomState };
        b.Draw = b.Equipment.SelectMany(g => ChapterContent.Gear[g].Cards).ToList();
        b.Enemies = [Enemy("dummy")]; State.Battle = b; State.Phase = ChapterPhase.Battle;
        // Fixed legal hand from actual copies. Trial uses neither real equipment nor real health.
        b.Round = 1; b.Action = 3; b.Mana = 2;
        foreach (var card in new[] { "C36", "C01", "C03", "C35", "C02" }) { b.Draw.Remove(card); b.Hand.Add(card); }
        Note("岑砧：先用预备工具看看下一张牌，再做一次攻击。借用手套只在台架里有效。"); return true;
    }
    public bool EndTrial()
    {
        if (State.Phase != ChapterPhase.Battle || State.Battle?.Id != "trial") return false;
        State.RandomState = State.Battle.TrialRandomState; State.Battle = null; State.Phase = ChapterPhase.Camp;
        Note(State.World.Contains("trial-complete") ? "试用完成，检修工具手套配方已开放。借用品已归还。" : "暂时结束试用，借用品已归还；随时可以重试。"); return true;
    }
    private void BeginRound(ChapterBattle b)
    {
        b.BeginPending = false; b.Round++; b.Action = 3; b.Mana = Math.Min(3, b.Mana + 1);
        b.Shield = b.KeepShield ? Math.Min(b.Shield, 4) : 0; b.KeepShield = false;
        b.ExtraDraw = 0; b.ShieldResonated = false;
        if (b.Round == 1 && State.Resonance == "R01" && b.Id != "trial") b.Shield += 5;
        DrawCards(b, 5);
    }
    private void DrawCards(ChapterBattle b, int count, bool extra = false)
    {
        if (extra) count = Math.Min(count, 2 - b.ExtraDraw);
        for (var i = 0; i < count; i++)
        {
            if (b.Draw.Count == 0) { b.Draw.AddRange(b.Discard); b.Discard.Clear(); Shuffle(b.Draw); }
            if (b.Draw.Count == 0) return;
            b.Hand.Add(b.Draw[0]); b.Draw.RemoveAt(0); if (extra) b.ExtraDraw++;
        }
    }
    public bool CanPlay(int index, int target = 0)
    {
        var b = State.Battle;
        if (State.Phase != ChapterPhase.Battle || b == null || index < 0 || index >= b.Hand.Count) return false;
        var c = ChapterContent.Cards[b.Hand[index]];
        return b.Action >= c.Action && b.Mana >= c.Mana && (c.Damage == 0 || target >= 0 && target < b.Enemies.Count && b.Enemies[target].Health > 0);
    }
    public bool PlayCard(int index, int target)
    {
        if (!CanPlay(index, target)) return false;
        var b = State.Battle!; var c = ChapterContent.Cards[b.Hand[index]];
        b.Action -= c.Action; b.Mana -= c.Mana; b.Hand.RemoveAt(index);
        var damage = c.Damage;
        if (c.Id == "C07") { var used = Math.Min(8, b.Shield); b.Shield -= used; damage += used; }
        if (damage > 0)
        {
            var targets = c.Id == "C18" ? b.Enemies.Where(e => e.Health > 0).ToArray() : [b.Enemies[target]];
            foreach (var enemy in targets)
            {
                var absorbed = c.Id == "C06" ? 0 : Math.Min(enemy.Shield, damage);
                enemy.Shield -= absorbed; enemy.Health = Math.Max(0, enemy.Health - damage + absorbed);
                if (enemy.Id == "E13" && enemy.Health == 0 && b.Round <= 3) b.BonusSecured = true;
            }
            if (b.Id == "trial" && b.TrialToolUsed && State.World.Add("trial-complete")) Note("岑砧笑了：抽牌让你多一种选择，也会占用行动。检修工具手套配方已解锁。");
        }
        if (c.Shield > 0)
        {
            b.Shield += c.Shield;
            if (State.Resonance == "R07" && b.Id != "trial" && c.Shield >= 7 && !b.ShieldResonated)
            { b.Shield += 2; b.ShieldResonated = true; }
        }
        if (c.Id == "C05") b.Mana = Math.Min(3, b.Mana + 1);
        if (c.Id == "C25") b.KeepShield = true;
        if (c.Id == "C26") DrawCards(b, 1, true);
        if (c.Id == "C36") { DrawCards(b, 2, true); b.TrialToolUsed = true; }
        b.Discard.Add(c.Id);
        if (b.Enemies.All(e => e.Health == 0))
        {
            if (b.Id == "trial") { b.Enemies[0].Health = b.Enemies[0].MaxHealth; Note("练习靶复位，试用无战利品。"); }
            else WinBattle(b);
        }
        return true;
    }
    public bool EndTurn(bool retreat = false)
    {
        var b = State.Battle;
        if (State.Phase != ChapterPhase.Battle || b == null) return false;
        if (b.Id == "trial" && retreat) return EndTrial();
        b.Discard.AddRange(b.Hand); b.Hand.Clear();
        foreach (var enemy in b.Enemies.ToArray())
        {
            if (enemy.Health == 0) continue;
            var hit = EnemyDamage(enemy); var absorb = Math.Min(b.Shield, hit);
            b.Shield -= absorb; if (b.Id != "trial") State.Health = Math.Max(0, State.Health - hit + absorb);
            switch (enemy.Id)
            {
                case "E02" when enemy.Cycle % 2 == 1: enemy.Shield = 6; break;
                case "E13" when enemy.Cycle % 3 == 1: enemy.Shield = Math.Min(20, enemy.Shield + 8); break;
                case "E03" when enemy.Cycle % 3 == 0:
                    var ally = b.Enemies.Where(e => e != enemy && e.Health > 0).OrderBy(e => e.Health).FirstOrDefault() ?? enemy;
                    ally.Shield = Math.Min(20, ally.Shield + 4); break;
                case "E03" when enemy.Cycle % 3 == 2 && !enemy.Signaled:
                    enemy.Signaled = true;
                    if (b.Enemies.Count < 3) { b.Enemies.Add(Enemy("E01")); Note("支援傀儡抵达，下个敌方阶段才会行动。"); }
                    else foreach (var e in b.Enemies.Where(e => e.Health > 0)) e.Shield = Math.Min(20, e.Shield + 3);
                    break;
            }
            enemy.Cycle = (enemy.Cycle + 1) % (enemy.Id == "E02" ? 2 : 3);
            if (State.Health == 0) { Defeat(); return true; }
        }
        if (b.Id == "E13" && b.Round == 3 && !b.BonusSecured) Note("额外弹簧包被运走了。破甲锤仍然可以取得。");
        if (retreat)
        {
            b.BeginPending = true; State.Paused[b.Id] = b; State.Battle = null;
            State.Location = b.ReturnPlace; State.Phase = ChapterPhase.Exploring; State.Warned = false;
            Note("承受本轮意图后退回" + ChapterContent.Places[State.Location].Name + "。敌人与牌区暂停保存；仍需自行撤离。");
        }
        else BeginRound(b);
        return true;
    }
    private void WinBattle(ChapterBattle b)
    {
        State.Run.Add(b.Id); State.Paused.Remove(b.Id);
        switch (b.Id)
        {
            case "K01": SpawnGround("battle-K01", "运输队战利品", ("M01", 1), ("M03", 1)); break;
            case "K02": SpawnGround("battle-K02", "傀儡残骸", ("M01", 1)); break;
            case "K09": SpawnGround("battle-K09", "巡逻残骸", ("M01", 1)); State.PatrolPlace = ""; State.Warned = false; break;
            case "E13":
                State.World.Add("leader-won"); SpawnGround("battle-E13", "领班战利品", ("G03", 1), ("M01", 2), ("M03", b.BonusSecured ? 3 : 1)); break;
        }
        State.Battle = null; State.Phase = ChapterPhase.Exploring;
        Note("战斗结束，战利品留在战场，装入行囊后再撤离。" + (b.Id == "E13" ? "领班首胜已记录，若装备遗失可回广场取回。" : ""));
    }
}
