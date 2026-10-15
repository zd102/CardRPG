using CardRPG.Core;
using Godot;
using System;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private void ToggleRecipePlan(string kind)
    {
        Change(() =>
        {
            if (Session.State.RecipePlan.Remove(kind)) return true;
            if (Session.State.RecipePlan.Count >= 4) return false;
            Session.State.RecipePlan.Add(kind); return true;
        });
    }
    private void RecipePlan(VBoxContainer box)
    {
        box.AddChild(Text(UiCopy.Text("UI_CD4E0434B3E9"), 18, Ink));
        var page = Scroll(box);
        foreach (var kind in Session.State.RecipePlan.Order())
        {
            var row = Row(8); page.AddChild(row);
            row.AddChild(Button("plan_recipe_" + kind, ChapterContent.ItemName(kind) + (Session.CraftBlockers(kind).Length == 0 ? UiCopy.Text("UI_CBEF19A3EF19") : UiCopy.Text("UI_BC35AC2C71EC")), () => OpenRecipe(kind)));
            row.AddChild(Button("plan_remove_" + kind, UiCopy.Text("UI_84036EE2884B"), () => ToggleRecipePlan(kind)));
        }
        Rule(page);
        var combined = Session.PlannedMaterialCost(Session.State.RecipePlan);
        page.AddChild(Text(combined.Any(p => p.Value > Session.SafeMaterialCount(p.Key)) ? UiCopy.Text("UI_F077CA1890A7") : UiCopy.Text("UI_F0450DE4CF55"), 16, combined.Any(p => p.Value > Session.SafeMaterialCount(p.Key)) ? Danger : Success));
        foreach (var (kind, count) in Session.PlannedMaterialCost(Session.State.RecipePlan))
        {
            var have = Session.SafeMaterialCount(kind); var missing = Math.Max(0, count - have);
            page.AddChild(Text(ChapterContent.ItemName(kind) + UiCopy.Format("UI_E075270E7461", count, have, missing), 18, missing > 0 ? Danger : Success));
            var sources = Session.KnownMaterialSources(kind);
            if (missing > 0 && sources.Count == 0) page.AddChild(Text(UiCopy.Text("UI_AFE37D596E9A"), 16, Muted));
            var sourceRow = new HFlowContainer(); page.AddChild(sourceRow);
            foreach (var id in sources)
                sourceRow.AddChild(Button("plan_source_" + kind + "_" + id, ChapterContent.Places[id].Name + " →", () => { _mapPlace = id; OpenNested("map"); }));
        }
        if (Session.State.RecipePlan.Count == 0) page.AddChild(Text(UiCopy.Text("UI_8C59B498C4B4"), 18, Muted));
        box.AddChild(Button("plan_reserve", UiCopy.Text("UI_D4BE78EA6A95") + (Session.State.ReservePlanMaterials ? UiCopy.Text("UI_39EAE64CFC41") : UiCopy.Text("UI_5D0AE622F61F")), () => Change(() => { Session.State.ReservePlanMaterials = !Session.State.ReservePlanMaterials; return true; })));
        box.AddChild(Button("plan_return", UiCopy.Text("UI_572CF45BA436"), ClosePanel));
        PinTaskButton(box, "plan");
        var needs=combined.Where(p=>p.Value>Session.SafeMaterialCount(p.Key)).ToDictionary(p=>p.Key,p=>p.Value-Session.SafeMaterialCount(p.Key));
        if(needs.Count>0)
        {
            page.AddChild(Text(UiCopy.Text("UI_EB1B0CE693F4"),20,Ink));
            foreach(var place in needs.Keys.SelectMany(Session.KnownMaterialSources).Distinct())
            {
                page.AddChild(Button("shopping_place_"+place,ChapterContent.Places[place].Name+" →",()=>{OpenNested("map");_mapPlace=place;Render();}));
                page.AddChild(Text(string.Join("、",needs.Where(p=>Session.KnownMaterialSources(p.Key).Contains(place)).Select(p=>ChapterContent.ItemName(p.Key)+"（总缺 "+p.Value+"）")),16,Ink));
            }
            page.AddChild(Text(UiCopy.Text("UI_FD9BE165A40F"),14,Muted));
        }
    }

    private void RequestCraft(string kind, Func<ChapterSession, bool> craft)
    {
        var preview = Session.Preview(craft);
        if (preview == null) { _actionWarning = string.Join(" · ", Session.CraftBlockers(kind)); Render(); return; }
        void Commit(bool release = false)
        {
            var before = Session.ToJson(); Change(() => { if (!craft(Session)) return false; Session.State.RecipePlan.Remove(kind); if(release)Session.State.ReservePlanMaterials=false; return true; });
            if (Session.ToJson() != before && _saveWarning == "") { _craftedResult = kind; Render(); }
        }
        var remaining = Session.State.RecipePlan.Where(k => k != kind);
        var conflicts = Session.PlannedMaterialCost(remaining).Where(p => Math.Max(0, p.Value - preview.SafeMaterialCount(p.Key)) > Math.Max(0, p.Value - Session.SafeMaterialCount(p.Key)))
            .Select(p => ChapterContent.ItemName(p.Key) + $"：其他计划需 {p.Value}，制作后剩 {preview.SafeMaterialCount(p.Key)}").ToArray();
        if (Session.State.ReservePlanMaterials && conflicts.Length > 0)
            Confirm(UiCopy.Text("UI_3374EE7CE243"), string.Join("\n", conflicts) + UiCopy.Text("UI_144256E81F99"), ()=>Commit(), UiCopy.Text("UI_008D51E0B6D3"), ()=>Commit(true),UiCopy.Text("UI_B70F43BB4BA7"));
        else Commit();
    }
}
