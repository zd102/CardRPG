using CardRPG.Core;
using Godot;
using System;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private PanelContainer? _inspection;
    private Label? _inspectionCopy;
    private Control? _inspected;
    private string _inspectionFullText = "";
    private int _inspectionTicket;
    private Action? _resumePointerInspection;
    private Control? _pointerInspected;

    private static ExpeditionGlyph Glyph(string symbol, Color? color = null, float size = 26) => new()
    { Symbol = symbol, Tint = color ?? Accent, CustomMinimumSize = new Vector2(size, size), MouseFilter = MouseFilterEnum.Ignore };

    private void Inspect(Control control, Func<string> description)
    {
        if (control is not Godot.Button)
        {
            control.Draw += () => { if (control.HasFocus()) control.DrawRect(new Rect2(Vector2.Zero,control.Size),Accent,false,2); };
            control.FocusEntered += control.QueueRedraw; control.FocusExited += control.QueueRedraw;
        }
        void Show()
        {
            if (_inspection == null || !GodotObject.IsInstanceValid(_inspection)) return;
            if (!_hoverTips || !GodotObject.IsInstanceValid(control) || !control.IsInsideTree()) return;
            if (_stockMenu != null || GetViewport().GuiIsDragging() || _panel == "detail") return;
            if (control.Name == "close_panel" || control.Name.ToString().StartsWith("target_") || control.Name.ToString().StartsWith("inspection_")) return;
            var text = description();
            if (string.IsNullOrWhiteSpace(text)) return;
            if (_selectedCard >= 0 && _panel == "" && Session.State.Phase == ChapterPhase.Battle) return;
            _inspected = control; _inspectionFullText = text;
            _inspectionCopy!.Text = UiCopy.Present(_referenceText == "" || _referenceText == text ? text : "【已固定 · 只读快照，不代表当前结果】\n" + _referenceText + "\n\n【本次查看】\n" + text); _inspection.Visible = true;
            if (_panel != "")
            {
                var rect = control.GetGlobalRect(); var shortCopy = !text.Contains('\n') && text.Length <= 28;
                var width = Math.Min(shortCopy ? Math.Max(220, text.Length * 16 * ReadingScale + 32) : 540, Size.X - 64);
                var height = Math.Min(Size.Y - 160, shortCopy ? 150 : 300);
                var x = Mathf.Clamp(rect.Position.X, 32, Size.X - width - 32);
                var y = rect.End.Y + height + 12 < Size.Y - 32 ? rect.End.Y + 8 : Math.Max(90, rect.Position.Y - height - 8);
                var freeLeft=rect.Position.X-44;var freeRight=Size.X-rect.End.X-44;
                if(freeLeft>=200||freeRight>=200)
                {var onLeft=freeLeft>=freeRight;width=Math.Min(width,onLeft?freeLeft:freeRight);x=onLeft?rect.Position.X-width-12:rect.End.X+12;y=Math.Clamp(rect.Position.Y,190,Math.Max(190,Size.Y-height-240));}
                if(_root.FindChild("StockActionBar",true,false) is Control actionBar)
                {
                    var left=rect.Position.X-44;var right=Size.X-rect.End.X-44;
                    if(left>=300||right>=300)
                    {var onLeft=left>=right;width=Math.Min(width,onLeft?left:right);x=onLeft?rect.Position.X-width-12:rect.End.X+12;}
                    height=Math.Min(height,Math.Max(150,actionBar.GetGlobalRect().Position.Y-110));
                    y=Math.Clamp(rect.Position.Y,90,Math.Max(90,actionBar.GetGlobalRect().Position.Y-height-12));
                }
                y = Math.Clamp(y, 16, Math.Max(16, Size.Y - height - 32));
                Dock(_inspection, x, y, x + width, y + height);
            }
            else if(Session.State.Phase==ChapterPhase.Battle)
                Dock(_inspection,24,-362,-320,-202,0,1,1,1);
            else
            {
                var width=Math.Min(480,Size.X*.38f);var height=220f;
                var keepouts=Buttons.Where(p=>!p.Key.StartsWith("inspection_")&&p.Value.IsVisibleInTree()).Select(p=>p.Value.GetGlobalRect().Grow(6)).ToArray();
                var points=new System.Collections.Generic.List<Vector2>();
                for(var y=100f;y+height<Size.Y-24;y+=32)for(var x=24f;x+width<Size.X-24;x+=32)points.Add(new(x,y));
                var origin=control.GetGlobalRect().GetCenter();
                var free=points.OrderBy(p=>(p+new Vector2(width,height)/2).DistanceSquaredTo(origin)).FirstOrDefault(p=>!keepouts.Any(r=>r.Intersects(new Rect2(p,new(width,height)))));
                if(free==Vector2.Zero){free=new Vector2(380,104);width=Math.Max(320,Size.X-420);height=134;}
                Dock(_inspection,free.X,free.Y,free.X+width,free.Y+height);
            }
        }
        async void PointerShow()
        {
            var ticket = ++_inspectionTicket;
            if (_inputModality != "mouse") return;
            if (_hoverDelayMs > 0) await ToSignal(GetTree().CreateTimer(_hoverDelayMs / 1000.0), SceneTreeTimer.SignalName.Timeout);
            if (ticket == _inspectionTicket && GodotObject.IsInstanceValid(control) && control.IsInsideTree() && control.GetGlobalRect().HasPoint(GetGlobalMousePosition())) Show();
        }
        void Hide()
        {
            if (_inspected != control || _inspection == null || !GodotObject.IsInstanceValid(_inspection)) return;
            _inspection.Visible = false; _inspected = null;
        }
        async void PointerHide()
        {
            ++_inspectionTicket;
            await ToSignal(GetTree().CreateTimer(.18), SceneTreeTimer.SignalName.Timeout);
            if (_inspection != null && GodotObject.IsInstanceValid(_inspection) && _inspection.Visible && _inspection.GetGlobalRect().HasPoint(GetGlobalMousePosition())) return;
            if (GodotObject.IsInstanceValid(control) && control.IsInsideTree() && !control.HasFocus()) Hide();
        }
        control.MouseEntered += ()=>{_pointerInspected=control;_resumePointerInspection=PointerShow;PointerShow();}; control.FocusEntered += Show;
        control.MouseExited += ()=>{if(_pointerInspected==control){_pointerInspected=null;_resumePointerInspection=null;}PointerHide();};
        control.FocusExited += Hide;
    }

    private void InspectionLayer()
    {
        _inspected = null;
        ++_inspectionTicket;
        _inspection = FlatPanel(_root, 12); _inspection.Name = "Inspection";
        _inspection.ZIndex = 20; _inspection.MouseFilter = MouseFilterEnum.Stop; _inspection.Visible = false;
        var panel = _inspection;
        // Dock writes several anchors/offsets. Clamp only after that transaction and container layout settle.
        panel.Resized += () => Callable.From(()=>ClampInspectionBounds(panel)).CallDeferred();
        if (_panel != "") Dock(_inspection, 340, 120, -24, 260, ar:1);
        else if (Session.State.Phase == ChapterPhase.Battle)
        {
            Dock(_inspection,-300,-442,300,-266,.5f,1,.5f,1);
        }
        else Dock(_inspection, -300, -238, 300, -108, .5f, 1, .5f, 1);
        var body = Column(6); _inspection.AddChild(body);
        var head = new HFlowContainer(); head.AddThemeConstantOverride("h_separation",8);head.AddThemeConstantOverride("v_separation",6);body.AddChild(head);
        if (_pinEntry) head.AddChild(Button("inspection_pin", KeyLabel("inspect") + UiCopy.Text("UI_3CAE3B130D83"), () => { _detailItemId = 0; _detailTitle = UiCopy.Text("UI_17B00C8B46A9"); _detailText = _inspectionFullText; OpenNested("detail"); }));
        if (_pinEntry) head.AddChild(Button("inspection_reference", _referenceText == "" ? UiCopy.Text("UI_461BAC02935B") : UiCopy.Text("UI_40B0EF6D99C5"), () => PinReference(_inspectionFullText)));
        if (_referenceText != "") head.AddChild(Button("inspection_clear_reference", UiCopy.Text("UI_ED51208197F8"), () => { _referenceText = ""; Render(); }));
        head.AddChild(Button("inspection_close", UiCopy.Text("UI_D301BC125833"), () => { _inspection?.Hide(); _inspected = null; }));
        var scroll = new ScrollContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; body.AddChild(scroll);
        _inspectionCopy = Text("", 16, Ink); _inspectionCopy.Name="InspectionCopy"; _inspectionCopy.SizeFlagsHorizontal = SizeFlags.ExpandFill; scroll.AddChild(_inspectionCopy);
        panel.MouseExited += () => { if (_inspected == null || !GodotObject.IsInstanceValid(_inspected) || !_inspected.HasFocus() && !_inspected.GetGlobalRect().HasPoint(GetGlobalMousePosition())) panel.Hide(); };
    }
    private void ClampInspectionBounds(PanelContainer panel)
    {
        if(!GodotObject.IsInstanceValid(panel)||!panel.IsInsideTree()||_inspection!=panel)return;
        var position=panel.GlobalPosition;
        position.X=Math.Clamp(position.X,16,Math.Max(16,Size.X-panel.Size.X-16));
        position.Y=Math.Clamp(position.Y,16,Math.Max(16,Size.Y-panel.Size.Y-16));
        if(_panel!=""&&_root.FindChild("StockActionBar",true,false) is Control actions)
            position.Y=Math.Min(position.Y,Math.Max(16,actions.GetGlobalRect().Position.Y-panel.Size.Y-12));
        panel.GlobalPosition=position;
    }

    private Button IconButton(string id, string symbol, string description, Action action, bool primary = false)
    {
        var button = Button(id, "", action, primary: primary);
        button.CustomMinimumSize = new Vector2(46, 46); button.TooltipText = description;
        var glyph = Glyph(symbol, primary ? new Color("f1e7ce") : Ink, 24);
        button.AddChild(glyph); Dock(glyph, -12, -12, 12, 12, .5f, .5f, .5f, .5f);
        return button;
    }

    private HBoxContainer SymbolValue(Node parent, string symbol, string value, Color color, string description = "", int size = 20)
    {
        var row = Row(6); row.MouseFilter = MouseFilterEnum.Pass; parent.AddChild(row);
        row.AddChild(Glyph(symbol, color, size + 6));
        var label = Inline(value, size, color);
        if (value.Any(char.IsDigit) && !value.Any(c => c >= 0x3400)) label.AddThemeFontOverride("font", ChapterSkin.NumberFont);
        row.AddChild(label);
        if (description != "")
        {
            row.FocusMode = FocusModeEnum.All;
            Inspect(row, () => description);
        }
        return row;
    }

    private void Pips(Node parent, string symbol, int value, int max, Color color, string description, int spend = 0)
    {
        var row = Row(7); parent.AddChild(row); row.AddChild(Glyph(symbol, color));
        for (var i = 0; i < max; i++)
        {
            var cell = new Panel { CustomMinimumSize = new Vector2(14, 18), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
            var consumed = i < value && i >= value - spend;
            cell.AddThemeStyleboxOverride("panel", Style(consumed ? new Color("f0cf9b") : i < value ? color : new Color("d3ccb3"), consumed ? Accent : i < value ? color : Muted, 0)); row.AddChild(cell);
        }
        row.FocusMode = FocusModeEnum.All;
        Inspect(row, () => description + $"  {value}/{max}" + (spend > 0 ? $" · 将消耗 {spend}，剩余 {value - spend}" : ""));
    }

    private void MaterialStrip(Node parent, int[] materials, Color? color = null)
    {
        var row = Row(20); parent.AddChild(row);
        for (var i = 0; i < 3; i++) SymbolValue(row, new[] { "steel", "wire", "spring" }[i], materials[i].ToString(), color ?? Accent, ChapterContent.MaterialName(i));
    }

    private static string CardSymbol(ChapterCard card) => card.Id switch
    { "C04" or "C05" => "bolt", "C02" or "C19" or "C35" => "bash", "C06" => "breaker", "C18" => "all", "C26" or "C36" => "draw", _ => card.Damage > 0 && card.Shield > 0 ? "bash" : card.Damage == 0 ? "guard" : "hammer" };

    private void CardEffects(Node parent, ChapterCard card, Color ink, bool live = false, int size = 20)
    {
        var effects = Row(size <= 14 ? 6 : 12); effects.Alignment = BoxContainer.AlignmentMode.Center; parent.AddChild(effects);
        if (card.Damage > 0)
        {
            var damage = card.Damage + (live && card.Id == "C07" ? Math.Min(8, Session.State.Battle?.Shield ?? 0) : 0);
            SymbolValue(effects, card.Id == "C18" ? "all" : "hammer", damage.ToString(), ink, size: size);
        }
        if (card.Shield > 0) SymbolValue(effects, "guard", card.Shield.ToString(), ink, size: size);
        if (card.Id == "C05") SymbolValue(effects, "mana", "+1", ink, size: size);
        if (card.Id is "C26" or "C36") SymbolValue(effects, "draw", card.Id == "C36" ? "+2" : "+1", ink, size: size);
    }

    private static string CardKeyword(string id) => id switch
    {
        "C06" => "穿盾", "C07" => "耗盾 ≤8", "C18" => "全体", "C25" => "留盾 ≤4",
        "C26" or "C36" => "抽牌上限 2", _ => ""
    };

    private (string Place, string Symbol, int Step, string[] Stages) ObjectiveState()
    {
        var s = Session.State;
        if (!s.Done.Contains("Q01"))
        {
            var step = s.StoredItems.Contains("T01") ? 2 : s.Items.Contains("T01") ? 1 : 0;
            return (step == 0 ? "废料场" : step == 1 ? "安全撤离" : "岑砧", "bolt", step, ["loot", "exit", "home"]);
        }
        if (!s.Done.Contains("Q02"))
        {
            var step = s.StoredCalibration ? 3 : s.Calibrated ? 2 : s.Items.Contains("T02") || s.StoredItems.Contains("T02") ? 1 : 0;
            return (step == 0 ? "货运广场" : step == 1 ? "维修工坊" : step == 2 ? "安全撤离" : "岑砧", "craft", step, ["loot", "craft", "exit", "home"]);
        }
        if (!s.Done.Contains("S01"))
        {
            var step = s.StoredItems.Contains("T11") ? 2 : s.Items.Contains("T11") ? 1 : 0;
            return (step == 0 ? "外环货站" : step == 1 ? "安全撤离" : "岑砧", "route", step, ["craft", "exit", "home"]);
        }
        return ("自由探索", "check", 0, ["map"]);
    }
}
