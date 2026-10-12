using System.Text.Json;

namespace CardRPG.Core;

public enum ExpeditionPhase { Camp, Exploring, Battle, Reward, Extracted, Defeated }

public sealed class EnemyState
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    public int Damage { get; set; }
    public int Block { get; set; }
}

/// <summary>Serializable state includes RNG and all card piles, so loading never rerolls a run.</summary>
public sealed class ExpeditionState
{
    public int Version { get; set; } = 1;
    public ExpeditionPhase Phase { get; set; }
    public string Location { get; set; } = "gate";
    public int Health { get; set; } = 42;
    public int Steps { get; set; }
    public int PatrolEta { get; set; } = 4;
    public bool Locked { get; set; }
    public bool Prepared { get; set; }
    public int SafeProgress { get; set; }
    public int Materials { get; set; }
    public int StoredMaterials { get; set; }
    public int Extractions { get; set; }
    public int Runs { get; set; }
    public int Supplies { get; set; } = 2;
    public uint RandomState { get; set; } = 1701;
    public string Weapon { get; set; } = "hammer";
    public bool WearBracer { get; set; }
    public List<string> Owned { get; set; } = ["hammer", "staff", "armor"];
    public List<string> Loot { get; set; } = [];
    public HashSet<string> Flags { get; set; } = [];
    public HashSet<string> Intel { get; set; } = [];
    public string Blessing { get; set; } = "";
    public List<string> BlessingOffers { get; set; } = [];
    public string Encounter { get; set; } = "";
    public string BattleReason { get; set; } = "";
    public int Round { get; set; }
    public int ActionPoints { get; set; }
    public int Mana { get; set; }
    public int Block { get; set; }
    public List<EnemyState> Enemies { get; set; } = [];
    public List<string> Draw { get; set; } = [];
    public List<string> Hand { get; set; } = [];
    public List<string> Discard { get; set; } = [];
    public List<string> Log { get; set; } = ["选择出征武器。装备决定全部基础卡牌；撤离后收获才会入库。"];
    public string Summary { get; set; } = "";
}

public sealed class ExpeditionSession
{
    public const int MaxHealth = 42;
    public ExpeditionState State { get; }
    public ExpeditionSession(ExpeditionState? state = null) => State = state ?? new();
    public string ToJson() => JsonSerializer.Serialize(State, new JsonSerializerOptions { WriteIndented = true });
    public static ExpeditionSession FromJson(string json)
    {
        var state = JsonSerializer.Deserialize<ExpeditionState>(json) ?? throw new InvalidDataException("存档为空。");
        if (state.Version != 1 || !Enum.IsDefined(state.Phase) || !ExpeditionContent.Places.ContainsKey(state.Location)
            || (state.Weapon != "" && !ExpeditionContent.Gear.ContainsKey(state.Weapon))
            || state.Health is < 0 or > MaxHealth || state.RandomState == 0
            || state.Owned == null || state.Loot == null || state.Flags == null || state.Intel == null
            || state.Hand == null || state.Draw == null || state.Discard == null || state.Enemies == null || state.Log == null
            || state.BlessingOffers == null
            || state.Owned.Concat(state.Loot).Any(g => !ExpeditionContent.Gear.ContainsKey(g))
            || state.Draw.Concat(state.Hand).Concat(state.Discard).Any(c => !ExpeditionContent.Cards.ContainsKey(c)))
            throw new InvalidDataException("不支持或损坏的远征存档。");
        return new(state);
    }

    public List<string> Deck()
    {
        var result = new List<string>(ExpeditionContent.Gear["armor"].Cards);
        result.AddRange(State.Weapon == "" ? ["punch", "punch", "punch", "punch"] : ExpeditionContent.Gear[State.Weapon].Cards);
        if (State.WearBracer) result.AddRange(ExpeditionContent.Gear["bracer"].Cards);
        return result;
    }

    public string Threat => State.Flags.Contains("patrol") ? "巡逻队已击败"
        : State.Locked ? "已锁定：再留在广场行动一次将被拦截"
        : State.PatrolEta == 0 ? "巡逻队驻守广场；进入后会锁定你"
        : State.PatrolEta == 1 ? "预警：巡逻队将在下一次行动后抵达广场"
        : $"巡逻队距广场 {State.PatrolEta} 步";
    public int ThreatTier => State.Steps >= 12 ? 2 : State.Steps >= 7 ? 1 : 0;
    public string ThreatTierText => ThreatTier switch { 0 => "平静", 1 => "戒备 · 新遭遇攻击 +1", _ => "封锁 · 新遭遇攻击 +2" };

