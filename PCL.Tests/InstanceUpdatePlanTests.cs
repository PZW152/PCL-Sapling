namespace PCL.Tests;

public class InstanceUpdatePlanTests {
    [Test]
    public async Task 清单保留实际JSON文件名并按Windows路径比较管理文件() {
        string Root = Path.Combine(Path.GetTempPath(), "PclInstance-" + Guid.NewGuid().ToString("N"));
        string Build = Path.Combine(Root, "build");
        string Game = Path.Combine(Root, "game");
        Directory.CreateDirectory(Path.Combine(Build, "mods"));
        Directory.CreateDirectory(Path.Combine(Build, "resources"));
        Directory.CreateDirectory(Game);
        try {
            File.WriteAllText(Path.Combine(Build, "Instance.jar"), "new jar");
            File.WriteAllText(Path.Combine(Build, "Instance.json"), "{}");
            File.WriteAllText(Path.Combine(Build, "mods", "Keep.jar"), "new mod");
            File.WriteAllText(Path.Combine(Build, "resources", "legacy.ogg"), "legacy sound");
            string OldJson = Path.Combine(Game, "other-name.json");
            string Jar = Path.Combine(Game, "Instance.jar");
            var Plan = InstanceUpdatePlan.Prepare(Build, Game, OldJson, Jar, new[] { @"mods\keep.jar", "mods/remove.jar" }, new[] { "mods/Keep.jar" });

            await Assert.That(Plan.VersionFiles[1].Target).IsEqualTo(OldJson);
            await Assert.That(Plan.Recycle.ToArray()).IsEquivalentTo(new[] { OldJson, Path.ChangeExtension(OldJson, ".jar"), Jar, Path.Combine(Game, "mods", "remove.jar") });
            await Assert.That(Plan.Resources.Single().Target).IsEqualTo(Path.Combine(Game, "resources", "legacy.ogg"));
            await Assert.That(Directory.GetFileSystemEntries(Game)).IsEmpty();
        } finally { Directory.Delete(Root, true); }
    }

    [Test]
    public async Task 缺少产物或目标被文件夹占用时保留旧实例() {
        string Root = Path.Combine(Path.GetTempPath(), "PclInstance-" + Guid.NewGuid().ToString("N"));
        string Build = Path.Combine(Root, "build");
        string Game = Path.Combine(Root, "game");
        Directory.CreateDirectory(Build);
        Directory.CreateDirectory(Game);
        try {
            string Json = Path.Combine(Game, "Instance.json");
            string Jar = Path.Combine(Game, "Instance.jar");
            File.WriteAllText(Json, "old json");
            File.WriteAllText(Jar, "old jar");
            File.WriteAllText(Path.Combine(Build, "Instance.jar"), "new jar");
            await Assert.That(() => InstanceUpdatePlan.Prepare(Build, Game, Json, Jar, Array.Empty<string>(), Array.Empty<string>())).Throws<FileNotFoundException>();
            File.WriteAllText(Path.Combine(Build, "Instance.json"), "new json");
            Directory.CreateDirectory(Path.Combine(Game, "mods", "new.jar"));
            Directory.CreateDirectory(Path.Combine(Build, "mods"));
            File.WriteAllText(Path.Combine(Build, "mods", "new.jar"), "new mod");
            await Assert.That(() => InstanceUpdatePlan.Prepare(Build, Game, Json, Jar, Array.Empty<string>(), new[] { "mods/new.jar" })).Throws<IOException>();

            await Assert.That(File.ReadAllText(Json)).IsEqualTo("old json");
            await Assert.That(File.ReadAllText(Jar)).IsEqualTo("old jar");
        } finally { Directory.Delete(Root, true); }
    }
}
