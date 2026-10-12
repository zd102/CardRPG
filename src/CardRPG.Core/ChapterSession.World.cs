namespace CardRPG.Core;

public sealed partial class ChapterSession
{
    public bool IsRevealed(string id) => ChapterContent.Places.ContainsKey(id) && id switch
    { "A06" => State.World.Contains("dark-door"), "A07" => State.Done.Contains("Q01"), _ => true };
    public bool Depart()
    {
        if (State.Phase != ChapterPhase.Camp) return false;
        State.Phase = ChapterPhase.Exploring; State.Runs++; State.Health = MaxHealth; State.Supplies = 2;
        State.Location = State.Previous = State.Start; State.Pressure = 0; State.Cargo = [0, 0, 0];
        State.Items.Clear(); State.Loot.Clear(); State.Run.Clear(); State.Paused.Clear(); State.Battle = null;
        foreach (var item in State.CarryItems) { State.StoredItems.Remove(item); State.Items.Add(item); }
        State.CarryItems.Clear();
        State.Resonance = ""; State.PatrolEta = -1; State.PatrolPlace = ""; State.Warned = false;
        State.Calibrated = false; State.CrateSupplyHeld = false;
        State.DepartureMods = new(State.Mods);
        State.DepartureGear = new() { ["weapon"] = State.Weapon, ["body"] = State.Body, ["hands"] = State.Hands };
        var i = 0; foreach (var backup in State.Backups) State.DepartureGear["backup" + ++i] = backup;
        Note($"第 {State.Runs} 次远征。当前目标：{Objective}"); return true;
    }
    public bool Move(string destination)
    {
        if (State.Phase != ChapterPhase.Exploring || !ChapterContent.Places[State.Location].Neighbors.Contains(destination) || !IsRevealed(destination)) return false;
        State.Previous = State.Location; State.Location = destination; State.Warned = false;
        if (destination == "A03" && !State.World.Contains("robot-rescued")) Note("咚、咚、咚。废铁下面的小维修偶正在敲击夹具，向你求救。");
        if (destination == "A06") State.Intel.Add("glowing-manifest");
        Note("抵达" + ChapterContent.Places[destination].Name + "。移动 1 步。"); Advance(); return true;
    }
    public string Objective => !State.Done.Contains("Q01") ? "废料场取继电器 → 撤离 → 据点交付"
        : !State.Done.Contains("Q02") ? "广场取夹具 → 工坊校准 → 撤离交付"
        : !State.Done.Contains("S01") ? "修复货站并带回路签，准备前进站"
        : "外环任务已完成；可继续发现暗室、居民包裹与工坊试用";
    public string PressureText => State.Pressure >= 26 ? "收紧 · 新精英额外 4 护盾"
        : State.Pressure >= (State.Extractions == 0 ? 24 : 18) ? "巡查 · 关注巡逻预警"
        : State.Pressure >= 10 ? "戒备 · 转运箱正在撤走" : "平静 · 没有活动巡逻";
    public string PatrolText => State.Run.Contains("K09") ? "本次巡逻已清除，不再增援"
        : State.Warned ? "巡逻已抵达！移动可以离开；继续操作会被拦截"
        : State.PatrolPlace != "" ? "巡逻位于" + ChapterContent.Places[State.PatrolPlace].Name + "，沿广场与废料场巡查"
        : State.PatrolEta >= 0 ? $"巡逻将在 {State.PatrolEta} 次区域行动后抵达；当前可先撤离" : "尚无巡逻 · 阅读与战斗思考不耗区域步数";
    public (string Place, int Distance) NearestExit()
    {
        var queue = new Queue<(string, int)>(); var seen = new HashSet<string> { State.Location };
        queue.Enqueue((State.Location, 0));
        while (queue.Count > 0)
        {
            var (place, n) = queue.Dequeue();
            if (place == "A01" || place == "A05" && State.World.Contains("station")) return (place, n);
            foreach (var next in ChapterContent.Places[place].Neighbors.Where(IsRevealed))
                if (seen.Add(next)) queue.Enqueue((next, n + 1));
        }
        throw new InvalidOperationException("区域缺少安全出口。");
    }
    private bool NeedItem(string item, string quest) => !State.Done.Contains(quest) && !State.Items.Contains(item) && !State.StoredItems.Contains(item);
    public List<ExpeditionAction> Actions()
    {
        if (State.Phase != ChapterPhase.Exploring) return [];
        var a = new List<ExpeditionAction>();
        void Offer(string id, string name, string description, bool when = true, bool enabled = true)
        { if (when) a.Add(new(id, name, description, enabled)); }
        void Once(string id, string name, string description, bool when = true) => Offer(id, name, description, when && !State.Run.Contains(id));
        switch (State.Location)
        {
            case "A01": Offer("extract", "带着收获撤离", "免费返回余火站。所有材料、任务物与新装备入库。"); break;
            case "A02":
                Offer("clamp", "取出工坊夹具", "1 步。取得 T02；需带到维修工坊校准后再撤离。", State.Done.Contains("Q01") && NeedItem("T02", "Q02"));
                Once("K01", "迎战运输队", "1 步开战。搬运傀儡＋铆盾守卫；胜利得废钢 1、精密弹簧 1。可绕开，不保护主线夹具。");
                Offer("crate", State.Run.Contains("crate-start") ? "继续开箱 · 2/2" : "打开货箱 · 1/2", "每阶段 1 步并额外产生 1 压力。完成得废钢 2、精密弹簧 1；第二步前可离开。", !State.Run.Contains("crate"));
                Offer("E13", "挑战军需领班", "领班 48 生命／4 护盾，另有铆盾守卫。第 3 次敌方阶段后额外弹簧运走；破甲锤首奖不会消失。", State.Done.Contains("Q01") && !State.World.Contains("leader-won"));
                Offer("leader-tool", "取回领班工具箱", "1 步。重新领取尚未登记的破甲锤，无需再战。", State.World.Contains("leader-won") && !State.Owned.Contains("G03") && !State.Loot.Contains("G03"));
                Offer("transfer", "取走转运箱", "1 步。额外废钢 1；压力达到 10 时开始 3 步撤走预告，不影响普通货箱。", !State.Run.Contains("transfer") && State.Pressure < 13);
                break;
            case "A03":
                Offer("relay", "拿走封装继电器", "1 步。主线物件在安全侧，无需先战斗；带回据点才可交付。", NeedItem("T01", "Q01"));
                Offer("rescue", "扳开夹具，救出维修偶", "1 步，无战斗。小维修偶自行前往营地；你成功撤离后可接它进站。", !State.World.Contains("robot-rescued"));
                Once("scrap", "拆解废机", State.FirstSalvageBanked ? "1 步，废钢 3。" : "1 步，首次安全带回前固定废钢 4、导能线 2。");
                Once("supplies", "搜索补给堆", "1 步，获得 1 份补给；最多携带 3 份。", State.Supplies < 3);
                Once("K02", "观察傀儡并迎战", "搬运傀儡：攻击 6 → 抬臂准备 → 重砸 10。胜利得废钢 1。可以先取任务物。");
                break;
            case "A04":
                Once("bench", "启动换装台架", "1 步。可换出发携带的装备和本次新装备；换装本身免费。");
                Offer("calibrate", "校准工坊夹具", "1 步。校准状态随 T02 保存；需将它安全带回。", State.Items.Contains("T02") && !State.Calibrated, State.Run.Contains("bench"));
                Offer("dark", "关闭检修灯", "1 步，永久显露荧光夹层。不关闭换装台或撤离点。", State.Intel.Contains("relay-seen") && !State.World.Contains("dark-door"));
                Offer("R01", "共鸣：余烬护层", "1 步。每场首回合获得 5 护盾；外环最多激活一种共鸣。", State.Resonance == "");
                Offer("R07", "共鸣：厚壁余温", "1 步。每回合首次单次获得至少 7 护盾，额外获得 2。", State.Resonance == "");
                break;
            case "A05":
                Offer("repair", "永久修复货站线路", "1 步，使用现场工具，不收费。本次立即可从这里撤离。", State.Done.Contains("Q01") && !State.World.Contains("station"));
                Offer("sign", "领取货站路签", "1 步。带回交付可解锁从货站出征。", State.World.Contains("station") && NeedItem("T11", "S01"));
                Once("wire", "搜索线缆柜", "1 步，导能线 2。", State.World.Contains("station"));
                Offer("extract", "乘升降车撤离", "安全带回所有收获，不消耗行动。", true, State.World.Contains("station"));
                break;
            case "A06":
                Offer("shovel", "查看荧光货单与工具", "1 步。发现折页清障铲：新增横扫，但比锤击更耗行动。带回或到 A04 试装。", !State.World.Contains("shovel-inspected") || !State.Owned.Contains("G25") && !State.Loot.Contains("G25"));
                break;
            case "A07":
                Once("train-inspect", "检查无人车厢", "1 步。列车已停机；工具、补给、居民包裹只先取一项，没有守门战。");
                if (State.Run.Contains("train-inspect") && !State.CrateChoiceBanked && !State.Run.Contains("train-choice"))
                {
                    Offer("train-wire", "选择导能线 ×2", "1 步。与补给、包裹互斥。本次带回后不重复发奖；包裹下一趟仍可取。", State.CrateSelection is "" or "wire");
                    Offer("train-supply", "选择一份补给", "1 步。补给使用或安全带回后不再补发；包裹下一趟仍可取。", State.CrateSelection is "" or "supply", State.Supplies < 3);
                    Offer("train-parcel", "选择居民包裹", "1 步。带回交给居民，替代本次物资奖励。", State.CrateSelection is "" or "parcel" && NeedItem("T14", "S09"));
                }
                Offer("package", "取回留在货架上的包裹", "1 步。上次留在车厢的包裹还在；此前的物资选择不让支线永久错过。", State.CrateSelection is "wire" or "supply" && !State.Run.Contains("train-choice") && NeedItem("T14", "S09"));
                break;
        }
        Offer("K09", "主动迎战巡逻", "警铃巡视者＋搬运傀儡；巡视者第三次行动呼叫一名傀儡。全局本趟只来一支巡逻。", State.Warned && !State.Run.Contains("K09"));
        Offer("heal", $"使用补给 · {State.Supplies}", "1 步，恢复 14 生命。若巡逻已锁定，继续停留将先被拦截。", true, State.Supplies > 0 && State.Health < MaxHealth);
        return a;
    }
    public bool Act(string id)
    {
        if (!Actions().Any(a => a.Id == id && a.Enabled)) return false;
        if (id == "extract") { Extract(); return true; }
        if (State.Warned && id is not ("K01" or "K02" or "E13" or "K09"))
        { State.Pressure++; StartBattle("K09"); Note("巡逻打断了操作，尚未扣除补给或生成奖励。可在战后继续。"); return true; }
        if (id is "K01" or "K02" or "E13" or "K09")
        { Advance(false); StartBattle(id); return true; }
        var noise = 0;
        switch (id)
        {
            case "relay": State.Items.Add("T01"); State.Intel.Add("relay-seen"); Note("继电器已装进背包。回程的路就在身后，也可以看看敲击声来自哪里。"); break;
            case "clamp": State.Items.Add("T02"); Note("取得夹具。带到维修工坊校准，再撤离交付。"); break;
            case "calibrate": State.Calibrated = true; Note("夹具校准完成。现在需要把它安全带回。"); break;
            case "rescue": State.World.Add("robot-rescued"); Note("夹具松开了。小维修偶朝你挥了挥扳手，慢慢走向入口营地。"); break;
            case "scrap": Add(State.Cargo, State.FirstSalvageBanked ? 3 : 4, State.FirstSalvageBanked ? 0 : 2, 0); State.Run.Add(id); Note("废机拆解完成，材料尚未入库。"); break;
            case "supplies": State.Supplies++; State.Run.Add(id); Note("找到一份密封补给。"); break;
            case "bench": State.Run.Add(id); Note("台架启动。现在可打开装备页，试用随身装备。"); break;
            case "dark": State.World.Add("dark-door"); Note("灯熄了，墙后却亮起一道荧光门。检修夹层 A06 已显露。"); break;
            case "shovel": State.World.Add("shovel-inspected"); GiveGear("G25"); Note("清障铲折开时露出宽大的刃面。它可以横扫多个目标；回 A04 就能试装。"); break;
            case "repair": State.World.Add("station"); Note("货站重新通电！这里现在可以安全撤离，永久有效。"); break;
            case "sign": State.Items.Add("T11"); Note("领到路签，带回交付后可以从货站直接出发。"); break;
            case "wire": Add(State.Cargo, 0, 2, 0); State.Run.Add(id); break;
            case "crate":
                noise = 1;
                if (!State.Run.Add("crate-start")) { Add(State.Cargo, 2, 0, 1); State.Run.Add("crate"); Note("货箱打开：废钢 2、精密弹簧 1。"); }
                else Note("箱锁已撬开一半，金属声增加了压力。可以先离开，稍后继续。");
                break;
            case "leader-tool": GiveGear("G03"); break;
            case "transfer": State.Run.Add(id); Add(State.Cargo, 1, 0, 0); Note("抢在转运之前取得额外废钢。"); break;
            case "R01": case "R07": State.Resonance = id; Note("临时共鸣已激活，离开灰区即消失。"); break;
            case "train-inspect": State.Run.Add(id); Note("空车厢里还有三样东西。选之前可以比较，物资与居民包裹各有用途。"); break;
            case "train-wire": case "train-supply": case "train-parcel":
                State.CrateSelection = id[6..]; State.Run.Add("train-choice");
                if (id == "train-wire") Add(State.Cargo, 0, 2, 0);
                else if (id == "train-supply") { State.Supplies++; State.CrateSupplyHeld = true; }
                else State.Items.Add("T14");
                Note("列车选择已记录。另一份居民包裹不会因此永久消失。"); break;
            case "package": State.Items.Add("T14"); Note("带上那份迟到的包裹，收件人也许就在余火站。"); break;
            case "heal":
                State.Supplies--; State.Health = Math.Min(MaxHealth, State.Health + 14);
                if (State.CrateSupplyHeld) { State.CrateSupplyHeld = false; State.CrateChoiceBanked = true; }
                Note("补给恢复 14 生命。"); break;
        }
        Advance(true, noise); return true;
    }
    private void Advance(bool movePatrol = true, int noise = 0)
    {
        var before = State.Pressure; State.Pressure += 1 + noise;
        if (before < 10 && State.Pressure >= 10) Note("戒备：广场转运箱将在压力 13 时撤走，普通货箱与任务物保留。");
        var threshold = State.Extractions == 0 ? 24 : 18;
        if (!State.Run.Contains("K09"))
        {
            if (State.PatrolEta == -1 && State.Pressure >= threshold)
            { State.PatrolEta = 2; Note("巡逻出发了。至少还有两次区域行动，可以先走向出口。"); }
            else if (movePatrol && State.PatrolEta > 0)
            {
                State.PatrolEta--;
                if (State.PatrolEta == 0) State.PatrolPlace = "A02";
            }
            else if (movePatrol && State.PatrolPlace != "")
                State.PatrolPlace = State.PatrolPlace == "A02" ? "A03" : "A02";
        }
        State.Warned = State.PatrolPlace == State.Location && !State.Run.Contains("K09");
        if (State.Warned) Note("巡逻到了！现在移动可以离开；继续停留操作会被拦截。");
    }
    private void GiveGear(string id)
    {
        if (!State.Owned.Contains(id) && State.Loot.Add(id)) Note("获得" + ChapterContent.Gear[id].Name + "（未登记，需撤离）。");
    }
    private void Extract()
    {
        var names = State.Loot.Select(g => ChapterContent.Gear[g].Name).Concat(State.Items.Select(ChapterContent.ItemName)).ToList();
        var materialCount = State.Cargo.Sum();
        foreach (var gear in State.Loot) State.Owned.Add(gear);
        foreach (var item in State.Items) State.StoredItems.Add(item);
        for (var i = 0; i < 3; i++) State.Bank[i] += State.Cargo[i];
        if (State.Calibrated && State.Items.Contains("T02")) State.StoredCalibration = true;
        if (State.Run.Contains("scrap")) State.FirstSalvageBanked = true;
        if (State.Run.Contains("train-choice")) State.CrateChoiceBanked = true;
        if (State.World.Contains("robot-rescued")) State.World.Add("robot-arrived");
        State.Owned.UnionWith(["G01", "G02"]); State.Extractions++;
        State.Summary = $"安全带回 {materialCount} 份材料。" + (names.Count == 0 ? "" : "\n" + string.Join("、", names)) + "\n回到据点后可以交付任务、配装与制作。";
        FinishRun(ChapterPhase.Extracted); Note(State.Summary);
    }
    private void Defeat()
    {
        State.Weapon = State.DepartureGear.GetValueOrDefault("weapon", "G01");
        State.Body = State.DepartureGear.GetValueOrDefault("body", "G09");
        State.Hands = State.DepartureGear.GetValueOrDefault("hands", "");
        State.Summary = "未撤离的新装备、材料与任务物已丢失。\n已登记装备、仓库、救援和永久修复保留；任务物可在原来源重取。";
        FinishRun(ChapterPhase.Defeated); Note(State.Summary);
    }
    private void FinishRun(ChapterPhase phase)
    {
        State.Phase = phase; State.Items.Clear(); State.Loot.Clear(); State.Cargo = [0, 0, 0];
        State.Resonance = ""; State.Battle = null; State.Paused.Clear(); State.Calibrated = false; State.CrateSupplyHeld = false; State.Warned = false;
    }
    public bool ReturnToCamp()
    {
        if (State.Phase is not (ChapterPhase.Extracted or ChapterPhase.Defeated)) return false;
        State.Phase = ChapterPhase.Camp; State.Health = MaxHealth; return true;
    }
    public bool QuestOpen(string id) => id switch
    {
        "Q01" or "S07" => true, "Q02" or "S01" or "S09" => State.Done.Contains("Q01"),
        "S08" => State.Intel.Contains("relay-seen"), "S10" => State.Done.Contains("Q02"), _ => false
    };
    public bool CanTurnIn(string id) => State.Phase == ChapterPhase.Camp && QuestOpen(id) && !State.Done.Contains(id) && id switch
    {
        "Q01" => State.StoredItems.Contains("T01"),
        "Q02" => State.StoredItems.Contains("T02") && State.StoredCalibration,
        "S01" => State.World.Contains("station") && State.StoredItems.Contains("T11"),
        "S07" => State.World.Contains("robot-arrived"),
        "S08" => State.World.Contains("shovel-inspected") && State.Owned.Contains("G25"),
        "S09" => State.StoredItems.Contains("T14"), _ => false
    };
    public string QuestStatus(string id) => State.Done.Contains(id) ? "已完成" : id == "S10" && State.World.Contains("trial-complete") ? "试用已完成 · 剧场后续尚未开放"
        : !QuestOpen(id) ? "尚未开放" : CanTurnIn(id) ? "可在据点交付" : "进行中";
    public bool TurnIn(string id)
    {
        if (!CanTurnIn(id)) return false;
        State.Done.Add(id);
        switch (id)
        {
            case "Q01":
                State.StoredItems.Remove("T01"); State.Owned.Add("G10"); Add(State.Bank, 2, 0, 2);
                Note("工作灯亮了，墙上浮现旧日晴天的投影。有人认出了公寓屋顶的菜园。岑砧交给你修补工装，工坊夹具任务开放。"); break;
            case "Q02":
                State.StoredItems.Remove("T02"); State.StoredCalibration = false; State.Owned.Add("G15"); Add(State.Bank, 4, 0, 2);
                Note("岑砧点了点头：这回是你自己的工具。回响护腕、手部位置、制作和改装开放。"); break;
            case "S01": State.StoredItems.Remove("T11"); Add(State.Bank, 2, 2, 0); Note("洛舟登记了路签。以后可以直接从外环货站出发。"); break;
            case "S07": Add(State.Bank, 1, 1, 0); Note("小维修偶在工作台旁安了家。它朝一张荧光货单比划着，似乎想告诉你工坊墙后的秘密。"); break;
            case "S08": Note("你把荧光货单挂在工坊墙上：原来清障工具也能成为武器。"); break;
            case "S09": State.StoredItems.Remove("T14"); Add(State.Bank, 0, 0, 2); Note("失主拆开包裹，里面是修补过的工作服。余火站的衣架上，多了一抹温暖的颜色。"); break;
        }
        if (SliceComplete) Note("外环主线已完成，货站也已开通。可以继续探索支线与试装；水务区将在后续切片开放。");
        return true;
    }
}
