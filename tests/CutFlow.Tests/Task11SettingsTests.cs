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
    [DataRow(600d, 720d, 300d)]
    [DataRow(600d, 900d, 480d)]
    [DataRow(600d, 1020d, 600d)]
    [DataRow(600d, 600d, 180d)]
    [DataRow(600d, 0d, 600d)]
    public void TimelineHeight_IsLimitedByTheAvailableWindowHeight(
        double requestedHeight,
        double windowHeight,
        double expectedHeight)
    {
        var height = AppSettings.NormalizeTimelineHeightForWindow(requestedHeight, windowHeight);

        Assert.AreEqual(expectedHeight, height);
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
    public void CloseBounds_WhenWindowIsRestored_UseCurrentGeometry()
    {
        var current = new WindowGeometry(40, 60, 1400, 840);
        var previous = new WindowGeometry(20, 30, 1180, 720);

        var selected = WindowGeometry.SelectBoundsForPersistence(current, previous, isRestored: true);

        Assert.AreEqual(current, selected);
    }

    [TestMethod]
    public void CloseBounds_WhenWindowIsMaximized_UseLastRestoredGeometry()
    {
        var maximized = new WindowGeometry(0, 0, 1920, 1040);
        var restored = new WindowGeometry(120, 80, 1400, 840);

        var selected = WindowGeometry.SelectBoundsForPersistence(maximized, restored, isRestored: false);

        Assert.AreEqual(restored, selected);
    }

    [TestMethod]
    public void CloseBounds_WhenWindowIsMinimized_UseLastRestoredGeometry()
    {
        var minimized = new WindowGeometry(-32000, -32000, 160, 28);
        var restored = new WindowGeometry(-1800, 120, 1600, 960);

        var selected = WindowGeometry.SelectBoundsForPersistence(minimized, restored, isRestored: false);

        Assert.AreEqual(restored, selected);
    }

    [TestMethod]
    public void CloseBounds_WhenCapturedDisplayWasRemoved_ClampToCurrentWorkArea()
    {
        var captured = new WindowGeometry(3200, 120, 1600, 960);

        var validated = WindowGeometry.ClampPhysicalToWorkArea(
            captured,
            new WindowGeometry(0, 0, 1920, 1040),
            targetDpi: 96);

        Assert.AreEqual(new WindowGeometry(320, 80, 1600, 960), validated);
    }

    [TestMethod]
    public void TargetDisplay_WhenBoundsIntersectMultipleDisplays_SelectsLargestIntersectionBeforePrimary()
    {
        WindowDisplayGeometry[] displays =
        [
            new(new WindowGeometry(0, 0, 1920, 1080), 96, IsPrimary: true),
            new(new WindowGeometry(1920, 0, 2560, 1440), 144)
        ];

        var targetDisplay = WindowGeometry.SelectTargetDisplay(
            new WindowGeometry(1700, 100, 1180, 720),
            displays);

        Assert.AreEqual(1, targetDisplay);
    }

    [TestMethod]
    public void TargetDisplay_WithNegativeCoordinates_SelectsIntersectingSecondaryDisplay()
    {
        WindowDisplayGeometry[] displays =
        [
            new(new WindowGeometry(0, 0, 1920, 1080), 96, IsPrimary: true),
            new(new WindowGeometry(-2560, 0, 2560, 1440), 144)
        ];

        var targetDisplay = WindowGeometry.SelectTargetDisplay(
            new WindowGeometry(-1800, 120, 1600, 960),
            displays);

        Assert.AreEqual(1, targetDisplay);
    }

    [TestMethod]
    public void TargetDisplay_WhenSavedMonitorWasRemoved_SelectsNearestConnectedDisplay()
    {
        WindowDisplayGeometry[] displays =
        [
            new(new WindowGeometry(0, 0, 1920, 1080), 96, IsPrimary: true),
            new(new WindowGeometry(1920, 0, 2560, 1440), 144)
        ];

        var targetDisplay = WindowGeometry.SelectTargetDisplay(
            new WindowGeometry(5000, 100, 1600, 960),
            displays);

        Assert.AreEqual(1, targetDisplay);
    }

    [TestMethod]
    public void TargetDisplay_WithEqualGeometry_IsIndependentOfDisplayEnumerationOrder()
    {
        var left = new WindowDisplayGeometry(new WindowGeometry(-1920, 0, 1920, 1080), 96);
        var right = new WindowDisplayGeometry(new WindowGeometry(0, 0, 1920, 1080), 96);
        var requested = new WindowGeometry(-590, 100, 1180, 720);

        WindowDisplayGeometry[] leftFirst = [left, right];
        WindowDisplayGeometry[] rightFirst = [right, left];
        var firstSelection = WindowGeometry.SelectTargetDisplay(requested, leftFirst);
        var secondSelection = WindowGeometry.SelectTargetDisplay(requested, rightFirst);

        Assert.AreEqual(left, leftFirst[firstSelection]);
        Assert.AreEqual(left, rightFirst[secondSelection]);
    }

    [TestMethod]
    public void TargetDisplay_WithEqualGeometry_PrefersPrimaryDisplay()
    {
        WindowDisplayGeometry[] displays =
        [
            new(new WindowGeometry(-1920, 0, 1920, 1080), 96),
            new(new WindowGeometry(0, 0, 1920, 1080), 96, IsPrimary: true)
        ];

        var targetDisplay = WindowGeometry.SelectTargetDisplay(
            new WindowGeometry(-590, 100, 1180, 720),
            displays);

        Assert.AreEqual(1, targetDisplay);
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
    public void LegacyLogicalRestore_SelectsMixedDpiDisplayUsingEachDisplaysPhysicalCandidate()
    {
        var settings = new AppSettings
        {
            WindowX = 1300,
            WindowY = 100,
            WindowWidth = 1180,
            WindowHeight = 720
        };
        WindowDisplayGeometry[] displays =
        [
            new(new WindowGeometry(0, 0, 1920, 1080), 96, IsPrimary: true),
            new(new WindowGeometry(1920, 0, 2560, 1440), 144)
        ];

        var targetDisplay = WindowGeometry.SelectLegacyTargetDisplay(settings, displays);
        var requested = WindowGeometry.FromLegacyLogicalSettings(settings, displays[targetDisplay].Dpi);

        Assert.AreEqual(1, targetDisplay);
        Assert.AreEqual(new WindowGeometry(1950, 150, 1770, 1080), requested);
    }

    [TestMethod]
    public void LegacyLogicalRestore_MixedDpiCandidateSizeDoesNotBiasDisplaySelection()
    {
        var settings = new AppSettings
        {
            WindowX = 700,
            WindowY = 100,
            WindowWidth = 1180,
            WindowHeight = 720
        };
        WindowDisplayGeometry[] displays =
        [
            new(new WindowGeometry(0, 0, 1920, 1080), 96, IsPrimary: true),
            new(new WindowGeometry(1920, 0, 2560, 1440), 144)
        ];

        var targetDisplay = WindowGeometry.SelectLegacyTargetDisplay(settings, displays);

        Assert.AreEqual(0, targetDisplay);
    }

    [TestMethod]
    public void LegacyLogicalRestore_WhenOriginalDisplayIsDisconnected_SelectsNearestConnectedDisplay()
    {
        var settings = new AppSettings
        {
            WindowX = 4000,
            WindowY = 100,
            WindowWidth = 1180,
            WindowHeight = 720
        };
        WindowDisplayGeometry[] displays =
        [
            new(new WindowGeometry(0, 0, 1920, 1080), 96, IsPrimary: true),
            new(new WindowGeometry(1920, 0, 2560, 1440), 144)
        ];

        var targetDisplay = WindowGeometry.SelectLegacyTargetDisplay(settings, displays);
        var requested = WindowGeometry.FromLegacyLogicalSettings(settings, displays[targetDisplay].Dpi);
        var restored = WindowGeometry.ClampPhysicalToWorkArea(requested, displays[targetDisplay].PhysicalBounds, displays[targetDisplay].Dpi);

        Assert.AreEqual(1, targetDisplay);
        Assert.AreEqual(new WindowGeometry(2710, 150, 1770, 1080), restored);
    }

    [TestMethod]
    public void LegacyLogicalRestore_InvalidDimensionsRemainPositiveAfterConversion()
    {
        var requested = WindowGeometry.FromLegacyLogicalSettings(new AppSettings
        {
            WindowWidth = double.Epsilon,
            WindowHeight = double.NaN
        }, targetDpi: 1);

        Assert.IsTrue(requested.Width > 0);
        Assert.IsTrue(requested.Height > 0);
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
