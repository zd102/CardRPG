using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private readonly HashSet<long> _stockSelection = new();
    private readonly Dictionary<long, int> _stockQuantities = new();
    private Control? _stockMenu;
    private string? _stockMenuFocus;
    private long _quantityItem;
    private string _quantitySource = "";
    private int _quantity = 1;
    private string? _stockUndoBefore, _stockUndoAfter;
    private string _stockUndoLabel = "";
    private bool _stockChanging;
    private Label? _dragForecast;
    private Label? _dragPreviewText;
    private readonly Dictionary<(long Id, string Source, string Destination, int Count), (bool Allowed, string Forecast)> _dragChecks = new();

    private static string ContainerName(string container) => container == "carry" ? "随身" : container == "warehouse" ? "仓库" : "地面";
    private int ChosenQuantity(ChapterItem item) => Math.Clamp(_stockQuantities.GetValueOrDefault(item.Id, item.Quantity), 1, item.Quantity);
    private string StockDestination(ChapterItem item) => item.Container == "carry" ? Session.AtBase ? "warehouse" : "ground" : "carry";
    private bool StockCommand(ChapterSession session, long id, string source, int quantity, bool overrideProtection = false)
    {
        var item = session.State.Stock.FirstOrDefault(i => i.Id == id && i.Container == source);
        if (item == null) return false;
        return session.State.Phase == ChapterPhase.Camp
            ? session.TransferBatch(new Dictionary<long, int> { [id] = quantity }, source, source == "carry" ? "warehouse" : "carry", overrideProtection)
            : source == "carry" ? session.Drop(id, quantity) : session.Pickup(id, quantity);
    }

    private void ChangeStock(Func<bool> action, string label)
    {
        var stockBefore = Session.State.Stock.ToDictionary(i => i.Id, i => (i.Container, i.Quantity));
        var before = Session.ToJson(); var camp = Session.State.Phase == ChapterPhase.Camp;
        _stockChanging = true;
        try { Change(action); } finally { _stockChanging = false; }
        if (_saveWarning == "" && before != Session.ToJson())
        {
            _stockArrivals.Clear();
            foreach (var item in Session.State.Stock) if (!stockBefore.TryGetValue(item.Id, out var old) || old.Container != item.Container || old.Quantity < item.Quantity) _stockArrivals.Add(item.Id);
        }
        if (camp && _saveWarning == "" && before != Session.ToJson())
        { _stockUndoBefore = before; _stockUndoAfter = Session.ToJson(); _stockUndoLabel = label; Render(); }
    }

    private void UndoStock()
    {
        if (_stockUndoBefore == null || Session.State.Phase != ChapterPhase.Camp || Session.ToJson() != _stockUndoAfter) return;
        var before = _stockUndoBefore;
        Change(() => { Session = ChapterSession.FromJson(before); return true; });
    }

    private void RequestStockMove(long id, string source, int count, bool confirmField = false)
    {
        var item = Session.State.Stock.FirstOrDefault(i => i.Id == id && i.Container == source);
        if (item == null) { _actionWarning = "物品位置已改变，请重新选择。"; Render(); return; }
        if (count > 0 && count <= item.Quantity) _stockQuantities[id] = count;
        _selectedStock = id;
        void Commit() => ChangeStock(() => StockCommand(Session, id, source, count, true), $"{ChapterContent.ItemName(item.Kind)} ×{count} → {ContainerName(StockDestination(item))}");
        if (confirmField && Session.State.Phase == ChapterPhase.Exploring)
            Confirm(UiCopy.Text("UI_AB3EF2D91A9B"), ChapterContent.ItemName(item.Kind) + " ×" + count + "\n" + QuantityForecast(item, count), Commit, UiCopy.Text("UI_0ECCD085A35F"));
        else if (item.KeepOnPerson && source == "carry" && Session.AtBase)
            Confirm(UiCopy.Text("UI_B8D9E137AAE6"), UiCopy.Format("UI_32D9D104D8B5", ChapterContent.ItemName(item.Kind), count), Commit, UiCopy.Text("UI_568C107208D0"));
        else Commit();
    }

    private string QuantityForecast(ChapterItem item, int count)
    {
        var copy = Session.PreviewCopy();
        if (!StockCommand(copy, item.Id, item.Container, count, true)) return copy.FailureReason == "" ? "当前不可执行，物品保持原位。" : copy.FailureReason;
        if (copy.State.Phase == ChapterPhase.Battle) return "⚠ 巡逻将先拦截，本次物品不会移动。";
        return $"{ContainerName(item.Container)} → {ContainerName(StockDestination(item))} · 本次 {count} 件\n随身 {Session.CarryUsed} → {copy.CarryUsed}/{copy.CarryCapacity} 格；仓库 {Session.WarehouseUsed} → {copy.WarehouseUsed}/{copy.WarehouseCapacity} 格\n负重 {Session.CarriedGrams / 1000m:0.##} → {copy.CarriedGrams / 1000m:0.##} kg · 区域压力 +{copy.State.Pressure - Session.State.Pressure}"
            + (copy.Overweight ? " · ⚠ 超重，后续场景行动消耗翻倍" : "");
    }

    private void OpenQuantity(ChapterItem item, bool quota = false)
    {
        _quantityGroup = [];
        _quotaMode = quota; _quantityItem = item.Id; _quantitySource = item.Container; _quantity = ChosenQuantity(item); OpenNested("quantity");
    }

    private void QuantityPanel(VBoxContainer box)
    {
        if (_quantityGroup.Length > 1 && !_quotaMode) { BundleQuantityPanel(box); return; }
        var item = Session.State.Stock.FirstOrDefault(i => i.Id == _quantityItem && i.Container == _quantitySource);
        if (item == null) { box.AddChild(Text(UiCopy.Text("UI_CDDBF87BC76F"), 18, Danger)); return; }
        box.AddChild(Text(UiCopy.Format("UI_972A729AFEA1", ChapterContent.ItemName(item.Kind), ContainerName(item.Container), item.Quantity), 20, Ink));
        if (_quotaMode)
        {
            box.AddChild(Text(UiCopy.Text("UI_51CFF35ED381"), 18, Ink));
            var quota = new SpinBox { Name = "CarryQuota", MinValue = 0, MaxValue = 100000, Step = 1, Value = Session.State.CarryQuotas.GetValueOrDefault(item.Kind), UpdateOnTextChanged = true };
            box.AddChild(quota); Fill(box);
            box.AddChild(Button("quota_apply", UiCopy.Text("UI_DF462ABB71FF"), () => { var count = (int)quota.Value; ClosePanel(); Change(() => Session.SetCarryQuota(item.Kind, count)); }, primary: true));
            box.AddChild(Button("quantity_cancel", UiCopy.Text("UI_2CD0F3BE8738"), ClosePanel)); return;
        }
        var row = Row(10); box.AddChild(row);
        var amount = new SpinBox { Name = "StockQuantity", MinValue = 1, MaxValue = item.Quantity, Step = 1, Value = _quantity, CustomMinimumSize = new Vector2(180, 48), UpdateOnTextChanged = true };
        row.AddChild(amount);
        row.AddChild(Button("quantity_all", UiCopy.Text("UI_9BCAAD47068E"), () => amount.Value = item.Quantity));
        var max = item.Container == "carry" && !Session.AtBase ? item.Quantity : Session.MaximumTransfer(item.Id, StockDestination(item));
        row.AddChild(Button("quantity_max", UiCopy.Format("UI_B684D7BA3649", max), () => amount.Value = max, max > 0));
        var preview = Text(QuantityForecast(item, _quantity), 18, Ink); box.AddChild(preview);
        Fill(box);
        var actions = Row(12); box.AddChild(actions);
        actions.AddChild(Button("quantity_cancel", UiCopy.Text("UI_2CD0F3BE8738"), ClosePanel));
        actions.AddChild(Button("quantity_set", UiCopy.Text("UI_292E9F59397E"), () => { _stockQuantities[item.Id] = (int)amount.Value; ClosePanel(); }));
        var commit = Button("quantity_commit", UiCopy.Text("UI_54A9329196F1"), () => { var count = (int)amount.Value; ClosePanel(); RequestStockMove(item.Id, item.Container, count); }, primary: true);
        actions.AddChild(commit);
        void Update() { _quantity = (int)amount.Value; preview.Text = QuantityForecast(item, _quantity); commit.Disabled = Session.Preview(s => StockCommand(s, item.Id, item.Container, _quantity, true)) == null; }
        amount.ValueChanged += _ => Update(); Update();
    }

    private void CloseStockMenu()
    {
        if (_stockMenu == null) return;
        if (GodotObject.IsInstanceValid(_stockMenu)) { _stockMenu.GetParent()?.RemoveChild(_stockMenu); _stockMenu.QueueFree(); }
        _stockMenu = null;
        foreach (var key in Buttons.Keys.Where(k => k.StartsWith("menu_")).ToArray()) Buttons.Remove(key);
        if (_stockMenuFocus != null && Buttons.TryGetValue(_stockMenuFocus, out var previous)) FocusLater(previous);
    }

    private void OpenStockMenu(long id, Vector2? position = null)
    {
        CloseStockMenu();
        var item = Session.State.Stock.FirstOrDefault(i => i.Id == id);
        if (item == null || GetViewport().GuiIsDragging()) return;
        _stockMenuFocus = GetViewport().GuiGetFocusOwner()?.Name.ToString();
        if (_inspection != null) _inspection.Hide();
        var shade = new Control { Name = "StockContextMenu", MouseFilter = MouseFilterEnum.Stop, ZIndex = 60 };
        _root.AddChild(shade); shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); _stockMenu = shade;
        shade.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true }) { shade.AcceptEvent(); CloseStockMenu(); } };
        var panel = FlatPanel(shade, 12); panel.MouseFilter = MouseFilterEnum.Stop;
        var origin = position ?? GetGlobalMousePosition();
        var width = Math.Min(410, Size.X - 40); var height = Math.Min(570, Size.Y - 40);
        panel.Position = new Vector2(Math.Clamp(origin.X, 20, Size.X - width - 20), Math.Clamp(origin.Y, 20, Size.Y - height - 20)); panel.Size = new Vector2(width, height);
        var content = Column(8); panel.AddChild(content);
        content.AddChild(Text(ChapterContent.ItemName(item.Kind) + $" ×{item.Quantity} · {ContainerName(item.Container)}", 20, Ink));
        content.AddChild(Text(UiCopy.Text("UI_644D975C0118"), 14, Muted));
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        content.AddChild(scroll); var page = Column(8); page.SizeFlagsHorizontal = SizeFlags.ExpandFill; scroll.AddChild(page);
        void Entry(string key, string caption, Action action, bool enabled = true) => page.AddChild(Button("menu_" + key, caption, () => { CloseStockMenu(); action(); }, enabled));
        var count = ChosenQuantity(item); var allowed = Session.Preview(s => StockCommand(s, id, item.Container, count, true)) != null;
        Entry("move", $"{ContainerName(item.Container)} → {ContainerName(StockDestination(item))} ×{count}", () => RequestStockMove(id, item.Container, count), allowed);
        if (!allowed) page.AddChild(Text(QuantityForecast(item, count), 16, Danger));
        Entry("quantity", "选择数量 / 最大可放入…", () => OpenQuantity(item), Session.State.Phase is ChapterPhase.Camp or ChapterPhase.Exploring);
        Entry("detail", "固定完整详情…", () => ShowItemDetail(item));
        Entry("target", "点击选择目标容器…", () => { _clickMoveId = id; _clickMoveSource = item.Container; Render(); }, Session.State.Phase is ChapterPhase.Camp or ChapterPhase.Exploring);
        if (item.Container.StartsWith("ground:")) Entry("space", "预览腾出空间的步骤…", () => { _spacePlanItem = id; OpenNested("space-plan"); });
        if (ChapterContent.Gear.ContainsKey(item.Kind)) Entry("equip", "比较装备 / 查看换装条件…", () => OpenGearComparison(item.Kind));
        if (Session.State.Phase == ChapterPhase.Camp)
        {
            Entry("keep", item.KeepOnPerson ? "取消保留随身" : "标记保留随身", () => Change(() => Session.SetCarryProtection(id, !item.KeepOnPerson)));
            if (ChapterContent.MaterialIds.Contains(item.Kind)) Entry("quota", $"保留数量：{Session.State.CarryQuotas.GetValueOrDefault(item.Kind)}…", () => OpenCarryQuota(item));
            if (!item.Kind.StartsWith('T')) Entry("discard", "永久放弃此批…", () => ConfirmDiscard(item), !item.KeepOnPerson);
        }
        Entry("close", "关闭菜单 · Esc", () => { });
        FocusLater(Buttons.First(p => p.Key.StartsWith("menu_") && !p.Value.Disabled).Value);
    }

    private void OpenCarryQuota(ChapterItem item)
    {
        // The quantity picker has no side effects until the separate quota button is chosen.
        OpenQuantity(item, true);
    }
    private bool _quotaMode;
    private string _detailTitle = "", _detailText = "";
    private long _detailItemId;
    private string _materialRecipe = "";
    private void ShowItemDetail(ChapterItem item)
    {
        _detailItemId = item.Id;
        _detailTitle = ChapterContent.ItemName(item.Kind);
        var definition = ChapterContent.Items[item.Kind];
        _detailText = UiCopy.Format("UI_A4C902FE2228", ContainerName(item.Container), item.Id, item.Quantity, definition.Stack, definition.Slots, definition.Grams / 1000m) + ItemPurpose(item.Kind)
            + (item.FreeSupply ? UiCopy.Text("UI_AEE535F629C6") : "") + (item.KeepOnPerson ? UiCopy.Text("UI_6D1111239706") : "")
            + (item.Source == "" ? "" : UiCopy.Text("UI_D1E79617043E") + item.Source);
        OpenNested("detail");
    }
    private void ItemDetail(VBoxContainer box)
    {
        var page = Scroll(box); page.GetParent<Control>().Name = "DetailReading"; page.GetParent<Control>().FocusMode = FocusModeEnum.All;
        DetailComparison(page);
        var item = Session.State.Stock.FirstOrDefault(i => i.Id == _detailItemId);
        if (item != null && ChapterContent.MaterialIds.Contains(item.Kind))
        {
            page.AddChild(Text(UiCopy.Text("UI_5649CFDF7EB4"), 20, Ink));
            var recipes = ChapterContent.Recipes.Keys.Where(Session.RecipeOpen)
                .Concat(new[] { "BP01", "BP02", "BP03" }.Where(Session.BackpackRecipeOpen)).Append("U01")
                .Where(kind => Session.MaterialBudgetFor(Session.RecipeCost(kind)).Any(m => m.Kind == item.Kind)).ToArray();
            foreach (var kind in recipes)
                page.AddChild(Button("material_recipe_" + kind, ChapterContent.ItemName(kind) + (Session.CraftBlockers(kind).Length == 0 ? UiCopy.Text("UI_7E3F74E5931D") : UiCopy.Text("UI_611D89E29E5F")), () => { _materialRecipe = kind; OpenNested("recipe-preview"); }));
            if (recipes.Length == 0) page.AddChild(Text(UiCopy.Text("UI_FE9B5D09E858"), 16, Muted));
        }
        var detailActions = new HFlowContainer(); box.AddChild(detailActions);
        detailActions.AddChild(Button("detail_reference", _referenceText == "" ? UiCopy.Text("UI_E5FEF1A88C92") : UiCopy.Text("UI_2C880DDF7A70"), () => PinReference(_detailTitle + "\n" + _detailText)));
        if (_referenceText != "") detailActions.AddChild(Button("detail_clear_reference", UiCopy.Text("UI_ED51208197F8"), () => { _referenceText = ""; Render(); }));
        detailActions.AddChild(Button("detail_return", UiCopy.Text("UI_384912F7C842"), ClosePanel));
    }
    private void MaterialRecipePreview(VBoxContainer box)
    {
        var page = Scroll(box, "material_recipe", _materialRecipe);
        page.AddChild(Text(ChapterContent.ItemName(_materialRecipe) + (_materialRecipe == "U01" ? " ×2" : " ×1"), 20, Ink));
        var owned=_materialRecipe!="U01"&&Session.HasPhysical(_materialRecipe);
        if(owned)
        {
            page.AddChild(Text(UiCopy.Text("UI_3B20ACEBB697"),16,Success));
            page.AddChild(Button("material_history_cost",_showOwnedRecipeCost?UiCopy.Text("UI_A693576B9123"):UiCopy.Text("UI_74B1F44B66B0"),()=>{_showOwnedRecipeCost=!_showOwnedRecipeCost;Render();}));
            if(_showOwnedRecipeCost)Costs(page,Session.RecipeCost(_materialRecipe),false);
            page.AddChild(Button("material_owned_view",UiCopy.Text("UI_C1E70C803ED3"),()=>OpenGearComparison(_materialRecipe)));
        }
        else Costs(page, Session.RecipeCost(_materialRecipe));
        var blockers = Session.CraftBlockers(_materialRecipe);
        page.AddChild(Text(blockers.Length == 0 ? UiCopy.Text("UI_6C1A0BF87313") : string.Join("\n", blockers), 16, blockers.Length == 0 ? Success : Muted));
        box.AddChild(Button("material_recipe_return", UiCopy.Text("UI_9EEA537C1BB8"), ClosePanel));
    }
    private static string ItemPurpose(string kind) => kind.StartsWith('T') ? (kind switch {"T01"=>"用于修复据点工作灯", "T02"=>"带到工坊校准后交付工匠", "T11"=>"交付以解锁货站部署", "T14"=>"交付居民包裹委托",_=>"任务物品"})+"；安全带回才可交付，地面物品不随你撤离。"
        : kind == "U01" ? "探索中使用，恢复最多 14 点生命；满生命时不消耗。"
        : ChapterContent.Gear.TryGetValue(kind, out var gear) ? gear.Hint : kind switch { "M07"=>"再生织物：缝制背包、制作普通补给和部分仓库扩建。", "M08"=>"净化药剂：与织物一起制作普通补给。", "M09"=>"密封树脂：加固背包、承架背包及后续仓库扩建。", "M10"=>"承重构件：承架背包和仓库扩建。", _=>"制作材料：用于装备、改装、背包或扩仓；详情列出已知配方。" };

    private void RequestBatchMove(Dictionary<long, int> selected, string source, string destination)
    {
        var preview = Session.PreviewCopy(); var legal = preview.TransferBatch(selected, source, destination);
        var names = string.Join("\n", selected.Select(p => Session.State.Stock.FirstOrDefault(i => i.Id == p.Key) is { } item ? ChapterContent.ItemName(item.Kind) + " ×" + p.Value : "已失效物品"));
        if (!legal)
        {
            var fitting = Session.FittingTransfer(selected, source, destination);
            var left = selected.Where(p => fitting.GetValueOrDefault(p.Key) < p.Value).Select(p => Session.State.Stock.FirstOrDefault(i=>i.Id==p.Key) is { } item ? ChapterContent.ItemName(item.Kind) + " ×" + (p.Value - fitting.GetValueOrDefault(p.Key)) + " · " + (item.FreeSupply?"配发不能存仓":item.KeepOnPerson&&source=="carry"?"保留随身":item.Container!=source?"位置已变化":"目标空间不足，按清单顺序分配") : "批次已失效，未移动");
            if (fitting.Count == 0) { _actionWarning = preview.FailureReason; Render(); return; }
            var moved = string.Join("\n", fitting.Select(p => ChapterContent.ItemName(Session.State.Stock.Single(i => i.Id == p.Key).Kind) + " ×" + p.Value));
            Confirm(UiCopy.Text("UI_A8A24B93C925"), preview.FailureReason + UiCopy.Text("UI_85392D70A635") + moved + UiCopy.Text("UI_5D3139CD4C67") + string.Join("\n", left), () => ChangeStock(() => Session.TransferBatch(fitting, source, destination), "部分取放"), UiCopy.Text("UI_473A0EFA638A")); return;
        }
        Confirm(UiCopy.Format("UI_B9F76DAB7DE9", ContainerName(source), ContainerName(destination), selected.Count), names + UiCopy.Format("UI_008C8F6F7485", Session.CarryUsed, preview.CarryUsed, preview.CarryCapacity, Session.WarehouseUsed, preview.WarehouseUsed, preview.WarehouseCapacity, Session.CarriedGrams / 1000m, preview.CarriedGrams / 1000m), () => ChangeStock(() => Session.TransferBatch(selected, source, destination), "批量取放"), UiCopy.Text("UI_C03E1110FB29"));
    }

    private void BindStockPointer(Button tile, ChapterItem item)
    {
        tile.GuiInput += e =>
        {
            if (e is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true }) { tile.AcceptEvent(); OpenBundleMenu(item); }
            if (e is InputEventKey { Pressed: true, Echo: false } key && (key.Keycode == Key.Menu || key.Keycode == Key.F10 && key.ShiftPressed))
            { tile.AcceptEvent(); OpenBundleMenu(item); }
        };
        tile.SetDragForwarding(Callable.From<Vector2, Variant>(_ =>
        {
            if (!_dragItems || Session.State.Phase is not (ChapterPhase.Camp or ChapterPhase.Exploring) || _stockMenu != null) return default;
            var bundle = StockBundle(item); var count = bundle.Length == 1 ? ChosenQuantity(item) : bundle.Sum(i => i.Quantity); if (_inspection != null) _inspection.Hide();
            var preview = new PanelContainer(); preview.AddThemeStyleboxOverride("panel", Style(Surface, Ink));
            _dragPreviewText = Text(UiCopy.Format("UI_6738F080EE89", ChapterContent.ItemName(item.Kind), count, ContainerName(item.Container)), 16, Ink); _dragPreviewText.CustomMinimumSize = new Vector2(420, 0); preview.AddChild(_dragPreviewText);
                tile.SetDragPreview(preview);
            return new Godot.Collections.Dictionary { ["type"] = "stock", ["id"] = item.Id, ["source"] = item.Container, ["count"] = count, ["generation"]=(long)_renderGeneration };
        }), Callable.From<Vector2, Variant, bool>((_, data) => StockDropAllowed(item.Container.StartsWith("ground:") ? "ground" : item.Container, data)), Callable.From<Vector2, Variant>((_, data) => CommitStockDrop(item.Container.StartsWith("ground:") ? "ground" : item.Container, data)));
    }

    private bool ReadStockDrop(string destination, Variant data, out long id, out string source, out int count)
    {
        id = 0; source = ""; count = 0;
        if (data.VariantType != Variant.Type.Dictionary || Session.State.Phase is not (ChapterPhase.Camp or ChapterPhase.Exploring)) return false;
        var payload = data.AsGodotDictionary();
        if (!payload.ContainsKey("type") || payload["type"].AsString() != "stock" || !payload.ContainsKey("id") || !payload.ContainsKey("source") || !payload.ContainsKey("count") || !payload.ContainsKey("generation") || payload["generation"].AsInt64()!=(long)_renderGeneration) return false;
        id = payload["id"].AsInt64(); source = payload["source"].AsString(); count = payload["count"].AsInt32();
        return Session.State.Phase == ChapterPhase.Camp ? destination is "carry" or "warehouse" && source != destination
            : source == "carry" && destination == "ground" || source.StartsWith("ground:") && destination == "carry";
    }
    private bool StockDropAllowed(string destination, Variant data)
    {
        if (!ReadStockDrop(destination, data, out var id, out var source, out var count)) return false;
        var item = Session.State.Stock.FirstOrDefault(i => i.Id == id && i.Container == source);
        if (item == null) return false;
        var key = (id, source, destination, count);
        if (!_dragChecks.TryGetValue(key, out var check))
        { var bundle = StockBundle(item); check = (BundlePreview(bundle, count) != null, bundle.Length == 1 ? QuantityForecast(item, count) : BundleForecast(bundle, count)); _dragChecks[key] = check; }
        if (_dragPreviewText != null && GodotObject.IsInstanceValid(_dragPreviewText)) _dragPreviewText.Text = ChapterContent.ItemName(item.Kind) + " ×" + count + "\n" + check.Forecast + UiCopy.Text("UI_35A32E3B4FC0");
        return check.Allowed;
    }
    private void CommitStockDrop(string destination, Variant data)
    {
        if (ReadStockDrop(destination, data, out var id, out var source, out var count))
        {
            Callable.From(() => { var item = Session.State.Stock.FirstOrDefault(i => i.Id == id && i.Container == source); if (item != null) RequestBundleMove(item, count, true); }).CallDeferred();
        }
    }
    private void BindStockDrop(Control target, string destination)
    {
        if (target is not ChapterButton)
            target.SetDragForwarding(default, Callable.From<Vector2, Variant, bool>((_, data) => StockDropAllowed(destination, data)), Callable.From<Vector2, Variant>((_, data) => CommitStockDrop(destination, data)));
        foreach (var child in target.GetChildren().OfType<Control>()) BindStockDrop(child, destination);
    }
    private void OpenGearComparison(string kind)
    {
        OpenNested("gear"); _selectedGear = kind;
        _gearFilter = ChapterContent.Gear[kind].Slot; _requestedFocus = "@initial"; Render();
    }
    private void BindEquipmentDrop(Control slotControl, string slot)
    {
        string Candidate(Variant data)
        {
            if (data.VariantType != Variant.Type.Dictionary || Session.State.Phase != ChapterPhase.Camp) return "";
            var payload = data.AsGodotDictionary();
            if (!payload.ContainsKey("type") || payload["type"].AsString() != "stock") return "";
            var item = Session.State.Stock.FirstOrDefault(i => i.Id == payload["id"].AsInt64() && i.Container == payload["source"].AsString());
            return item != null && item.Container is "carry" or "warehouse" && ChapterContent.Gear.TryGetValue(item.Kind, out var gear) && gear.Slot == slot ? item.Kind : "";
        }
        slotControl.TooltipText += UiCopy.Text("UI_823F3A3B5A4C");
        slotControl.SetDragForwarding(default, Callable.From<Vector2, Variant, bool>((_, data) => Candidate(data) != ""), Callable.From<Vector2, Variant>((_, data) =>
        {
            var kind = Candidate(data);
            if (kind != "") Callable.From(() =>
            {
                if (Candidate(data) != kind) return;
                if (_panel == "gear" && _gearFilter == "cargo")
                {
                    _stockGearSlot = slot; _stockFilter = "gear"; _stockSearch = "";
                    _selectedStock = data.AsGodotDictionary()["id"].AsInt64(); _selectedGear = kind; Render();
                }
                else OpenGearComparison(kind);
            }).CallDeferred();
        }));
    }
}