    public List<ExpeditionAction> Actions()
    {
        if (State.Phase != ExpeditionPhase.Exploring) return [];
        var actions = new List<ExpeditionAction>();
        void Add(string id, string name, string description, bool enabled = true) => actions.Add(new(id, name, description, enabled));
        switch (State.Location)
        {
            case "gate": Add("extract", "撤离至据点", "安全撤离，不消耗行动。装备和材料入库，临时祝福消失。"); break;
            case "square":
                Add("convoy", "主动迎战运输队", "立即锁定敌人；已抵达的巡逻会协战。胜利获得破甲锤。", !State.Flags.Contains("convoy"));
                Add("prepare", "隐蔽接近", "1 步。若未被拦截，下一场开局准备一张武器牌，占五张手牌之一。", !State.Prepared && !State.Flags.Contains("convoy"));
                Add("safe", State.SafeProgress == 0 ? "撬开保险箱 · 第 1 步" : "继续撬锁 · 第 2 步", State.SafeProgress == 0 ? "制造噪声，巡逻立即抵达并锁定你；可放弃后续操作。" : "完成后得材料与随机装备。若已被锁定，会先被拦截，搜索中断。", !State.Flags.Contains("safe"));
                if (!State.Flags.Contains("patrol") && State.PatrolEta == 0) Add("patrol", "主动迎战巡逻队", "正常交锋，避免被动拦截；附近运输队不会主动协战。");
                break;
            case "scrap":
                Add("salvage", "搜索补给堆", "1 步。获得 3 材料和 1 份治疗补给。", !State.Flags.Contains("salvage"));
                Add("power", "恢复货运站电源", "1 步。开启第二个安全撤离点。", !State.Flags.Contains("power"));
                break;
            case "workshop":
                Add("workshop", "启用临时工坊", "1 步。允许在这里更换武器和护腕；更换本身不耗时。", !State.Flags.Contains("workshop"));
                Add("blessing", "触碰回响装置", "1 步。发现两种随机临时祝福，选择一种；仅本次远征生效。", State.Blessing == "");
                break;
            case "furnace":
                Add("intel", "调查铸炉记录", "1 步。永久记录守卫机制；已调查后免费查看。", !State.Intel.Contains("boss"));
                Add("alarm", "切断警钟", "1 步。最终挑战移除一名支援者。", !State.Flags.Contains("alarm") && !State.Flags.Contains("boss"));
                Add("boss", "挑战铸炉守卫", State.Flags.Contains("alarm") ? "守卫单独应战。胜利获得回响护腕；仍需撤离入库。" : "守卫有一名警钟护卫支援。胜利获得回响护腕。", !State.Flags.Contains("boss"));
                break;
            case "exit": Add("extract", "搭乘货运升降机撤离", "不消耗行动。需先在废料场恢复电源。", State.Flags.Contains("power")); break;
        }
        Add("heal", $"使用补给 · 剩余 {State.Supplies}", "1 步，恢复 14 生命。锁定状态下治疗后会被接战。", State.Supplies > 0 && State.Health < MaxHealth);
        Add("wait", "观察局势 · 等待 1 步", "有意让局势推进。阅读信息与浏览地图本身不耗时。");
        return actions;
    }

    public bool Equip(string id)
    {
        if (State.Phase != ExpeditionPhase.Camp && !(State.Phase == ExpeditionPhase.Exploring && State.Location == "workshop" && State.Flags.Contains("workshop"))) return false;
        if (!State.Owned.Contains(id) && !State.Loot.Contains(id)) return false;
        if (id == "bracer") State.WearBracer = !State.WearBracer;
        else if (ExpeditionContent.Gear[id].Slot == "weapon") State.Weapon = id;
        else return false;
        Note($"调整装备：{ExpeditionContent.Gear[id].Name}。当前卡组 {Deck().Count} 张。");
        return true;
    }

