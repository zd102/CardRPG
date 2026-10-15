namespace CardRPG.Core;

public sealed class ChapterSaveStore(string path)
{
    public string Path { get; } = System.IO.Path.GetFullPath(path);
    public ChapterSession Read(string? source = null) => ChapterSession.FromJson(File.ReadAllText(source ?? Path));
    public IReadOnlyList<string> RecoveryFiles() => Directory.Exists(System.IO.Path.GetDirectoryName(Path))
        ? Directory.EnumerateFiles(System.IO.Path.GetDirectoryName(Path)!, System.IO.Path.GetFileName(Path) + ".*")
            .Where(p => p.EndsWith(".bak") || p.Contains(".archive-") || p.Contains(".invalid-"))
            .OrderByDescending(File.GetLastWriteTimeUtc).Take(10).ToArray() : [];
    public void Save(ChapterSession session)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        if (File.Exists(Path))
        {
            try { _ = Read(); File.Copy(Path, Path + ".bak", true); }
            catch (Exception e) when (e is System.Text.Json.JsonException or InvalidDataException or ArgumentException or InvalidOperationException) { }
        }
        File.WriteAllText(Path + ".tmp", session.ToJson());
        File.Move(Path + ".tmp", Path, true);
    }
    public string? ArchiveCurrent()
    {
        if (!File.Exists(Path)) return null;
        var archive = Path + ".archive-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "-" + Guid.NewGuid().ToString("N")[..8];
        File.Copy(Path, archive, false); return archive;
    }
    public void Restore(string source)
    {
        if (!RecoveryFiles().Contains(System.IO.Path.GetFullPath(source))) throw new InvalidOperationException("恢复文件不属于当前存档。");
        var session = Read(source); ArchiveCurrent(); Save(session);
    }
}
