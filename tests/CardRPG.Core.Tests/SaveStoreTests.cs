using CardRPG.Core;
using Xunit;

namespace CardRPG.Core.Tests;

public class SaveStoreTests
{
    [Fact]
    public void SavesKeepPreviousVersionAndRestoreArchivesCurrent()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CardRPG-save-test-" + Guid.NewGuid()); Directory.CreateDirectory(directory);
        try
        {
            var store = new ChapterSaveStore(Path.Combine(directory, "save.json")); var first = new ChapterSession(); store.Save(first);
            var next = new ChapterSession(); next.Depart(); store.Save(next);
            Assert.Equal(first.ToJson(), store.Read(store.Path + ".bak").ToJson());
            store.Restore(store.Path + ".bak");
            Assert.Equal(first.ToJson(), store.Read().ToJson());
            Assert.Contains(store.RecoveryFiles(), f => f.Contains(".archive-") && store.Read(f).ToJson() == next.ToJson());
        }
        finally { foreach (var file in Directory.GetFiles(directory)) File.Delete(file); Directory.Delete(directory); }
    }
    [Fact]
    public void CorruptFileCanBeArchivedWithoutDestroyingRecoverableBackup()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CardRPG-save-test-" + Guid.NewGuid()); Directory.CreateDirectory(directory);
        try
        {
            var store = new ChapterSaveStore(Path.Combine(directory, "save.json")); var session = new ChapterSession(); store.Save(session); store.Save(session);
            File.WriteAllText(store.Path, "broken-json"); var archived = store.ArchiveCurrent();
            Assert.NotNull(archived); Assert.Equal("broken-json", File.ReadAllText(archived));
            store.Restore(store.Path + ".bak"); Assert.Equal(session.ToJson(), store.Read().ToJson());
            Assert.Throws<InvalidOperationException>(() => store.Restore(Path.Combine(directory, "unrelated.json")));
        }
        finally { foreach (var file in Directory.GetFiles(directory)) File.Delete(file); Directory.Delete(directory); }
    }
}