    public bool Depart()
    {
        if (State.Phase != ExpeditionPhase.Camp) return false;
        State.Runs++;
        State.Phase = ExpeditionPhase.Exploring;
        State.Health = MaxHealth;
        State.Location = "gate";
        State.Steps = 0; State.PatrolEta = 4; State.Locked = false; State.Prepared = false;
        State.SafeProgress = 0; State.Materials = 0; State.Supplies = 2;
        State.Flags.Clear(); State.Loot.Clear(); State.Blessing = ""; State.BlessingOffers.Clear();
        ClearBattle();
        Note($"第 {State.Runs} 次远征开始。目标：获得回响护腕并成功撤离。");
        return true;
    }

    public bool Move(string destination)
    {
        if (State.Phase != ExpeditionPhase.Exploring || !ExpeditionContent.Places[State.Location].Neighbors.Contains(destination)) return false;
        State.Location = destination;
        State.Locked = false; State.Prepared = false;
        Note($"前往{ExpeditionContent.Places[destination].Name}，消耗 1 步。");
        Advance(false);
        return true;
    }

    public bool Act(string id)
    {
        if (!Actions().Any(a => a.Id == id && a.Enabled)) return false;
        if (id == "extract") { Extract(); return true; }
        if (id is "convoy" or "patrol" or "boss")
        {
            State.Steps++;
            StartBattle(id, State.Prepared ? "隐蔽突袭：一张武器牌纳入首回合五张手牌" : "主动迎战：在敌方下一步前锁定遭遇", State.Prepared);
            return true;
        }
        var wasLocked = State.Locked;
        if (id == "heal") { State.Supplies--; State.Health = Math.Min(MaxHealth, State.Health + 14); Note("使用补给，恢复 14 生命。"); }
        if (id == "safe" && wasLocked)
        {
            State.Steps++;
            Note("守卫打断撬锁；保险箱尚未打开，未生成奖励。可在战后继续。");
            StartBattle("patrol", "无视锁定预警：被动拦截，巡逻首回合护盾 +4", false, true);
            return true;
        }
        switch (id)
        {
            case "safe":
                State.SafeProgress++;
                if (State.SafeProgress == 1)
                {
                    if (!State.Flags.Contains("patrol")) State.PatrolEta = 0;
                    Note("撬锁 1/2：金属响声引来巡逻。下一步可以离开、迎战或冒险继续。");
                }
                else
                {
                    State.Flags.Add("safe"); State.Materials += 5;
                    var gear = Next(2) == 0 ? "breaker" : "bracer";
                    State.Loot.Add(gear);
                    Note($"保险箱打开：材料 +5，获得{ExpeditionContent.Gear[gear].Name}（未撤离）。");
                }
                break;
            case "salvage": State.Flags.Add(id); State.Materials += 3; State.Supplies++; Note("补给堆：材料 +3，补给 +1。"); break;
            case "power": State.Flags.Add(id); Note("货运站电源已恢复，第二撤离点开放。"); break;
            case "workshop": State.Flags.Add(id); Note("临时工坊已启用。可以更换当前持有装备。"); break;
            case "alarm": State.Flags.Add(id); Note("警钟已切断。铸炉守卫失去支援。"); break;
            case "intel": State.Intel.Add("boss"); Note("永久情报：守卫每回合重建 5 护盾；破甲重击可以绕过。"); break;
            case "prepare": Note("利用货箱隐蔽接近；若未被拦截，获得开场准备。"); break;
            case "blessing":
                var offers = new List<string> { "edge", "bastion", "surge" };
                Shuffle(offers); State.BlessingOffers = offers.Take(2).ToList();
                break;
            case "wait": Note("等待 1 步，观察敌人的动向。"); break;
        }
        Advance(wasLocked);
        if (State.Phase == ExpeditionPhase.Exploring && id == "prepare") State.Prepared = true;
        if (State.Phase == ExpeditionPhase.Exploring && id == "blessing") State.Phase = ExpeditionPhase.Reward;
        return true;
    }

    public static string BlessingName(string id) => id switch { "edge" => "锋锐回响", "bastion" => "残留壁垒", "surge" => "魔力潮汐", _ => "尚无祝福" };
    public static string BlessingText(string id) => id switch { "edge" => "攻击牌伤害 +2", "bastion" => "每个玩家回合获得 3 护盾", "surge" => "每回合额外恢复 1 魔法点", _ => "临时祝福仅在本次远征有效" };
    public bool ChooseBlessing(string id)
    {
        if (State.Phase != ExpeditionPhase.Reward || !State.BlessingOffers.Contains(id)) return false;
        State.Blessing = id; State.BlessingOffers.Clear(); State.Phase = ExpeditionPhase.Exploring;
        Note($"获得临时祝福：{BlessingName(id)}。{BlessingText(id)}。"); return true;
    }

