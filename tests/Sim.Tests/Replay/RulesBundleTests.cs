using Rts.Data;

namespace Rts.Sim.Tests.Replay;

public class RulesBundleTests
{
    private static string RulesDir() => RulesLocator.FindRulesDir(AppContext.BaseDirectory)!;

    [Fact]
    public void RulesLoadedFromTheirFileText_HashLikeTheDirectory()
    {
        var files = RulesBundle.ReadDirectory(RulesDir());
        Assert.Equal(RulesBundle.FileNames, files.Select(f => f.Name).ToList());
        Assert.Equal(RulesLoader.Load(RulesDir()).RulesHash, RulesBundle.Load(files).RulesHash);
    }

    [Fact]
    public void EmbeddedRules_SurviveTheReplayHeader()
    {
        var files = RulesBundle.ReadDirectory(RulesDir());
        var sim = Rts.Sim.Tests.Determinism.ScriptedGame.NewSim(Rts.Sim.Tests.Determinism.ScriptedGame.Setup());
        var ms = new MemoryStream();
        new Rts.Sim.Replay.ReplayWriter(ms, sim, "b", files).Finish();
        var data = Rts.Sim.Replay.ReplayReader.Read(new MemoryStream(ms.ToArray()));
        Assert.Equal(files, data.RulesFiles);
        Assert.Equal(RulesLoader.Load(RulesDir()).RulesHash, RulesBundle.Load(data.RulesFiles).RulesHash);
        Assert.True(ms.Length < files.Sum(f => f.Text.Length), "the rules blob should be compressed");
    }

    [Fact]
    public void MissingFile_IsReportedLikeTheDirectoryLoaderDoes()
    {
        var files = RulesBundle.ReadDirectory(RulesDir()).Where(f => f.Name != RulesLoader.TerrainFile).ToList();
        var ex = Assert.Throws<RulesException>(() => RulesBundle.Load(files));
        Assert.Contains(ex.Problems, p => p.File == RulesLoader.TerrainFile);
    }
}
