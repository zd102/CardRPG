using CardRPG.Core;
using Godot;
using System;
using System.IO;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private bool _startupOpen, _loadedSave;
    private string _startupProblem = "", _lastSavedAt = "";
    private ChapterSaveStore SaveStore => new(SavePath);
    internal void ShowStartupForVerification()
    { if(!_verify)throw new InvalidOperationException("Verification only");_startupOpen=true;_loadedSave=true;_panel="startup";_panelHistory.Clear();_requestedFocus="@initial";Render(); }
    private void Startup(VBoxContainer box)
    {
        var page = Scroll(box); page.AddChild(Text(UiCopy.Text("UI_E6B97B2273C1"), 28, Ink));
        if (_loadedSave)
        {
            page.AddChild(Text(UiCopy.Format("UI_E75E9F70F8A3", Session.State.Runs, Session.State.Done.Count, Session.State.Health) + (Session.AtBase ? UiCopy.Text("UI_916C57AF9A41") : ChapterContent.Places[Session.State.Location].Name), 20, Ink));
            page.AddChild(Text(File.Exists(SavePath) ? UiCopy.Text("UI_B2E3A8332C3F") + File.GetLastWriteTime(SavePath).ToString("yyyy-MM-dd HH:mm") : UiCopy.Text("UI_A3C141CEDDFA"), 16, Muted));
            page.AddChild(Button("startup_continue", UiCopy.Text("UI_A897AEB6CF42"), () => { _startupOpen = false; _panel = ""; _requestedFocus = null; Render(); }, primary: true));
        }
        else page.AddChild(Text(_startupProblem == "" ? UiCopy.Text("UI_E53165626D46") : _startupProblem, 18, _startupProblem == "" ? Ink : Danger));
        page.AddChild(Button("startup_new", _loadedSave || File.Exists(SavePath) ? UiCopy.Text("UI_D156EFF36978") : UiCopy.Text("UI_2077635A3A90"), () =>
        {
            void Create()
            {
                var previous = Session;
                try
                {
                    if (!_verify) SaveStore.ArchiveCurrent();
                    Session = new();
                    if (!Save()) { Session = previous; Render(); return; }
                    _startupOpen = false; _loadedSave = true; _startupProblem = ""; _saveWarning = "";
                    _readDiscoveries.Clear(); _discoveryOrder.Clear(); _dismissedOpportunities.Clear(); SavePreferences(); ReplaceSession(Session);
                }
                catch (Exception e) { _actionWarning = "无法保留旧档，尚未创建新进度：" + e.Message; Render(); }
            }
            if (_loadedSave || File.Exists(SavePath)) Confirm(UiCopy.Text("UI_6023273AF400"), UiCopy.Text("UI_4E691E602ACC"), Create, UiCopy.Text("UI_C7A4172320CD")); else Create();
        }));
        if (!_verify)
        {
            foreach (var source in SaveStore.RecoveryFiles())
            {
                ChapterSession? recovered = null;
                try { recovered = SaveStore.Read(source); } catch { }
                var id = "recover_" + page.GetChildCount();
                page.AddChild(Button(id, File.GetLastWriteTime(source).ToString("MM-dd HH:mm") + (recovered == null ? UiCopy.Text("UI_1DF155E6738B") : UiCopy.Format("UI_6E39283DC57A", recovered.State.Runs)), () =>
                    Confirm(UiCopy.Text("UI_A4388E08758B"), UiCopy.Text("UI_73767D28F940"), () =>
                    {
                        try { SaveStore.Restore(source); Session = SaveStore.Read(); _loadedSave = true; _startupProblem = _saveWarning = ""; Render(); }
                        catch (Exception e) { _actionWarning = "恢复失败，原文件已保留：" + e.Message; Render(); }
                    }, UiCopy.Text("UI_0D383BBB7809")), recovered != null));
            }
        }
        box.AddChild(Button("startup_quit", UiCopy.Text("UI_498E1D59B4D7"), () => GetTree().Quit()));
    }
}
