namespace CardRPG.Core;

public record CardSpec(string Id, string Name, int ActionCost, int ManaCost, int Damage, int Block, string Text);
public record GearSpec(string Id, string Name, string Slot, string[] Cards, string Description);
public record PlaceSpec(string Id, string Name, string Description, string[] Neighbors);
public record ExpeditionAction(string Id, string Name, string Description, bool Enabled = true);

/// <summary>Small, explicit prototype catalogue. Gear is the sole source of deck cards.</summary>
public static class ExpeditionContent
{
    public static readonly Dictionary<string, CardSpec> Cards = new()
    {
        ["strike"] = new("strike", "锤击", 1, 0, 8, 0, "对目标造成 8 点伤害"),
        ["bash"] = new("bash", "架盾猛击", 1, 0, 5, 4, "造成 5 点伤害，获得 4 点护盾"),
        ["guard"] = new("guard", "坚守", 1, 0, 0, 9, "获得 9 点护盾"),
        ["bolt"] = new("bolt", "电弧", 0, 1, 9, 0, "消耗魔法点，造成 9 点伤害"),
        ["spark"] = new("spark", "导能打击", 1, 0, 5, 0, "造成 5 点伤害，恢复 1 点魔法"),
        ["cleave"] = new("cleave", "破甲重击", 2, 0, 17, 0, "造成 17 点伤害；忽略敌方护盾"),
        ["riposte"] = new("riposte", "护盾反击", 1, 0, 4, 3, "伤害额外增加当前护盾值，再获得 3 点护盾"),
        ["punch"] = new("punch", "徒手打击", 1, 0, 5, 0, "造成 5 点伤害；无武器时的默认牌")
    };
    public static readonly Dictionary<string, GearSpec> Gear = new()
    {
        ["hammer"] = new("hammer", "守备铁锤", "weapon", ["strike", "strike", "strike", "bash"], "稳定的行动点输出 · 4 张牌"),
        ["staff"] = new("staff", "导能短杖", "weapon", ["bolt", "bolt", "spark", "spark"], "行动与魔法双资源 · 4 张牌"),
        ["armor"] = new("armor", "铆钉护甲", "body", ["guard", "guard", "guard", "bash"], "防御与反击基础 · 4 张牌"),
        ["breaker"] = new("breaker", "熔铸破甲锤", "weapon", ["cleave", "cleave", "strike", "bash"], "高费破甲，适合对抗铸炉守卫 · 4 张牌"),
        ["bracer"] = new("bracer", "回响护腕", "hands", ["riposte", "riposte"], "让护盾成为进攻资源 · 2 张牌")
    };
    public static readonly Dictionary<string, PlaceSpec> Places = new()
    {
        ["gate"] = new("gate", "入口营地", "通往据点的安全出口。撤离不消耗行动；此处不能治疗或换装。", ["square", "scrap"]),
        ["square"] = new("square", "货运广场", "运输队看守货箱。撬开保险箱会产生噪声，吸引巡逻。", ["gate", "scrap", "workshop"]),
        ["scrap"] = new("scrap", "废料场", "残留补给与废弃线路。可绕开广场，修复货运站电源。", ["gate", "square", "workshop", "exit"]),
        ["workshop"] = new("workshop", "临时工坊", "静止的工作台与残留祝福。启用后允许调整装备。", ["square", "scrap", "furnace"]),
        ["furnace"] = new("furnace", "地下熔炉", "铸炉守卫持有回响护腕。关闭警钟可切断它的支援。", ["workshop", "exit"]),
        ["exit"] = new("exit", "货运站", "恢复电源后可从这里撤离，带走全部未入库收获。", ["scrap", "furnace"])
    };
    public static string GearCards(string id) => string.Join("、", Gear[id].Cards.GroupBy(c => c).Select(g => $"{Cards[g.Key].Name} ×{g.Count()}"));
}
