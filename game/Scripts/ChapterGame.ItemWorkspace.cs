using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private bool _stockBatches, _stockBatchMode;
    private string _stockGearSlot = "";
    private long[] _quantityGroup = [];
    private long _bundleDetailId;
    private static bool IsWornItem(ChapterItem item) => item.Container is "weapon" or "body" or "hands" or "backpack";
    private static Label StockText(string value, int size, Color color)
    {
        var label = Inline(value, size, color); label.AddThemeFontSizeOverride("font_size", (int)Math.Round(size * ReadingScale)); return label;
    }

    // Group presentation only. Source identities and every protection flag remain in the save.
    private (string Kind, string Container, bool Free, bool Calibrated, bool Registered, bool Protected, long Batch) StockGroupKey(ChapterItem item)
        => (item.Kind, item.Container, item.FreeSupply, item.Calibrated, item.Registered, item.KeepOnPerson,
            _stockBatches || _stockPaired || Session.State.Phase != ChapterPhase.Camp ? item.Id : 0);
    private ChapterItem[] StockBundle(ChapterItem item) => _stockBatches || _stockPaired || Session.State.Phase != ChapterPhase.Camp
        ? [item]
        : Session.State.Stock.Where(i => i.Container == item.Container && i.Kind == item.Kind && i.FreeSupply == item.FreeSupply
            && i.Calibrated == item.Calibrated && i.Registered == item.Registered && i.KeepOnPerson == item.KeepOnPerson).OrderBy(i => i.Id).ToArray();

    private void SelectStock(ChapterItem item)
    {
        _selectedStock = item.Id;
        if (ChapterContent.Gear.ContainsKey(item.Kind)) _selectedGear = item.Kind;
        Render();
    }

    private void SelectWearSlot(string slot, string kind)
    {
        _stockGearSlot = _stockGearSlot == slot ? "" : slot;
        _stockFilter = _stockGearSlot == "" ? "all" : "gear"; _stockSearch = "";
        _selectedGear = kind;
        _selectedStock = Session.State.Stock.FirstOrDefault(i => i.Kind == kind && i.Container == slot)?.Id ?? 0;
        Render();
    }

    private void PhysicalInventory(VBoxContainer box)
    {
        if (Session.State.Phase != ChapterPhase.Camp) { _stockBatchMode = false; _stockSelection.Clear(); }
        LegacyCapacityRecovery(box);
        var local = Session.State.Stock.Where(i => i.Container == "carry" || Session.AtBase && i.Container == "warehouse"
            || !Session.AtBase && Session.GroundHere.Any(g => g.Key == i.Container)).ToArray();
        var bar = Row(6); box.AddChild(bar);
        var categories = new[] { ("all", "全部"), ("materials", "材料"), ("gear", "装备"), ("quest", "任务物"), ("supply", "补给") };
        var category = new OptionButton { Name = "StockCategory", CustomMinimumSize = new Vector2(120, 40) };
        foreach (var (key, caption) in categories) category.AddItem(caption + " " + local.Where(i => StockCategory(i, key)).Select(i => i.Kind).Distinct().Count());
        category.Selected = Array.FindIndex(categories, t => t.Item1 == _stockFilter);
        category.ItemSelected += index => { _stockFilter = categories[index].Item1; _stockGearSlot = ""; Render(); }; bar.AddChild(category);
        var search = new LineEdit { Name = "StockSearch", PlaceholderText = "搜索名称或用途 · Enter", Text = _stockSearch, ClearButtonEnabled = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        search.TextSubmitted += value => { _stockSearch = value; Render(); }; bar.AddChild(search);
        bar.AddChild(Button("stock_search", "查找", () => { _stockSearch = search.Text; Render(); }));
        bar.AddChild(Button("stock_sort", "排序：" + (_stockSort switch { "name" => "名称", "quantity" => "数量", "weight" => "重量", _ => "类型" }), () => { _stockSort = _stockSort switch { "kind" => "name", "name" => "quantity", "quantity" => "weight", _ => "kind" }; Render(); }));
        bar.AddChild(QuietButton("storage_view", _stockList ? "图块视图" : "清单视图", () => { _stockList = !_stockList; SavePreferences(); Render(); }));
        bar.AddChild(QuietButton("tools_stock", _expandedTools.Contains("stock") ? "收起工具" : "整理工具", () => { if (!_expandedTools.Remove("stock")) _expandedTools.Add("stock"); SavePreferences(); Render(); }));

        var toolbar = new ScrollContainer { Name = "StockToolbar", CustomMinimumSize = new Vector2(0, 52), HorizontalScrollMode = ScrollContainer.ScrollMode.Auto, VerticalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        box.AddChild(toolbar); var commands = Row(6); toolbar.AddChild(commands);
        var toolsOpen = _expandedTools.Contains("stock");
        if (_stockBatches && !toolsOpen)
            commands.AddChild(QuietButton("storage_batches", "合并同类", () => { _stockBatches = false; Render(); }));
        if (_stockSearch != "" || _stockFilter != "all" || _stockGearSlot != "")
            commands.AddChild(QuietButton("stock_clear", "清除筛选" + (_stockGearSlot == "" ? "" : " · " + SlotName(_stockGearSlot)), () => { _stockFilter = "all"; _stockGearSlot = _stockSearch = ""; Render(); }));
        if (Session.State.Phase == ChapterPhase.Camp)
        {
            var deposit = Session.MaterialDepositPlan(Session.CarriedItems.Select(i => i.Kind));
            commands.AddChild(Button("store_materials", "存入材料", () => RequestBatchMove(deposit, "carry", "warehouse"), deposit.Count > 0));
            commands.AddChild(QuietButton("stock_batch_mode", _stockBatchMode ? "退出多选" : "多选", () => { _stockBatchMode = !_stockBatchMode; if (!_stockBatchMode) _stockSelection.Clear(); Render(); }));
            if (_stockUndoBefore != null && Session.ToJson() == _stockUndoAfter) commands.AddChild(QuietButton("stock_undo", "撤销取放", UndoStock));
        }
        _stockSelection.RemoveWhere(id => !local.Any(i => i.Id == id));
        if (_stockBatchMode)
        {
            var visible = local.Where(StockMatches).ToArray();
            commands.AddChild(QuietButton("stock_select_visible", "全选当前结果", () => { foreach (var i in visible) _stockSelection.Add(i.Id); Render(); }));
            if (_stockSelection.Count > 0)
            {
                commands.AddChild(QuietButton("stock_clear_selection", $"清空已选 {_stockSelection.Count}", () => { _stockSelection.Clear(); Render(); }));
                foreach (var source in new[] { "carry", "warehouse" })
                {
                    var chosen = local.Where(i => i.Container == source && _stockSelection.Contains(i.Id) && !(source == "carry" && i.KeepOnPerson)).ToDictionary(i => i.Id, ChosenQuantity);
                    commands.AddChild(Button("stock_batch_" + source, source == "carry" ? "已选存仓" : "已选带入", () => RequestBatchMove(chosen, source, source == "carry" ? "warehouse" : "carry"), chosen.Count > 0));
                }
                commands.AddChild(QuietButton("stock_review_selection", "查看已选", () => { _detailItemId = 0; _detailTitle = "已选物品"; _detailText = string.Join("\n", local.Where(i => _stockSelection.Contains(i.Id)).Select(i => ChapterContent.ItemName(i.Kind) + $" ×{ChosenQuantity(i)} · {ContainerName(i.Container)}" + (i.KeepOnPerson ? " · 保留" : ""))); OpenNested("detail"); }));
            }
        }
        if (toolsOpen)
        {
            commands.AddChild(QuietButton("gear_mode_equipment", "装备图鉴与比较", () => { _gearFilter = "all"; Render(); }));
            commands.AddChild(QuietButton("storage_batches", _stockBatches ? "合并同类" : "批次明细", () => { _stockBatches = !_stockBatches; Render(); }));
            if (Session.AtBase) commands.AddChild(QuietButton("storage_paired", _stockPaired ? "返回物品区" : "同类对照", () => { _stockPaired = !_stockPaired; _pairedAwaitBatch = false; Render(); }));
            commands.AddChild(QuietButton("storage_loadout", _showStockLoadout ? "收起穿戴" : "展开穿戴", () => { _showStockLoadout = !_showStockLoadout; Render(); }));
            if (_stockSelection.Count > 0)
            {
                commands.AddChild(QuietButton("stock_clear_hidden", "清除筛选外已选", () => { _stockSelection.RemoveWhere(id => !local.Any(i => i.Id == id && StockMatches(i))); Render(); }));
                commands.AddChild(QuietButton("stock_discard_batch", "丢弃已选…", () => PreviewBatchDiscard(local.Where(i => _stockSelection.Contains(i.Id)).ToArray())));
            }
        }
        if (_clickMoveId != 0) commands.AddChild(Button("stock_cancel_target", "取消选择目标", () => { _clickMoveId = 0; Render(); }));
        var selected = Session.State.Stock.FirstOrDefault(i => i.Id == _selectedStock && (local.Contains(i) && StockMatches(i) || IsWornItem(i) && (_stockGearSlot == "" || _stockGearSlot == i.Container)))
            ?? local.FirstOrDefault(i => StockMatches(i) && i.Container.StartsWith("ground:")) ?? local.FirstOrDefault(StockMatches);
        if (_stockPaired && _pairedAwaitBatch) selected = null;
        if (selected != null && !IsWornItem(selected)) selected = StockBundle(selected)[0];
        _selectedStock = selected?.Id ?? 0;
        var columns = Row(12); columns.Name = "StorageWorkspace"; columns.SizeFlagsVertical = SizeFlags.ExpandFill; box.AddChild(columns);
        if (_stockPaired && Session.AtBase) PairedStockTable(columns, local);
        else
        {
            var left = Section(columns, Size.X < 1100 ? .9f : .78f); left.Name = "CarryContainer";
            // At large type the complete left pane scrolls; normal type keeps wear slots fixed.
            var scrollWear = ReadingScale > 1 || Size.Y < 780;
            var carryBody = scrollWear ? Scroll(left, "carry_stock") : left;
            carryBody.AddThemeConstantOverride("separation", 6);
            if (_showStockLoadout)
            {
                carryBody.AddChild(Inline("穿戴装备", 16, Ink));
                var wear = new GridContainer { Name = "CompactLoadout", Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill }; carryBody.AddChild(wear);
                wear.AddThemeConstantOverride("h_separation", 6); wear.AddThemeConstantOverride("v_separation", 6);
                foreach (var slot in new[] { "weapon", "body", "hands", "backpack" })
                {
                    var kind = Session.Equipped.FirstOrDefault(k => ChapterContent.Gear[k].Slot == slot) ?? "";
                    var locked = slot == "hands" && !Session.State.Done.Contains("Q02");
                    var tile = EquipmentTile("wear_" + slot, kind, SlotName(slot) + (locked ? " · 未解锁" : kind == "" ? " · 空槽" : " · 已穿戴"), () => SelectWearSlot(slot, kind), _stockGearSlot == slot, true);
                    tile.CustomMinimumSize = new Vector2(0, 76 * ReadingScale); wear.AddChild(tile);
                    var labels = tile.GetChildren().OfType<Label>().ToArray();
                    foreach (var label in labels) { label.AutowrapMode = TextServer.AutowrapMode.Off; label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; }
                    Dock(tile.GetChild<Control>(0), 6, 4, 42, 40);
                    Dock(labels[0], 50, 4, -6, 26 * ReadingScale, ar: 1);
                    Dock(labels[1], 6, 43 * ReadingScale, -6, 72 * ReadingScale, ar: 1);
                    tile.TooltipText = SlotName(slot) + " · " + (kind == "" ? locked ? "完成前置任务后解锁" : "点击查看可穿戴装备" : ChapterContent.ItemName(kind)) + "\n点击筛选同部位装备";
                    BindEquipmentDrop(tile, slot);
                }
            }
            var carryHeader = Row(6); carryBody.AddChild(carryHeader);
            var carryTitle = Inline("随身背包", 16, Ink); carryTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill; carryHeader.AddChild(carryTitle);
            carryHeader.AddChild(Inline($"{Session.CarryUsed} / {Session.CarryCapacity} 格", 16, Ink));
            AddLoadMeter(carryBody, "CarryCapacityMeter", Session.CarryUsed, Session.CarryUsed, Session.CarryCapacity);
            StockTarget(left, "carry");
            StorageGrid(scrollWear ? carryBody : Scroll(left, "carry_stock"), Session.CarriedItems, Session.CarryCapacity, "carry");
            BindStockDrop(left.GetParent<Control>(), "carry");
            var right = Section(columns, 1.5f); right.Name = "ExternalContainer";
            if (Session.AtBase)
            {
                var header = Row(8); right.AddChild(header);
                var title = Inline("仓库", 20, Ink); title.SizeFlagsHorizontal = SizeFlags.ExpandFill; header.AddChild(title);
                header.AddChild(Inline($"{Session.WarehouseUsed} / {Session.WarehouseCapacity} 格", 16, Ink));
                AddLoadMeter(right, "WarehouseCapacityMeter", Session.WarehouseUsed, Session.WarehouseUsed, Session.WarehouseCapacity);
                StockTarget(right, "warehouse"); StorageGrid(Scroll(right, "warehouse_stock"), Session.StoredStock, Session.WarehouseCapacity, "warehouse");
                BindStockDrop(right.GetParent<Control>(), "warehouse");
            }
            else
            {
                right.AddChild(Inline("附近物品", 20, Ink)); StockTarget(right, "ground"); var ground = Scroll(right, "ground_stock");
                foreach (var pair in Session.GroundHere.ToArray())
                {
                    var key = pair.Key; var items = Session.State.Stock.Where(i => i.Container == key).ToArray(); if (items.Length == 0) continue;
                    ground.AddChild(Text(pair.Value.Name + (pair.Value.FreePickup ? " · 拾取不耗行动" : $" · 拾取消耗 {Session.SceneActionCost()} 行动"), 16, Ink));
                    var fitting = Session.FittingPickup(key); var count = fitting.Values.Sum(); var full = count == items.Sum(i => i.Quantity);
                    ground.AddChild(Button("loot_all_" + key.Replace(':', '_'), full ? "全部拾取" : count > 0 ? $"拾取可装下的 {count} 件…" : "背包已满", () => { if (full) Change(() => Session.PickupAll(key)); else ConfirmPartialPickup(key, fitting); }, count > 0 && Session.State.Phase == ChapterPhase.Exploring));
                    StorageGrid(ground, items, 0, key.Replace(':', '_'));
                    ground.AddChild(QuietButton("loot_plan_" + key.Replace(':', '_'), "自定义装包…", () => ConfirmPartialPickup(key, fitting)));
                }
                if (!Session.GroundHere.Any()) ground.AddChild(Text("附近没有可拾取物品", 16, Muted));
                BindStockDrop(right.GetParent<Control>(), "ground");
            }
        }
        StockSelectionBar(box, selected);
    }

    private void StockSelectionBar(VBoxContainer box, ChapterItem? item)
    {
        var panel = FlatPanel(box, 10); panel.Name = "StockActionBar";
        var content = Column(6); panel.AddChild(content);
        if (item == null) { content.AddChild(Text(_pairedAwaitBatch ? "选择具体来源批次后取放" : "选择物品查看用途；点击穿戴槽筛选装备", 16, Muted)); return; }
        var bundle = StockBundle(item); var total = bundle.Sum(i => i.Quantity); var worn = IsWornItem(item);
        var top = Row(12); content.AddChild(top);
        var title = Inline(ChapterContent.ItemName(item.Kind) + (worn ? " · 已穿戴" : $" ×{total}"), 20, Ink); title.SizeFlagsHorizontal = SizeFlags.ExpandFill; title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; top.AddChild(title);
        top.AddChild(Inline($"负重 {Session.CarriedGrams / 1000m:0.#} / {ChapterSession.WeightLimitGrams / 1000} kg" + (Session.Overweight ? " · 超重" : ""), 16, Session.Overweight ? Danger : Muted));
        string summary;
        if (ChapterContent.Gear.TryGetValue(item.Kind, out var gear))
        {
            var old = Session.Equipped.FirstOrDefault(k => ChapterContent.Gear[k].Slot == gear.Slot);
            var preview = Session.Preview(s => s.Equip(item.Kind));
            summary = gear.Slot == "backpack" ? worn ? $"容量 {Session.CarryCapacity} 格 · 基础口袋 + 当前背包" : $"穿戴后容量 {Session.CarryCapacity} → {4 + ChapterContent.BagCapacity(item.Kind)} 格"
                : worn ? $"{SlotName(gear.Slot)} · 提供 {Session.GearCards(item.Kind).Length} 张战斗牌" : "替换：" + (old == null ? "空槽" : ChapterContent.ItemName(old)) + $" · 供牌 { (old == null ? 0 : Session.GearCards(old).Length)} → {Session.GearCards(item.Kind).Length} 张";
            summary += preview == null ? " · " + Session.EquipBlocker(item.Kind) : $" · 负重 {Session.CarriedGrams / 1000m:0.#} → {preview.CarriedGrams / 1000m:0.#} kg";
        }
        else summary = ItemPurpose(item.Kind).Split('\n')[0] + (item.KeepOnPerson ? " · 保留随身" : "") + (item.FreeSupply ? " · 配发物品" : "");
        var note = StockText(summary, 16, Muted); note.Name = "StockSelectionSummary"; note.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; content.AddChild(note); note.TooltipText = summary;
        if (gear != null && !worn && gear.Slot != "backpack")
        {
            var old = Session.Equipped.FirstOrDefault(k => ChapterContent.Gear[k].Slot == gear.Slot);
            var from = old == null ? new Dictionary<string, int>() : Session.GearCards(old).GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
            var to = Session.GearCards(item.Kind).GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
            var delta = string.Join(" · ", from.Keys.Union(to.Keys).Where(k => from.GetValueOrDefault(k) != to.GetValueOrDefault(k)).Select(k => ChapterContent.Cards[k].Name + $" {from.GetValueOrDefault(k)}→{to.GetValueOrDefault(k)}"));
            var changes = StockText(delta == "" ? "供牌不变" : delta, 14, Ink); changes.Name = "InlineGearChanges"; changes.TooltipText = delta; changes.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; content.AddChild(changes);
        }
        var actionsScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Auto, VerticalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(0, 52), FollowFocus = true }; content.AddChild(actionsScroll);
        var actions = Row(6); actionsScroll.AddChild(actions);
        if (gear != null)
        {
            actions.AddChild(Button("equip_" + item.Kind, worn ? gear.Slot is "backpack" or "hands" ? "卸下" : "已穿戴" : "穿戴", () => ChangeEquipment(item.Kind), Session.CanEquipItem(item.Kind), !worn));
            actions.AddChild(QuietButton("stock_compare", "详细比较", () => OpenGearComparison(item.Kind)));
        }
        if (!worn)
        {
            var count = bundle.Length == 1 ? ChosenQuantity(item) : total;
            var preview = BundlePreview(bundle, count);
            var verb = item.Container == "carry" ? Session.AtBase ? "存仓" : "放下" : Session.AtBase ? "带入" : "拾取";
            var prefix = Session.AtBase ? "transfer" : item.Container == "carry" ? "drop" : "pickup";
            var move = Button(prefix + (item.Quantity > 1 ? "_stack_" : "_") + item.Id, verb + $" ×{count}", () => RequestBundleMove(item, count), preview != null, gear == null); actions.AddChild(move);
            move.TooltipText = bundle.Length == 1 ? QuantityForecast(item, count) : BundleForecast(bundle, count);
            actions.AddChild(Button("stock_quantity", "数量…", () => OpenBundleQuantity(item), Session.State.Phase is ChapterPhase.Camp or ChapterPhase.Exploring));
        }
        actions.AddChild(QuietButton("stock_more", bundle.Length > 1 ? $"来源批次 · {bundle.Length}" : "更多 · 右键", () => OpenBundleMenu(item)));
        if (gear == null)
        {
            var preview = BundlePreview(bundle, bundle.Length == 1 ? ChosenQuantity(item) : total);
            var forecast = preview == null ? "空间不足或物品受限 · 点数量查看原因" : preview.State.Phase == ChapterPhase.Battle ? "巡逻先拦截 · 物品不移动" : Session.AtBase ? $"取放后：随身 {preview.CarryUsed}/{preview.CarryCapacity} 格 · {preview.CarriedGrams / 1000m:0.#} kg" : $"区域压力 +{preview.State.Pressure - Session.State.Pressure} · 随身 {preview.CarryUsed}/{preview.CarryCapacity} 格";
            if (preview?.Overweight == true) forecast += " · 超重，后续行动 ×2";
            var forecastLabel = Inline(forecast, 14, preview == null || preview.Overweight ? Danger : Muted); forecastLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter; actions.AddChild(forecastLabel);
        }
    }

    private Dictionary<long, int> BundlePlan(ChapterItem[] bundle, int count)
    {
        var result = new Dictionary<long, int>();
        foreach (var item in bundle) { var take = Math.Min(count, item.Quantity); if (take > 0) result[item.Id] = take; count -= take; }
        return result;
    }
    private ChapterSession? BundlePreview(ChapterItem[] bundle, int count) => bundle.Length == 0 || count < 1 || count > bundle.Sum(i => i.Quantity) ? null
        : Session.Preview(s => bundle.Length == 1 ? StockCommand(s, bundle[0].Id, bundle[0].Container, count, true)
            : s.TransferBatch(BundlePlan(bundle, count), bundle[0].Container, StockDestination(bundle[0]), true));
    private string BundleForecast(ChapterItem[] bundle, int count)
    {
        var preview = BundlePreview(bundle, count);
        return preview == null ? "目标空间不足或物品受限；可选择较少数量。" : $"{ContainerName(bundle[0].Container)} → {ContainerName(StockDestination(bundle[0]))} ×{count}\n随身 {Session.CarryUsed} → {preview.CarryUsed}/{preview.CarryCapacity} 格 · 负重 {Session.CarriedGrams / 1000m:0.#} → {preview.CarriedGrams / 1000m:0.#} kg" + (preview.Overweight ? "\n超重：后续场景行动消耗翻倍。" : "");
    }
    private void RequestBundleMove(ChapterItem item, int count, bool confirmField = false)
    {
        var bundle = StockBundle(item);
        if (bundle.Length <= 1) { RequestStockMove(item.Id, item.Container, count, confirmField); return; }
        var plan = BundlePlan(bundle, count);
        if (count < 1 || plan.Values.Sum() != count || BundlePreview(bundle, count) == null) { _actionWarning = "目标空间不足或物品已改变，请重新选择数量。"; Render(); return; }
        void Commit() => ChangeStock(() => Session.TransferBatch(plan, item.Container, StockDestination(item), true), ChapterContent.ItemName(item.Kind) + $" ×{count}");
        if (item.KeepOnPerson && item.Container == "carry") Confirm("存入保留物品？", BundleForecast(bundle, count), Commit, "存入仓库"); else Commit();
    }
    private void OpenBundleQuantity(ChapterItem item)
    {
        var bundle = StockBundle(item);
        if (bundle.Length == 1) { _quantityGroup = []; OpenQuantity(item); return; }
        _quantityGroup = bundle.Select(i => i.Id).ToArray(); _quantity = bundle.Sum(i => i.Quantity); _quantityItem = item.Id; _quantitySource = item.Container; _quotaMode = false; OpenNested("quantity");
    }
    private void BundleQuantityPanel(VBoxContainer box)
    {
        var bundle = Session.State.Stock.Where(i => _quantityGroup.Contains(i.Id) && i.Container == _quantitySource).OrderBy(i => i.Id).ToArray();
        if (bundle.Length != _quantityGroup.Length) { box.AddChild(Text("物品已改变，请返回重新选择。", 16, Danger)); return; }
        var total = bundle.Sum(i => i.Quantity);
        box.AddChild(Text(ChapterContent.ItemName(bundle[0].Kind) + $" · 合计 {total} 件 / {bundle.Length} 个来源批次", 20, Ink));
        var amount = new SpinBox { Name = "StockQuantity", MinValue = 1, MaxValue = total, Step = 1, Value = _quantity, UpdateOnTextChanged = true }; box.AddChild(amount);
        var tools = Row(8); box.AddChild(tools);
        tools.AddChild(Button("quantity_all", "全部", () => amount.Value = total));
        var low = 0; var high = total; while (low < high) { var mid = low + (high - low + 1) / 2; if (BundlePreview(bundle, mid) != null) low = mid; else high = mid - 1; }
        var maximum = low;
        tools.AddChild(Button("quantity_max", $"最多可放入 {maximum}", () => amount.Value = maximum, maximum > 0));
        var preview = Text(BundleForecast(bundle, (int)amount.Value), 18, Ink); box.AddChild(preview); Fill(box);
        var actions = Row(8); box.AddChild(actions); actions.AddChild(Button("quantity_cancel", "返回", ClosePanel));
        var commit = Button("quantity_commit", "确认取放", () => { var count = (int)amount.Value; ClosePanel(); RequestBundleMove(bundle[0], count); }, primary: true); actions.AddChild(commit);
        void Update() { _quantity = (int)amount.Value; preview.Text = BundleForecast(bundle, _quantity); commit.Disabled = BundlePreview(bundle, _quantity) == null; }
        amount.ValueChanged += _ => Update(); Update();
    }
    private void OpenBundleMenu(ChapterItem item)
    {
        if (StockBundle(item).Length <= 1) { OpenStockMenu(item.Id); return; }
        _bundleDetailId = item.Id; OpenNested("stock-batches");
    }
    private void StockBatchesPanel(VBoxContainer box)
    {
        var item = Session.State.Stock.FirstOrDefault(i => i.Id == _bundleDetailId);
        if (item == null) return;
        box.AddChild(Text(ChapterContent.ItemName(item.Kind) + " · 来源批次", 20, Ink));
        box.AddChild(Text("合并显示不改变来源和保护状态。选择一批可单独取放、保留或查看详情。", 16, Muted));
        var scroll = Scroll(box, "stock_batches");
        foreach (var batch in StockBundle(item))
        {
            var captured = batch;
            var origin = batch.Source == "" ? "已收存物资" : batch.Source;
            scroll.AddChild(Button("stock_batch_detail_" + batch.Id, $"批次 {batch.Id} · ×{batch.Quantity} · {origin}" + (ChapterContent.Gear.ContainsKey(batch.Kind) ? batch.Registered ? " · 已登记" : " · 未登记" : ""), () => { _stockBatches = true; ClosePanel(); _selectedStock = captured.Id; _requestedFocus = $"stock_{captured.Id}_0_0"; Render(); }));
        }
    }
}
