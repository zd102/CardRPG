namespace CardRPG.Core;

public sealed partial class ChapterSession
{
    public bool IsRevealed(string id) => ChapterContent.Places.ContainsKey(id) && id switch
    { "A06" => State.World.Contains("dark-door"), "A07" => State.Done.Contains("Q01"), _ => true };
    public bool Depart()
    {
        if (!CanDepart) return Reject("行囊或仓库空间不足：为出征补给及装备返还留出格位。");
        BeginReport(); BeginInventoryRun();
        State.Phase = ChapterPhase.Exploring; State.Runs++; State.Health = MaxHealth;
        State.Location = State.Previous = State.Start; State.Pressure = 0;
        State.Run.Clear(); State.Paused.Clear(); State.Battle = null;
        State.CarryItems.Clear();
        State.Resonance = ""; State.PatrolEta = -1; State.PatrolPlace = ""; State.Warned = false;
        State.CrateSupplyHeld = false;
        State.DepartureMods = new(State.Mods);
        State.DepartureGear = new() { ["weapon"] = State.Weapon, ["body"] = State.Body, ["hands"] = State.Hands, ["backpack"] = State.Backpack };
        var i = 0; foreach (var backup in State.Backups) State.DepartureGear["backup" + ++i] = backup;
        SyncInventory();
        Note($"第 {State.Runs} 次远征。当前目标：{Objective}"); return true;
    }
    public bool Move(string destination)
    {
        if (State.Phase != ChapterPhase.Exploring || !ChapterContent.Places[State.Location].Neighbors.Contains(destination) || !IsRevealed(destination)) return false;
        var cost = SceneActionCost(); EndFreePickup();
        State.Previous = State.Location; State.Location = destination; State.Warned = false;
        if (destination == "A03" && !State.World.Contains("robot-rescued")) Note("咚、咚、咚。废铁下面的小维修偶正在敲击夹具，向你求救。");
        if (destination == "A06") State.Intel.Add("glowing-manifest");
        Note("抵达" + ChapterContent.Places[destination].Name + $"。行动值 +{cost}。"); Advance(cost: cost); return true;
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
    private bool NeedItem(string item, string quest) => !State.Done.Contains(quest) && !HasPhysical(item);
    public List<ExpeditionAction> Actions()
    {
        if (State.Phase != ChapterPhase.Exploring) return [];
        var a = new List<ExpeditionAction>();
        void Offer(string id, string name, string description, bool when = true, bool enabled = true)
        { if (when) a.Add(new(id, name, description, enabled)); }
        void Once(string id, string name, string description, bool when = true) => Offer(id, name, description, when && !State.Run.Contains(id));
        switch (State.Location)
        {
            case "A01": Offer("extract", "带着收获撤离", "免费返回余火站。仅带回随身实物；地面遗留物会重置，仓满余物保留随身。"); break;
            case "A02":
                Offer("clamp", "取出工坊夹具", "1 步。取得 T02；需带到维修工坊校准后再撤离。", State.Done.Contains("Q01") && NeedItem("T02", "Q02"));
                Once("K01", "迎战运输队", "1 步开战。搬运傀儡＋铆盾守卫；胜利得废钢 1、精密弹簧 1。可绕开，不保护主线夹具。");
                Offer("crate", State.Run.Contains("crate-start") ? "继续开箱 · 2/2" : "打开货箱 · 1/2", "每阶段 1 步并额外产生 1 压力。完成得废钢 2、精密弹簧 1；第二步前可离开。", !State.Run.Contains("crate"));
                Offer("E13", "挑战军需领班", "领班 48 生命／4 护盾，另有铆盾守卫。第 3 次敌方阶段后额外弹簧运走；破甲锤首奖不会消失。", State.Done.Contains("Q01") && !State.World.Contains("leader-won"));
                Offer("leader-tool", "取回领班工具箱", "1 步。重新领取尚未登记的破甲锤，无需再战。", State.World.Contains("leader-won") && !HasPhysical("G03"));
                Offer("transfer", "取走转运箱", "1 步。额外废钢 1；压力达到 10 时开始 3 步撤走预告，不影响普通货箱。", !State.Run.Contains("transfer") && State.Pressure < 13);
                break;
            case "A03":
                Offer("relay", "拿走封装继电器", "1 步。主线物件在安全侧，无需先战斗；带回据点才可交付。", NeedItem("T01", "Q01"));
                Offer("rescue", "扳开夹具，救出维修偶", "1 步，无战斗。小维修偶自行前往营地；你成功撤离后可接它进站。", !State.World.Contains("robot-rescued"));
                Once("scrap", "拆解废机", State.FirstSalvageBanked ? "1 步，废钢 3。" : "1 步，首次安全带回前固定废钢 4、导能线 2。");
                Once("supplies", "搜索补给堆", "1 步，发现普通补给一份；满包留在原位。");
                Once("cloth", "整理工装残片", "1 步，再生织物 2。可带回缝制第一只背包。");
                Once("K02", "观察傀儡并迎战", "搬运傀儡：攻击 6 → 抬臂准备 → 重砸 10。胜利得废钢 1。可以先取任务物。");
                break;
            case "A04":
                Offer("bag-cache", "检查工坊行李柜", "1 步，拼布挎包。没有空间时可留在原柜。", State.Done.Contains("Q02") && !HasPhysical("BP01"));
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
                Once("medicine", "打开站务药匣", "1 步，净化药剂 2。", State.World.Contains("station"));
                Once("frame", "拆卸旧货架", "1 步，承重构件 2。", State.World.Contains("station"));
                break;
            case "A06":
                Offer("shovel", "查看荧光货单与工具", "1 步。发现折页清障铲：新增横扫，但比锤击更耗行动。带回或到 A04 试装。", !State.World.Contains("shovel-inspected") || !HasPhysical("G25"));
                break;
            case "A07":
                Once("train-inspect", "检查无人车厢", "1 步。列车已停机；工具、补给、居民包裹只先取一项，没有守门战。");
                if (State.Run.Contains("train-inspect") && !State.CrateChoiceBanked && !State.Run.Contains("train-choice"))
                {
                    Offer("train-wire", "选择导能线 ×2", "1 步。与补给、包裹互斥。本次带回后不重复发奖；包裹下一趟仍可取。", State.CrateSelection is "" or "wire");
                    Offer("train-supply", "选择一份补给", "1 步。补给使用或安全带回后不再补发；包裹下一趟仍可取。", State.CrateSelection is "" or "supply");
                    Offer("train-parcel", "选择居民包裹", "1 步。带回交给居民，替代本次物资奖励。", State.CrateSelection is "" or "parcel" && NeedItem("T14", "S09"));
                }
                Offer("package", "取回留在货架上的包裹", "1 步。上次留在车厢的包裹还在；此前的物资选择不让支线永久错过。", State.CrateSelection is "wire" or "supply" && !State.Run.Contains("train-choice") && NeedItem("T14", "S09"));
                break;
        }
        Offer("K09", "主动迎战巡逻", "警铃巡视者＋搬运傀儡；巡视者第三次行动呼叫一名傀儡。全局本趟只来一支巡逻。", State.Warned && !State.Run.Contains("K09"));
        Offer("heal", $"使用补给 · {State.Supplies}", "1 步，恢复 14 生命。若巡逻已锁定，继续停留将先被拦截。", true, State.Supplies > 0 && State.Health < MaxHealth);
        return a.Select(x => x with { Description = x.Description + (x.Id == "extract" ? "" : $" 当前行动值 +{SceneActionCost(x.Id == "crate" ? 2 : 1)}。") }).ToList();
    }
    public bool Act(string id)
    {
        if (!Actions().Any(a => a.Id == id && a.Enabled)) return false;
        if (id == "extract") { Extract(); return true; }
        var cost = SceneActionCost(id == "crate" ? 2 : 1);
        EndFreePickup();
        if (State.Warned && id is not ("K01" or "K02" or "E13" or "K09"))
        { Advance(false, cost: cost); StartBattle("K09"); Note("巡逻打断了操作，尚未扣除补给或生成奖励。可在战后继续。"); return true; }
        if (id is "K01" or "K02" or "E13" or "K09")
        { Advance(false, cost: cost); StartBattle(id); return true; }
        switch (id)
        {
            case "relay": SpawnGround(id, "继电器托盘", ("T01", 1)); State.Intel.Add("relay-seen"); break;
            case "clamp": SpawnGround(id, "任务货格", ("T02", 1)); break;
            case "calibrate": State.Stock.Single(i => i.Kind == "T02" && i.Container == "carry").Calibrated = true; SyncInventory(); Note("夹具校准完成。现在需要把它安全带回。"); break;
            case "rescue": State.World.Add("robot-rescued"); Note("夹具松开了。小维修偶朝你挥了挥扳手，慢慢走向入口营地。"); break;
            case "scrap":
                if (State.FirstSalvageBanked) SpawnGround(id, "拆开的废机", ("M01", 3));
                else SpawnGround("scrap-first", "废机首获材料", ("M01", 4 - State.SecuredFirstMaterials.GetValueOrDefault("M01")), ("M02", 2 - State.SecuredFirstMaterials.GetValueOrDefault("M02")));
                State.Run.Add(id); break;
            case "supplies": SpawnGround(id, "密封补给堆", ("U01", 1)); State.Run.Add(id); break;
            case "cloth": SpawnGround(id, "工装残片", ("M07", 2)); State.Run.Add(id); break;
            case "medicine": SpawnGround(id, "站务药匣", ("M08", 2)); State.Run.Add(id); break;
            case "frame": SpawnGround(id, "旧货架构件", ("M10", 2)); State.Run.Add(id); break;
            case "bag-cache": SpawnGround(id, "工坊行李柜", ("BP01", 1)); break;
            case "bench": State.Run.Add(id); Note("台架启动。现在可打开装备页，试用随身装备。"); break;
            case "dark": State.World.Add("dark-door"); Note("灯熄了，墙后却亮起一道荧光门。检修夹层 A06 已显露。"); break;
            case "shovel": State.World.Add("shovel-inspected"); GiveGear("G25"); Note("清障铲折开时露出宽大的刃面。它可以横扫多个目标；回 A04 就能试装。"); break;
            case "repair": State.World.Add("station"); Note("货站重新通电！这里现在可以安全撤离，永久有效。"); break;
            case "sign": SpawnGround(id, "货站路签", ("T11", 1)); break;
            case "wire": SpawnGround(id, "线缆柜", ("M02", 2)); State.Run.Add(id); break;
            case "crate":
                if (!State.Run.Add("crate-start")) { SpawnGround(id, "货箱", ("M01", 2), ("M03", 1), ("M07", 2), ("M09", 1)); State.Run.Add("crate"); }
                else Note("箱锁已撬开一半，金属声增加了压力。可以先离开，稍后继续。");
                break;
            case "leader-tool": GiveGear("G03"); break;
            case "transfer": State.Run.Add(id); SpawnGround(id, "打开的转运箱", ("M01", 1)); break;
            case "R01": case "R07": State.Resonance = id; Note("临时共鸣已激活，离开灰区即消失。"); break;
            case "train-inspect": State.Run.Add(id); Note("空车厢里还有三样东西。选之前可以比较，物资与居民包裹各有用途。"); break;
            case "train-wire": case "train-supply": case "train-parcel":
                State.CrateSelection = id[6..]; State.Run.Add("train-choice");
                if (id == "train-wire") SpawnGround(id, "列车物资", ("M02", 2));
                else if (id == "train-supply") SpawnGround(id, "列车补给", ("U01", 1));
                else SpawnGround(id, "居民包裹", ("T14", 1));
                Note("列车选择已记录。另一份居民包裹不会因此永久消失。"); break;
            case "package": SpawnGround(id, "居民包裹", ("T14", 1)); break;
            case "heal": UseSupply(); break;
        }
        Advance(cost: cost); return true;
    }
    private void Advance(bool movePatrol = true, int cost = 1)
    {
        var before = State.Pressure; State.Pressure = checked(State.Pressure + cost);
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
        if (!HasPhysical(id)) SpawnGround("gear-" + id, ChapterContent.Gear[id].Name, (id, 1));
    }
    private void Extract()
    {
        var previouslyRegistered = State.Owned.ToHashSet();
        BeginReturnReport(true);
        var materialCount = State.Cargo.Sum();
        var left = State.Stock.Count(i => State.Ground.ContainsKey(i.Container) && !i.Registered);
        State.Phase = ChapterPhase.Extracted;
        FinishInventory(true);
        if (State.World.Contains("robot-rescued")) State.World.Add("robot-arrived");
        if (!HasPhysical("G02"))
        {
            var stock = CopyStock(); AddStock(stock, "G02", 1, "warehouse", registered: true);
            if (Fits(stock)) CommitStock(stock);
        }
        State.Extractions++;
        State.LastReport!.NewGear = State.Owned.Except(previouslyRegistered).ToList();
        State.Summary = $"安全带回 {materialCount} 份材料。\n{left} 批未带走物资留在区域，下一趟重置。\n仓库 {WarehouseUsed}/{WarehouseCapacity} 格；随身 {CarryUsed}/{CarryCapacity} 格。仓满余物仍安全随身。";
        FinishRun(ChapterPhase.Extracted); Note(State.Summary);
    }
    private void Defeat()
    {
        BeginReturnReport(false);
        FinishInventory(false);
        State.Summary = "随身材料、补给、任务物与未登记装备已丢失。\n已登记出发装备按原位置恢复；仓库、救援和永久修复保留。";
        FinishRun(ChapterPhase.Defeated); Note(State.Summary);
    }
    private void FinishRun(ChapterPhase phase)
    {
        State.Phase = phase; State.DepartureGear.Clear();
        State.Resonance = ""; State.Battle = null; State.Paused.Clear(); State.CrateSupplyHeld = false; State.Warned = false;
        if (State.LastReport is { BaselineKnown: true } report) report.Changes = State.World.Except(State.DepartureWorld).ToList();
        SyncInventory();
    }
    public bool ReturnToCamp()
    {
        if (State.Phase is not (ChapterPhase.Extracted or ChapterPhase.Defeated)) return false;
        State.Phase = ChapterPhase.Camp; State.Health = MaxHealth; SyncInventory(); return true;
    }
    public bool QuestOpen(string id) => id switch
    {
        "Q01" or "S07" => true, "Q02" or "S01" or "S09" => State.Done.Contains("Q01"),
        "S08" => State.Intel.Contains("relay-seen"), "S10" => State.Done.Contains("Q02"), _ => false
    };
    private bool HasSafeTask(string kind, bool calibrated = false) => State.Stock.Any(i => i.Kind == kind && i.Container is "carry" or "warehouse" && (!calibrated || i.Calibrated));
    private bool QuestReady(string id) => State.Phase == ChapterPhase.Camp && QuestOpen(id) && !State.Done.Contains(id) && id switch
    {
        "Q01" => HasSafeTask("T01"), "Q02" => HasSafeTask("T02", true),
        "S01" => State.World.Contains("station") && HasSafeTask("T11"),
        "S07" => State.World.Contains("robot-arrived"),
        "S08" => State.World.Contains("shovel-inspected") && State.Owned.Contains("G25"),
        "S09" => HasSafeTask("T14"), _ => false
    };
    private List<ChapterItem> QuestResult(string id)
    {
        var stock = CopyStock();
        var task = id switch { "Q01" => "T01", "Q02" => "T02", "S01" => "T11", "S09" => "T14", _ => "" };
        stock.RemoveAll(i => i.Kind == task && i.Container is "carry" or "warehouse");
        var gear = id switch { "Q01" => "G10", "Q02" => "G15", _ => "" };
        if (gear != "" && !HasPhysical(gear)) AddStock(stock, gear, 1, "warehouse", registered: true);
        int[] reward = id switch { "Q01" => [2,0,2], "Q02" => [4,0,2], "S01" => [2,2,0], "S07" => [1,1,0], "S09" => [0,0,2], _ => [0,0,0] };
        for (var n = 0; n < reward.Length; n++) if (reward[n] > 0) AddStock(stock, ChapterContent.MaterialIds[n], reward[n], "warehouse");
        return stock;
    }
    public bool CanTurnIn(string id) => QuestReady(id) && Fits(QuestResult(id));
    public string QuestStatus(string id) => State.Done.Contains(id) ? "已完成" : id == "S10" && State.World.Contains("trial-complete") ? "试用已完成 · 剧场后续尚未开放"
        : !QuestOpen(id) ? "尚未开放" : CanTurnIn(id) ? "可在据点交付" : "进行中";
    public bool TurnIn(string id)
    {
        if (!CanTurnIn(id)) return Reject("任务条件未满足或交付后的奖励没有存放空间，请先整理仓库。");
        var stock = QuestResult(id); State.Done.Add(id); CommitStock(stock);
        Note(id switch
        {
            "Q01" => "工作灯亮了，墙上浮现旧日投影。修补工装入库，夹具任务开放。",
            "Q02" => "回响护腕、手部位置、制作和改装开放。背包与仓库也可以扩充了。",
            "S01" => "洛舟登记了路签，以后可以从外环货站出征。",
            "S07" => "小维修偶在工作台旁安了家。它朝荧光货单比划着。",
            "S08" => "荧光货单挂在了工坊墙上。",
            "S09" => "余火站的衣架上，多了一件修补过的工作服。", _ => "任务已交付。"
        });
        if (SliceComplete) Note("外环主线已完成。可以继续探索、制作背包与整理仓库；水务区将在后续开放。");
        return true;
    }
}