    private void Advance(bool wasLocked)
    {
        State.Steps++;
        if (State.Flags.Contains("patrol")) return;
        State.PatrolEta = Math.Max(0, State.PatrolEta - 1);
        if (State.Location != "square") { State.Locked = false; return; }
        if (wasLocked)
        {
            StartBattle("patrol", "无视锁定预警：被动拦截，巡逻首回合护盾 +4", false, true);
            return;
        }
        if (State.PatrolEta == 0)
        {
            State.Locked = true;
            Note("预警：巡逻已锁定你！再留在广场行动一次将被接战；现在仍可离开或主动迎战。");
        }
    }

    private void StartBattle(string encounter, string reason, bool prepared, bool ambushed = false)
    {
        State.Phase = ExpeditionPhase.Battle; State.Encounter = encounter; State.BattleReason = reason;
        State.Enemies.Clear();
        void Enemy(string id, string name, int hp, int damage, int block = 0) => State.Enemies.Add(new() { Id = id, Name = name, Health = hp, MaxHealth = hp, Damage = damage + ThreatTier, Block = block });
        if (encounter == "convoy")
        {
            Enemy("convoy", "运输队护卫", 22, 6);
            if (State.PatrolEta == 0 && !State.Flags.Contains("patrol")) Enemy("patrol", "广场巡逻", 16, 5);
        }
        if (encounter == "patrol") Enemy("patrol", "广场巡逻", 16, 5, ambushed ? 4 : 0);
        if (encounter == "boss")
        {
            Enemy("boss", "铸炉守卫", 38, 8, 5);
            if (!State.Flags.Contains("alarm")) Enemy("support", "警钟护卫", 14, 4);
        }
        State.Draw = Deck(); Shuffle(State.Draw);
        if (prepared)
        {
            var card = State.Weapon == "" ? "punch" : ExpeditionContent.Gear[State.Weapon].Cards[0];
            State.Draw.Remove(card); State.Draw.Insert(0, card);
        }
        State.Prepared = false; State.Locked = false;
        State.Hand.Clear(); State.Discard.Clear(); State.Round = 0; State.Mana = 1;
        BeginTurn();
        Note($"进入战斗：{reason}。{State.Enemies.Count} 名敌人，外部局势暂停。");
    }

    private void BeginTurn()
    {
        State.Round++; State.ActionPoints = 3;
        State.Mana = Math.Min(3, State.Mana + (State.Blessing == "surge" ? 2 : 1));
        State.Block = State.Blessing == "bastion" ? 3 : 0;
        for (var i = 0; i < 5; i++)
        {
            if (State.Draw.Count == 0)
            {
                State.Draw.AddRange(State.Discard); State.Discard.Clear(); Shuffle(State.Draw);
            }
            if (State.Draw.Count == 0) break;
            State.Hand.Add(State.Draw[0]); State.Draw.RemoveAt(0);
        }
    }

    public bool PlayCard(int index, int target)
    {
        if (State.Phase != ExpeditionPhase.Battle || index < 0 || index >= State.Hand.Count) return false;
        var id = State.Hand[index]; var card = ExpeditionContent.Cards[id];
        if (State.ActionPoints < card.ActionCost || State.Mana < card.ManaCost) return false;
        if (card.Damage > 0 && (target < 0 || target >= State.Enemies.Count || State.Enemies[target].Health <= 0)) return false;
        State.ActionPoints -= card.ActionCost; State.Mana -= card.ManaCost;
        if (card.Damage > 0)
        {
            var enemy = State.Enemies[target];
            var damage = card.Damage + (id == "riposte" ? State.Block : 0) + (State.Blessing == "edge" ? 2 : 0);
            var absorbed = id == "cleave" ? 0 : Math.Min(enemy.Block, damage);
            enemy.Block -= absorbed; enemy.Health = Math.Max(0, enemy.Health - (damage - absorbed));
            Note($"{card.Name} → {enemy.Name}，造成 {damage - absorbed} 伤害。");
        }
        State.Block += card.Block;
        if (id == "spark") State.Mana = Math.Min(3, State.Mana + 1);
        State.Hand.RemoveAt(index); State.Discard.Add(id);
        if (State.Enemies.All(e => e.Health <= 0)) WinBattle();
        return true;
    }

