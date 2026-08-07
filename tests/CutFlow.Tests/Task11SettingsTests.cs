using CutFlow.Services;
using CutFlow.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class Task11SettingsTests
{
    [TestMethod]
    public void ClampPhysicalToWorkArea_EnforcesMinimumSizeAndVisiblePositionAt96Dpi()
    {
        var geometry = WindowGeometry.ClampPhysicalToWorkArea(
            new WindowGeometry(-500, 900, 20, 40),
            new WindowGeometry(20, 40, 1600, 1000),
            targetDpi: 96);

        Assert.AreEqual(1180, geometry.Width);
        Assert.AreEqual(720, geometry.Height);
        Assert.AreEqual(20, geometry.X);
        Assert.AreEqual(320, geometry.Y);
    }

    [TestMethod]
    public void ClampPhysicalToWorkArea_PrefersVisibilityWhenWorkAreaIsSmallerThanMinimum()
    {
        var geometry = WindowGeometry.ClampPhysicalToWorkArea(
            new WindowGeometry(-100, -100, 1500, 900),
            new WindowGeometry(0, 0, 1000, 600),
            targetDpi: 96);

        Assert.AreEqual(1000, geometry.Width);
        Assert.AreEqual(600, geometry.Height);
        Assert.AreEqual(0, geometry.X);
        Assert.AreEqual(0, geometry.Y);
    }

    [TestMethod]
    public void GetEffectiveMinimumSize_PrefersVisibilityOnSmallDisplay()
    {
        var size = WindowGeometry.GetEffectiveMinimumSize(1000, 600, targetDpi: 96);

        Assert.AreEqual(1000, size.Width);
        Assert.AreEqual(600, size.Height);
    }

    [TestMethod]
    public void PhysicalRestore_PreservesSavedPixelsAndUsesTargetDpiForMinimumSize()
    {
        var settings = new AppSettings
        {
            WindowBoundsVersion = AppSettings.CurrentWindowBoundsVersion,
            WindowPixelX = -1800,
            WindowPixelY = 120,
            WindowPixelWidth = 900,
            WindowPixelHeight = 500
        };

        Assert.IsTrue(WindowGeometry.TryGetStoredPhysicalBounds(settings, out var requested));
        Assert.AreEqual(new WindowGeometry(-1800, 120, 900, 500), requested);

        var restored = WindowGeometry.ClampPhysicalToWorkArea(
            requested,
            new WindowGeometry(-2560, 0, 2560, 1440),
            targetDpi: 144);

        Assert.AreEqual(new WindowGeometry(-1800, 120, 1770, 1080), restored);
    }

    [TestMethod]
    public void LegacyLogicalRestore_MigratesAtTargetDisplayDpi()
    {
        var requested = WindowGeometry.FromLegacyLogicalSettings(new AppSettings
        {
            WindowX = 40,
            WindowY = 60,
            WindowWidth = 1500,
            WindowHeight = 900
        }, targetDpi: 144);

        Assert.AreEqual(new WindowGeometry(60, 90, 2250, 1350), requested);
    }

    [TestMethod]
    public void ScaleFactorToDpi_UsesTheMonitorScalePercentage()
    {
        Assert.AreEqual(96u, WindowGeometry.ScaleFactorToDpi(100));
        Assert.AreEqual(144u, WindowGeometry.ScaleFactorToDpi(150));
        Assert.AreEqual(216u, WindowGeometry.ScaleFactorToDpi(225));
    }

    [TestMethod]
    public async Task LoadAsync_WhenSettingsFileIsMissing_ReturnsValidWorkspaceDefaults()
    {
        using var directory = new TemporaryDirectory();

        var settings = await new SettingsService(directory.Path).LoadAsync();

        Assert.AreEqual(1500d, settings.WindowWidth);
        Assert.AreEqual(900d, settings.WindowHeight);
        Assert.AreEqual(80d, settings.TimelineZoom);
        Assert.AreEqual(300d, settings.TimelineHeight);
    }

    [TestMethod]
    public async Task SaveAndLoadAsync_NormalizesInvalidWorkspacePreferences()
    {
        using var directory = new TemporaryDirectory();
        var service = new SettingsService(directory.Path);

        await service.SaveAsync(new AppSettings
        {
            WindowWidth = 0,
            WindowHeight = double.NaN,
            TimelineZoom = 999,
            TimelineHeight = -1
        });

        var settings = await service.LoadAsync();

        Assert.AreEqual(1500d, settings.WindowWidth);
        Assert.AreEqual(900d, settings.WindowHeight);
        Assert.AreEqual(400d, settings.TimelineZoom);
        Assert.AreEqual(300d, settings.TimelineHeight);
    }

    [TestMethod]
    public async Task SaveAndLoadAsync_PreservesWorkspacePreferences()
    {
        using var directory = new TemporaryDirectory();
        var service = new SettingsService(directory.Path);

        await service.SaveAsync(new AppSettings
        {
            WindowWidth = 1600,
            WindowHeight = 960,
            TimelineZoom = 240,
            TimelineHeight = 420,
            LoopPlayback = true
        });

        var settings = await service.LoadAsync();

        Assert.AreEqual(240d, settings.TimelineZoom);
        Assert.AreEqual(420d, settings.TimelineHeight);
        Assert.IsTrue(settings.LoopPlayback);
    }

    [TestMethod]
    public async Task SaveAndLoadAsync_PreservesVersionedPhysicalWindowBounds()
    {
        using var directory = new TemporaryDirectory();
        var service = new SettingsService(directory.Path);

        await service.SaveAsync(new AppSettings
        {
            WindowBoundsVersion = AppSettings.CurrentWindowBoundsVersion,
            WindowPixelX = -1800,
            WindowPixelY = 120,
            WindowPixelWidth = 1600,
            WindowPixelHeight = 960
        });

        var settings = await service.LoadAsync();

        Assert.AreEqual(AppSettings.CurrentWindowBoundsVersion, settings.WindowBoundsVersion);
        Assert.AreEqual(-1800, settings.WindowPixelX);
        Assert.AreEqual(120, settings.WindowPixelY);
        Assert.AreEqual(1600, settings.WindowPixelWidth);
        Assert.AreEqual(960, settings.WindowPixelHeight);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CutFlow.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
