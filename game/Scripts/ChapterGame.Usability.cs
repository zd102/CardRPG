using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CardRPG;

public partial class ChapterGame
{
    private sealed class FlowMeasurement
    {
        public string Task { get; set; } = "";
        public string Experience { get; set; } = "未标注";
        public string Input { get; set; } = "";
        public string StartContext {get;set;}="";
        public string EndContext {get;set;}="";
        public int Width { get; set; }
        public int Height { get; set; }
        public float Reading { get; set; }
        public int StartingUnits { get; set; }
        public int EndingUnits { get; set; }
        public int Activations { get; set; }
        public int PageChanges { get; set; }
        public int DetailOpens { get; set; }
        public List<ViewportMeasurement> Viewports { get; set; } = new();
        public int WheelSteps { get; set; }
        public int Errors { get; set; }
        public int Recoveries { get; set; }
        public double PointerPixels { get; set; }
        public double Seconds { get; set; }
        public double FeedbackWaitSeconds { get; set; }
    }
    private sealed class ViewportMeasurement
    {
        public string Context { get; set; } = "";
        public bool Compact { get; set; }
        public float Reading { get; set; }
        public int FullyVisibleObjects { get; set; }
        public int TotalObjects { get; set; }
    }
    private void QueueViewportMeasurement()
    {
        var measurement = _measurement; if (measurement == null || _panel is "system" or "measurements") return;
        var generation = _renderGeneration; var context = _page + "/" + (_stockList ? "list" : "grid") + (_stockPaired ? "/paired" : "");
        if (measurement.Viewports.Any(v => v.Context == context && v.Compact == CompactDensity && v.Reading == ReadingScale)) return;
        Callable.From(async () =>
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_measurement != measurement || generation != _renderGeneration) return;
            var objects = Buttons.Where(p => p.Key.StartsWith("stock_") && p.Key.Length > 6 && char.IsDigit(p.Key[6]) || new[] { "inspect_", "recipe_", "quest_detail_", "overview_card_" }.Any(p.Key.StartsWith)).Select(p => p.Value).ToArray();
            bool EntirelyVisible(Control item)
            {
                if (!item.IsVisibleInTree() || !GetViewport().GetVisibleRect().Encloses(item.GetGlobalRect())) return false;
                for (var parent = item.GetParent(); parent != null; parent = parent.GetParent())
                    if (parent is Control { ClipContents: true } clip && !clip.GetGlobalRect().Encloses(item.GetGlobalRect())) return false;
                return true;
            }
            measurement.Viewports.Add(new() { Context = context, Compact = CompactDensity, Reading = ReadingScale, FullyVisibleObjects = objects.Count(EntirelyVisible), TotalObjects = objects.Length });
        }).CallDeferred();
    }
    private readonly List<FlowMeasurement> _measurements = new();
    private FlowMeasurement? _measurement;
    private ulong _measurementStart;
    private Vector2? _metricPointer;
    private bool _measurementReadFailed;
    private void LoadMeasurements()
    {
        if(_verify)return;
        var path=ProjectSettings.GlobalizePath("user://ui-flow-measurements.json");if(!File.Exists(path))return;
        try {var saved=JsonSerializer.Deserialize<List<FlowMeasurement>>(File.ReadAllText(path));if(saved!=null)_measurements.AddRange(saved.TakeLast(20));}
        catch {_measurementReadFailed=true;}
    }
    private string MeasurementContext=>( _panelHistory.TryPeek(out var origin)?origin.Panel:_panel)+" / "+Session.State.Phase+" / "+Session.State.Location;
    private void RecordActivation() { if (_measurement != null) _measurement.Activations++; }
    private void RecordMeasurementInput(InputEvent input)
    {
        if (_measurement == null) return;
        if (input is InputEventMouseMotion motion)
        {
            if (_metricPointer is Vector2 previous) _measurement.PointerPixels += previous.DistanceTo(motion.Position);
            _metricPointer = motion.Position;
        }
        if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown }) _measurement.WheelSteps++;
    }
    private void MeasurementPage(VBoxContainer box)
    {
        box.AddChild(Text(UiCopy.Text("UI_B17A25D29C85"), 20, Ink));
        var page = Scroll(box);
        page.AddChild(Text(UiCopy.Text("UI_7676A816A799"), 16, Ink));
        var task = new LineEdit { Name = "MeasurementTask", PlaceholderText = UiCopy.Text("UI_ECF5E71C95A4"), MaxLength = 64 }; page.AddChild(task);
        var experience = new OptionButton { Name = "MeasurementExperience" }; foreach (var label in new[] { "未标注经验", "首次使用", "熟悉操作" }) experience.AddItem(label); page.AddChild(experience);
        if (_measurement == null) page.AddChild(Button("measurement_start", UiCopy.Text("UI_D79F577A03EC"), () =>
        {
            _measurement = new() { Task = task.Text.Trim() == "" ? "未命名流程" : task.Text.Trim(), Experience = experience.GetItemText(experience.Selected), Input = _inputModality, Width = GetWindow().Size.X, Height = GetWindow().Size.Y, Reading = ReadingScale, StartingUnits = Session.State.Stock.Sum(i => i.Quantity) };
            _measurement.StartContext=MeasurementContext;_measurementStart = Time.GetTicksMsec(); _metricPointer = null; ClosePanel();
        }));
        else page.AddChild(Button("measurement_stop", UiCopy.Text("UI_B1CA4B97BD3E"), () =>
        {
            _measurement.Seconds = (Time.GetTicksMsec() - _measurementStart) / 1000.0; _measurement.EndingUnits = Session.State.Stock.Sum(i => i.Quantity);
            _measurement.EndContext=MeasurementContext;
            _measurements.Add(_measurement); _measurement = null; if (_measurements.Count > 20) _measurements.RemoveAt(0);
            if (!_verify && !_measurementReadFailed)
                try { File.WriteAllText(ProjectSettings.GlobalizePath("user://ui-flow-measurements.json"), JsonSerializer.Serialize(_measurements, new JsonSerializerOptions { WriteIndented = true })); }
                catch { _actionWarning = "流程记录未能写盘，本次结果仍保留在界面。"; }
            Render();
        }));
        foreach (var entry in _measurements.AsEnumerable().Reverse())
        {
            page.AddChild(Text(UiCopy.Format("UI_C299DF1C14EC", entry.Task, entry.Experience, entry.Input, entry.Width, entry.Height, entry.Reading, entry.StartingUnits, entry.EndingUnits, entry.Activations, entry.PageChanges, entry.WheelSteps, entry.Seconds, entry.FeedbackWaitSeconds, entry.PointerPixels, entry.Errors, entry.Recoveries), 16, Ink));
            page.AddChild(Text(UiCopy.Format("UI_B993FB5F8994", entry.DetailOpens) + string.Join("；", entry.Viewports.Select(v => $"{v.Context} {v.FullyVisibleObjects}/{v.TotalObjects}，{(v.Compact ? "紧凑" : "舒适")} {v.Reading:P0}")), 14, Muted));
        }
        if (_measurements.Count == 0) page.AddChild(Text(UiCopy.Text("UI_5ECBECDD8C01"), 16, Muted));
        box.AddChild(Button("measurement_return", UiCopy.Text("UI_572CF45BA436"), ClosePanel));
        if(_measurementReadFailed)page.AddChild(Text(UiCopy.Text("UI_3C4B07224656"),16,Muted));
        page.AddChild(Text(UiCopy.Text("UI_DD1E9B1A025E"),14,Muted));
    }
}