    public bool EndTurn(bool retreat = false)
    {
        if (State.Phase != ExpeditionPhase.Battle) return false;
        State.Discard.AddRange(State.Hand); State.Hand.Clear();
        foreach (var enemy in State.Enemies.Where(e => e.Health > 0))
        {
            var absorbed = Math.Min(State.Block, enemy.Damage);
            State.Block -= absorbed; State.Health = Math.Max(0, State.Health - enemy.Damage + absorbed);
            Note($"{enemy.Name}攻击：{enemy.Damage - absorbed} 生命损失。");
            if (State.Health == 0) { Defeat(); return true; }
            enemy.Block = enemy.Id == "boss" ? 5 : 0;
        }
        if (retreat)
        {
            State.Materials = Math.Max(0, State.Materials - 3);
            State.Location = "gate"; State.Phase = ExpeditionPhase.Exploring;
            State.Locked = false; State.Prepared = false;
            ClearBattle(); Note("撑过敌方行动后撤回入口，损失最多 3 材料；敌人未被击败，下次挑战恢复生命。");
        }
        else BeginTurn();
        return true;
    }

    private void WinBattle()
    {
        foreach (var enemy in State.Enemies) if (enemy.Id != "support") State.Flags.Add(enemy.Id);
        var reward = State.Encounter == "boss" ? "bracer" : State.Encounter == "convoy" ? "breaker" : "";
        if (reward != "") State.Loot.Add(reward);
        State.Materials += State.Encounter == "boss" ? 8 : 3;
        State.Phase = ExpeditionPhase.Exploring;
        Note($"战斗胜利。{(reward == "" ? "" : ExpeditionContent.Gear[reward].Name + "已放入战利品，")}收获需撤离才能保留。");
        ClearBattle();
    }

    private void Extract()
    {
        var lootCount = State.Loot.Count; var materials = State.Materials;
        foreach (var item in State.Loot)
        {
            if (State.Owned.Contains(item)) State.Materials += 3;
            else State.Owned.Add(item);
        }
        var duplicates = State.Materials - materials;
        State.StoredMaterials += State.Materials; State.Extractions++;
        State.Summary = $"成功撤离 · 带回 {lootCount} 件装备、{materials} 材料。" + (duplicates > 0 ? $"重复装备回收为 {duplicates} 材料。" : "") + "临时祝福已清除。";
        State.Loot.Clear(); State.Materials = 0; State.Blessing = ""; State.Prepared = false;
        State.Phase = ExpeditionPhase.Extracted; Note(State.Summary);
    }

    private void Defeat()
    {
        State.Summary = $"远征失败 · 丢失 {State.Loot.Count} 件未撤离装备和 {State.Materials} 材料。绑定装备、仓库和调查情报保留。";
        State.Loot.Clear(); State.Materials = 0; State.Blessing = ""; State.Prepared = false;
        if (!State.Owned.Contains(State.Weapon)) State.Weapon = "hammer";
        if (!State.Owned.Contains("bracer")) State.WearBracer = false;
        State.Phase = ExpeditionPhase.Defeated; ClearBattle(); Note(State.Summary);
    }

    public bool ReturnToCamp()
    {
        if (State.Phase is not (ExpeditionPhase.Extracted or ExpeditionPhase.Defeated)) return false;
        State.Phase = ExpeditionPhase.Camp; State.Health = MaxHealth; State.Location = "gate";
        return true;
    }

    private void ClearBattle()
    {
        State.Enemies.Clear(); State.Draw.Clear(); State.Hand.Clear(); State.Discard.Clear();
        State.Block = 0; State.ActionPoints = 0; State.Mana = 0;
    }
    private int Next(int max)
    {
        var value = State.RandomState; value ^= value << 13; value ^= value >> 17; value ^= value << 5;
        State.RandomState = value; return (int)(value % (uint)max);
    }
    private void Shuffle<T>(List<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--) { var j = Next(i + 1); (list[i], list[j]) = (list[j], list[i]); }
    }
    private void Note(string message)
    {
        State.Log.Add(message);
        if (State.Log.Count > 60) State.Log.RemoveAt(0);
    }
}
