using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private string _materialInfoKind = "", _trackedMaterial = "";
    private bool _showOwnedRecipeCost;
    private readonly List<string> _pinnedTasks = new();
    private string _craftedResult = "";
    private void OpenRecipe(string kind)
    {
        OpenNested("craft");
        _craftTab = kind.StartsWith("BP") || kind == "U01" ? "storage" : "recipes";
        if (kind.StartsWith("BP")) { _storageCraftTab = "bags"; _selectedBag = kind; }
        else if (kind == "U01") _storageCraftTab = "supplies";
        else _selectedRecipe = kind;
        _requestedFocus = "@initial"; Render();
    }
    private void OpenStockObject(long id)
    {
        OpenNested("gear"); _gearFilter = "cargo"; _stockFilter = "all"; _stockSearch = ""; _selectedStock = id; _requestedFocus = "@initial"; Render();
    }
    private void MaterialInfo(VBoxContainer box)
    {
        var kind = _materialInfoKind; var page = Scroll(box);
        var warehouse = Session.StoredStock.Where(i => i.Kind == kind).Sum(i => i.Quantity); var carry = Session.CarriedItems.Where(i => i.Kind == kind).Sum(i => i.Quantity);
        var required = Session.PlannedMaterialCost(Session.State.RecipePlan).GetValueOrDefault(kind);
        page.AddChild(Text(ChapterContent.ItemName(kind), 28, Ink));
        page.AddChild(Text(UiCopy.Format("UI_31E05FB35E0D", warehouse, carry, required, Math.Max(0, required - warehouse - carry)), 18, Ink));
        foreach (var item in Session.State.Stock.Where(i => i.Kind == kind && (i.Container == "carry" || Session.AtBase && i.Container == "warehouse")))
            page.AddChild(Button("material_stock_" + item.Id, ContainerName(item.Container) + UiCopy.Format("UI_AAF591402B07", item.Id, item.Quantity), () => OpenStockObject(item.Id)));
        var sources = Session.KnownMaterialSources(kind);
        foreach (var place in sources) page.AddChild(Button("material_source_" + place, UiCopy.Text("UI_EDA468A5F0AB") + ChapterContent.Places[place].Name, () => { _mapPlace = place; OpenNested("map"); }));
        page.AddChild(Text(sources.Count == 0 ? UiCopy.Text("UI_AFE37D596E9A") : UiCopy.Text("UI_0779152561F4"), 16, Muted));
        box.AddChild(Button("material_track", _trackedMaterial == kind ? UiCopy.Text("UI_A136DF9534DF") : UiCopy.Text("UI_3C766A38ED6D"), () => { _trackedMaterial = _trackedMaterial == kind ? "" : kind; SavePreferences(); Render(); }));
        box.AddChild(Button("material_return", UiCopy.Text("UI_D0B98897C969"), ClosePanel));
    }
    private void PreparationTaskManifest(Node parent)
    {
        var quest = Session.ObjectiveQuest; var kind = ChapterSession.QuestItem(quest);
        if (kind == "") return;
        var item = Session.State.Stock.FirstOrDefault(i => i.Kind == kind);
        var status = item == null ? "尚未获得" : item.Container == "warehouse" ? "在仓库" : item.Container is "carry" or "weapon" or "body" or "hands" or "backpack" ? "已带" : "在区域地面";
        parent.AddChild(Text(UiCopy.Text("UI_F8A2183BEF5B") + ChapterContent.ItemName(kind) + " · " + status + UiCopy.Format("UI_E5383B518851", ChapterContent.Items[kind].Slots) + (status == "已带" ? UiCopy.Text("UI_F33F46937783") : UiCopy.Text("UI_E1857CF946CF")), 16, Ink));
        if (item != null && item.Container is "carry" or "warehouse") parent.AddChild(Button("prepare_task_item", UiCopy.Text("UI_16D4F6412A09"), () => OpenStockObject(item.Id)));
        else if (Session.QuestPlace(quest) is { Length: > 0 } place) parent.AddChild(Button("prepare_task_map", UiCopy.Text("UI_3CDDA00DDDCB"), () => { _mapPlace = place; OpenNested("map"); }));
    }
    private void LegacyCapacityRecovery(Node parent)
    {
        if (!Session.State.LegacyOverCapacity) return;
        var carry = Math.Max(0, Session.CarryUsed - Session.CarryCapacity); var stored = Math.Max(0, Session.WarehouseUsed + Session.WarehouseReserved - Session.WarehouseCapacity);
        parent.AddChild(Text(UiCopy.Format("UI_1E7B72981578", carry, stored), 16, Danger));
        var actions = new HFlowContainer(); parent.AddChild(actions);
        actions.AddChild(Button("legacy_carry", UiCopy.Text("UI_6DF7029EA656"), () => { _stockFilter = "all"; _stockSearch = ""; _selectedStock = Session.CarriedItems.OrderByDescending(i => ChapterContent.Items[i.Kind].Slots * i.Quantity).FirstOrDefault()?.Id ?? 0; Render(); }));
        actions.AddChild(Button("legacy_materials", UiCopy.Text("UI_DF8C9EBC14D1"), () => { _stockFilter = "materials"; _stockSearch = ""; Render(); }));
        actions.AddChild(Button("legacy_upgrade", UiCopy.Text("UI_31D07B250F3B"), () => { OpenNested("craft"); _craftTab = "storage"; _storageCraftTab = "warehouse"; Render(); }));
    }
    private string PinnedTitle(string reference)
    {
        var parts = reference.Split(':', 2);
        return parts[0] switch { "recipe" => "配方 · " + ChapterContent.ItemName(parts[1]), "quest" => "任务 · " + ChapterContent.Quests.First(q => q.Id == parts[1]).Name, "warehouse" => "仓库扩建", "plan" => "材料计划", _ => "物资整理" };
    }
    private bool ValidPinned(string reference)
    {
        var p = reference.Split(':', 2);
        return p[0] is "warehouse" or "plan" or "stock" || p.Length == 2 && (p[0] == "recipe" && ChapterContent.Items.ContainsKey(p[1]) && (ChapterContent.Recipes.ContainsKey(p[1]) || p[1].StartsWith("BP") || p[1] == "U01") || p[0] == "quest" && ChapterContent.Quests.Any(q => q.Id == p[1]));
    }
    private void PinTaskButton(Node parent, string reference)
    {
        parent.AddChild(Button("pin_task", _pinnedTasks.Contains(reference) ? UiCopy.Text("UI_02D80BDFDFBB") : UiCopy.Text("UI_B52BA7D7D953"), () => { if (!_pinnedTasks.Remove(reference)) _pinnedTasks.Add(reference); SavePreferences(); Render(); }, _pinnedTasks.Contains(reference) || _pinnedTasks.Count < 4));
    }
    private void OpenPinned(string reference)
    {
        var p = reference.Split(':', 2);
        if (p[0] == "recipe") OpenRecipe(p[1]);
        else if (p[0] == "quest")
        {
            if (!Session.QuestOpen(p[1])) { _detailItemId=0; _detailTitle=PinnedTitle(reference); _detailText=UiCopy.Text("UI_8EEEE6AB8110"); OpenNested("detail"); return; }
            OpenNested("journal"); _selectedQuest = p[1]; _questFilter = Session.State.Done.Contains(p[1]) ? "done" : "active"; Render();
        }
        else if (p[0] == "warehouse") { OpenNested("craft"); _craftTab = "storage"; _storageCraftTab = "warehouse"; Render(); }
        else if (p[0] == "stock") { OpenNested("gear"); _gearFilter = "cargo"; Render(); }
        else OpenNested("plan");
    }
    private void PinnedTasks(VBoxContainer box)
    {
        box.AddChild(Text(UiCopy.Text("UI_A9AA6E9681EC"), 18, Ink)); var page = Scroll(box);
        foreach (var reference in _pinnedTasks)
        {
            var row = Row(); page.AddChild(row); row.AddChild(Button("pinned_open_" + _pinnedTasks.IndexOf(reference), PinnedTitle(reference), () => OpenPinned(reference)));
            row.AddChild(Button("pinned_remove_" + _pinnedTasks.IndexOf(reference), UiCopy.Text("UI_6135D4159E89"), () => { _pinnedTasks.Remove(reference); SavePreferences(); Render(); }));
        }
        if (_pinnedTasks.Count == 0) page.AddChild(Text(UiCopy.Text("UI_9703456EB038"), 16, Muted));
        box.AddChild(Button("pinned_defaults", UiCopy.Text("UI_9B004FEAD719"), () => { _pinnedTasks.Clear(); _pinnedTasks.AddRange(["stock", "plan"]); SavePreferences(); Render(); }));
        box.AddChild(Button("pinned_return", UiCopy.Text("UI_572CF45BA436"), ClosePanel));
    }
    private void LastPreparationSummary(Node parent)
    {
        if (Session.State.LastPreparation is not { } preset) return;
        var missing=preset.Equipment.Values.Where(k=>k!=""&&!Session.State.Stock.Any(i=>i.Kind==k&&!i.Container.StartsWith("ground:"))).Select(ChapterContent.ItemName).ToList();
        foreach(var (kind,count) in preset.Carry) { var have=Session.State.Stock.Where(i=>i.Kind==kind&&!i.FreeSupply&&i.Container is "carry" or "warehouse").Sum(i=>i.Quantity);if(have<count)missing.Add(ChapterContent.ItemName(kind)+" ×"+(count-have)); }
        parent.AddChild(Text(UiCopy.Text("UI_266B22057DA2")+string.Join("、",preset.Equipment.Values.Where(k=>k!="").Select(ChapterContent.ItemName))+"\n"+(missing.Count==0?UiCopy.Text("UI_6841C9D8CE75"):UiCopy.Text("UI_463071AAADB9")+string.Join("、",missing)),16,missing.Count==0?Muted:Danger));
    }
}
