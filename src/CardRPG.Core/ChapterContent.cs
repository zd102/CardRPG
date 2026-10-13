namespace CardRPG.Core;

public record ChapterCard(string Id, string Name, int Action, int Mana, int Damage, int Shield, string Text);
public record ChapterGear(string Id, string Name, string Slot, string[] Cards, string Hint);
public record ChapterItemDefinition(string Id, string Name, int Slots, int Stack, int Grams);
public record ChapterPlace(string Id, string Name, string Description, string[] Neighbors);
public record ChapterRecipe(string Gear, int Steel, int Wire, int Spring);
public record ChapterQuest(string Id, string Name, string Hint);

/// <summary>First chapter outer-ring slice. IDs are separate from the prototype catalogue.</summary>
public static class ChapterContent
{
    public static readonly Dictionary<string, ChapterCard> Cards = new()
    {
        ["C01"] = new("C01", "锤击", 1, 0, 8, 0, "对目标造成 8 伤害。"),
        ["C02"] = new("C02", "架盾猛击", 1, 0, 5, 4, "造成 5 伤害，然后获得 4 护盾。"),
        ["C03"] = new("C03", "坚守", 1, 0, 0, 9, "获得 9 护盾。"),
        ["C04"] = new("C04", "电弧", 0, 1, 9, 0, "造成 9 伤害。"),
        ["C05"] = new("C05", "导能打击", 1, 0, 5, 0, "造成 5 伤害，然后恢复 1 魔法。"),
        ["C06"] = new("C06", "破甲重击", 2, 0, 17, 0, "造成 17 穿盾伤害，直接扣生命。"),
        ["C07"] = new("C07", "盾压", 1, 0, 4, 0, "消耗最多 8 护盾，造成 4＋实际消耗量的伤害。"),
        ["C18"] = new("C18", "横扫", 2, 0, 10, 0, "对每个存活敌人造成 10 伤害。"),
        ["C19"] = new("C19", "盾刃", 1, 0, 6, 3, "造成 6 伤害，然后获得 3 护盾。"),
        ["C25"] = new("C25", "稳固", 1, 0, 0, 7, "获得 7 护盾。下回合最多保留 4 剩余护盾。"),
        ["C26"] = new("C26", "应急加固", 1, 0, 0, 6, "获得 6 护盾，额外抽 1 张。每回合最多额外抽 2 张。"),
        ["C35"] = new("C35", "掩护射击", 1, 0, 5, 5, "造成 5 伤害，然后获得 5 护盾。"),
        ["C36"] = new("C36", "预备工具", 1, 0, 0, 0, "额外抽 2 张。本回合所有额外抽牌合计最多 2 张。")
    };
    public static readonly Dictionary<string, ChapterGear> Gear = new()
    {
        ["G01"] = new("G01", "守备铁锤", "weapon", ["C01", "C01", "C01", "C02"], "初始工具；稳定单体攻击。"),
        ["G02"] = new("G02", "导能短杖", "weapon", ["C04", "C04", "C05", "C05"], "初始工具；用行动回魔法，再释放电弧。"),
        ["G03"] = new("G03", "熔铸破甲锤", "weapon", ["C06", "C06", "C01", "C02"], "货运广场军需领班首胜，或工坊制作。"),
        ["G09"] = new("G09", "铆钉护甲", "body", ["C03", "C03", "C03", "C02"], "初始防具；高盾但没有过牌。"),
        ["G10"] = new("G10", "修补工装", "body", ["C03", "C03", "C25", "C26"], "交付继电器；保留护盾与抽牌。"),
        ["G15"] = new("G15", "回响护腕", "hands", ["C07", "C07"], "交付校准夹具；防御转换为进攻。"),
        ["G25"] = new("G25", "折页清障铲", "weapon", ["C01", "C01", "C18", "C02"], "熄灯后显现的检修夹层；群攻会占用 2 行动。"),
        ["G26"] = new("G26", "检修工具手套", "hands", ["C36", "C35"], "先完成据点台架试用，再制作。以盾压换取过牌。"),
        ["BP01"] = new("BP01", "拼布挎包", "backpack", [], "增加 8 格，总容量 12 格；简易缝制从开局可用。"),
        ["BP02"] = new("BP02", "加固行囊", "backpack", [], "增加 12 格，总容量 16 格；完成夹具任务后可制作。"),
        ["BP03"] = new("BP03", "承架背包", "backpack", [], "增加 16 格，总容量 20 格；外环整备完成后可制作。")
    };
    public static readonly Dictionary<string, ChapterPlace> Places = new()
    {
        ["A01"] = new("A01", "入口营地", "暖黄灯下停着回程的货车。带上收获，随时可以回家。", ["A02", "A03"]),
        ["A02"] = new("A02", "货运广场", "货箱与停机吊车排成长列。远处广播正在播报一班没有司机的列车。", ["A01", "A03", "A04", "A07"]),
        ["A03"] = new("A03", "废料场", "废铁堆里传来三下规律的敲击。继电器就在安全侧，不必先打倒傀儡。", ["A01", "A02", "A04", "A05"]),
        ["A04"] = new("A04", "维修工坊", "工作台仍能使用；检修扇逆着气流转动。也许熄灯后能看清墙上的东西。", ["A02", "A03", "A05", "A06"]),
        ["A05"] = new("A05", "外环货站", "修好线路就能从这里撤离。另一侧的水务区仍等待下一阶段开放。", ["A03", "A04"]),
        ["A06"] = new("A06", "荧光检修夹层", "黑暗中，一整张荧光货单在墙上浮现。折页工具还端正地放在最后一格。", ["A04"]),
        ["A07"] = new("A07", "静止列车", "广播准时报站，座位却空无一人。一个写着居民姓名的包裹留在工具箱旁。", ["A02"])
    };
    public static readonly Dictionary<string, ChapterRecipe> Recipes = new()
    {
        ["G03"] = new("G03", 6, 0, 2), ["G10"] = new("G10", 4, 0, 2),
        ["G15"] = new("G15", 3, 0, 2), ["G25"] = new("G25", 4, 0, 2),
        ["G26"] = new("G26", 2, 2, 1)
    };
    public static readonly ChapterQuest[] Quests =
    [
        new("Q01", "让工作灯亮起", "在废料场取得封装继电器，撤离后交给岑砧。"),
        new("Q02", "有自己的工具", "货运广场取夹具 → 维修工坊校准 → 撤离交付。"),
        new("S01", "回程的轨道", "外环货站修复线路并取路签，撤离后交付，开放货站出发。"),
        new("S07", "会敲门的维修偶", "在废料场扳开夹具救援；成功撤离后，在据点接它进站。"),
        new("S08", "灯灭之后", "在维修工坊关闭检修灯，调查荧光夹层并带回清障铲。"),
        new("S09", "迟到的包裹", "在静止列车拿到居民包裹，撤离后交还失主。"),
        new("S10", "给空座位的演出 · 序", "工坊开放后，试用检修手套：用预备工具后再攻击。后续演出在铸造区开放。")
    ];
    public static readonly string[] MaterialIds = ["M01", "M02", "M03", "M07", "M08", "M09", "M10"];
    public static readonly Dictionary<string, ChapterItemDefinition> Items = CreateItems();
    private static Dictionary<string, ChapterItemDefinition> CreateItems()
    {
        var items = new Dictionary<string, ChapterItemDefinition>();
        int[] weights = [4000, 2000, 5000, 6000, 3000, 1000, 4500, 1000, 1000, 1500, 2000];
        var index = 0;
        foreach (var gear in Gear.Values)
            items[gear.Id] = new(gear.Id, gear.Name, gear.Slot == "hands" ? 1 : 2, 1, weights[index++]);
        foreach (var (id, name, grams) in new (string, string, int)[]
        {
            ("M01", "废钢", 600), ("M02", "导能线", 200), ("M03", "精密弹簧", 100),
            ("M07", "再生织物", 100), ("M08", "净化药剂", 100), ("M09", "密封树脂", 200), ("M10", "承重构件", 2000)
        }) items[id] = new(id, name, 1, 10, grams);
        foreach (var (id, name, grams) in new (string, string, int)[]
        { ("T01", "封装继电器", 1200), ("T02", "工坊校准夹具", 2500), ("T11", "货站路签", 100), ("T14", "居民包裹", 1500) })
            items[id] = new(id, name, 1, 1, grams);
        items["U01"] = new("U01", "密封补给", 1, 2, 350);
        return items;
    }
    public static int BagCapacity(string id) => id switch { "BP01" => 8, "BP02" => 12, "BP03" => 16, _ => 0 };
    public static string ItemName(string id) => Items.TryGetValue(id, out var item) ? item.Name : id;
    public static string MaterialName(int i) => ItemName(MaterialIds[i]);
}
