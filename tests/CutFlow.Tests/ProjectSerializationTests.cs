using System.Text.Json;
using CutFlow.Models;
using CutFlow.Services;
using CutFlow.Utilities;
using CutFlow.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class ProjectSerializationTests
{
    [TestMethod]
    public async Task SaveAndLoadAsync_RoundTripsDocumentAndSchemaVersion()
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);
        var createdAt = new DateTimeOffset(2026, 8, 4, 12, 30, 0, TimeSpan.Zero);
        var project = ProjectDocument.CreateNew("Summer edit", createdAt);
        project.Settings.ApplyAspectRatio(AspectRatioPreset.Portrait9By16);
        project.Settings.VideoTrackVisible = false;
        project.Settings.TextTrackVisible = false;
        project.Settings.AudioTrackMuted = true;
        var asset = new ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = ProjectAssetKind.Video,
            SourcePath = @"C:\Media\summer.mp4",
            FileName = "summer.mp4",
            DurationMilliseconds = 12_000,
            Width = 1920,
            Height = 1080,
            FileSize = 123_456,
            LastWriteUtc = new DateTimeOffset(2026, 8, 4, 12, 0, 0, TimeSpan.Zero),
            ThumbnailCachePath = ThumbnailService.CreateRelativeCachePath(new string('a', 64)),
            IsMissing = true
        };
        project.Assets.Add(asset);
        var audioAsset = new ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = ProjectAssetKind.Audio,
            SourcePath = @"C:\Media\sound.wav",
            FileName = "sound.wav",
            DurationMilliseconds = 12_000,
            IsMissing = true
        };
        project.Assets.Add(audioAsset);
        project.VideoItems.Add(new VideoTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = asset.Id,
            SourceInMilliseconds = 100,
            SourceOutMilliseconds = 4_200
        });
        project.AudioItems.Add(new AudioTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = audioAsset.Id,
            StartMilliseconds = 250,
            SourceOutMilliseconds = 2_000
        });
        project.TextItems.Add(new TextTimelineItem { Id = Guid.NewGuid(), StartMilliseconds = 300, Text = "Hello" });

        await service.SaveAsync(project);
        var loaded = await service.LoadAsync(project.Id);
        var json = await File.ReadAllTextAsync(Path.Combine(directory.Path, "Projects", project.Id.ToString("D"), "project.json"));

        Assert.AreEqual(ProjectDocument.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.AreEqual(project.Id, loaded.Id);
        Assert.AreEqual(project.Name, loaded.Name);
        Assert.AreEqual(1080, loaded.Settings.Width);
        Assert.AreEqual(1920, loaded.Settings.Height);
        Assert.IsFalse(loaded.Settings.VideoTrackVisible);
        Assert.IsFalse(loaded.Settings.TextTrackVisible);
        Assert.IsTrue(loaded.Settings.AudioTrackMuted);
        Assert.AreEqual(2, loaded.Assets.Count);
        Assert.AreEqual(asset.SourcePath, loaded.Assets[0].SourcePath);
        Assert.AreEqual(asset.FileName, loaded.Assets[0].FileName);
        Assert.AreEqual(asset.Width, loaded.Assets[0].Width);
        Assert.AreEqual(asset.Height, loaded.Assets[0].Height);
        Assert.AreEqual(asset.FileSize, loaded.Assets[0].FileSize);
        Assert.AreEqual(asset.LastWriteUtc, loaded.Assets[0].LastWriteUtc);
        Assert.AreEqual(asset.ThumbnailCachePath, loaded.Assets[0].ThumbnailCachePath);
        Assert.IsTrue(loaded.Assets[0].IsMissing);
        Assert.AreEqual(1, loaded.VideoItems.Count);
        Assert.AreEqual(1, loaded.AudioItems.Count);
        Assert.AreEqual(1, loaded.TextItems.Count);
        StringAssert.Contains(json, "\"schemaVersion\": 1");
        StringAssert.Contains(json, $"cache/thumbnails/{new string('a', 64)}.jpg");
    }

    [TestMethod]
    public async Task LoadAsync_WhenThumbnailHashIsMalformed_DropsReferenceWithoutRewritingIt()
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var uppercaseHash = new string('A', 64);
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        var projectPath = Path.Combine(projectDirectory.FullName, "project.json");
        await File.WriteAllTextAsync(projectPath, $$"""
            {
              "schemaVersion": 1,
              "id": "{{id}}",
              "name": "Malformed thumbnail",
              "assets": [
                {
                  "id": "{{assetId}}",
                  "kind": "Video",
                  "sourcePath": "C:\\Media\\clip.mp4",
                  "thumbnailCachePath": "cache/thumbnails/{{uppercaseHash}}.jpg"
                }
              ]
            }
            """);
        var service = new ProjectService(directory.Path);

        var loaded = await service.LoadAsync(id);
        await service.SaveAsync(loaded);
        var savedJson = await File.ReadAllTextAsync(projectPath);

        Assert.AreEqual(string.Empty, loaded.Assets.Single().ThumbnailCachePath);
        Assert.IsFalse(savedJson.Contains(uppercaseHash, StringComparison.Ordinal));
        Assert.IsFalse(savedJson.Contains(uppercaseHash.ToLowerInvariant(), StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task LoadAsync_WhenOptionalPropertiesAreAbsent_UsesModelDefaults()
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), $$"""
            { "schemaVersion": 1, "id": "{{id}}", "name": "Older project", "createdAt": "2026-08-04T12:00:00+00:00", "modifiedAt": "2026-08-04T12:00:00+00:00" }
            """);

        var loaded = await new ProjectService(directory.Path).LoadAsync(id);

        Assert.IsNotNull(loaded.Settings);
        Assert.AreEqual(1920, loaded.Settings.Width);
        Assert.IsTrue(loaded.Settings.VideoTrackVisible);
        Assert.IsTrue(loaded.Settings.TextTrackVisible);
        Assert.IsFalse(loaded.Settings.AudioTrackMuted);
        Assert.HasCount(0, loaded.Assets);
        Assert.HasCount(0, loaded.VideoItems);
        Assert.HasCount(0, loaded.AudioItems);
        Assert.HasCount(0, loaded.TextItems);
    }

    [TestMethod]
    [DataRow("{ \"width\": 1280 }")]
    [DataRow("{ \"height\": 720 }")]
    [DataRow("{ \"width\": 1280, \"height\": 720 }")]
    [DataRow("{ \"width\": 0, \"height\": 0 }")]
    [DataRow("{ \"width\": 2147483647, \"height\": 2147483647 }")]
    public async Task LoadAsync_WhenLandscapeDimensionsArePartialOrUnsupported_UsesCanonicalPair(string settingsJson)
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), $$"""
            { "schemaVersion": 1, "id": "{{id}}", "name": "Canvas", "settings": {{settingsJson}} }
            """);

        var loaded = await new ProjectService(directory.Path).LoadAsync(id);

        Assert.AreEqual(AspectRatioPreset.Landscape16By9, loaded.Settings.AspectRatio);
        Assert.AreEqual(1920, loaded.Settings.Width);
        Assert.AreEqual(1080, loaded.Settings.Height);
        Assert.AreEqual(
            new PreviewStreamDimensions(1280, 720),
            PreviewStreamSize.Fit(loaded.Settings.Width, loaded.Settings.Height, 1280, 720));
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("-30")]
    [DataRow("24")]
    [DataRow("60")]
    public async Task LoadAsync_WhenFrameRateIsNotFixedValue_UsesThirtyFps(string frameRateJson)
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), $$"""
            { "schemaVersion": 1, "id": "{{id}}", "name": "Frame rate", "settings": { "frameRate": {{frameRateJson}} } }
            """);

        var loaded = await new ProjectService(directory.Path).LoadAsync(id);

        Assert.AreEqual(30d, loaded.Settings.FrameRate);
    }

    [TestMethod]
    public async Task LoadAndSaveAsync_PreserveAnUnusualNonblankLegacyNameExactly()
    {
        using var directory = new TemporaryDirectory();
        var unusualName = $"  Legacy\u0001{new string('N', ProjectDocument.MaximumNameLength + 10)}  ";
        var project = ProjectDocument.CreateNew(unusualName, DateTimeOffset.UnixEpoch);
        var projectDirectory = Directory.CreateDirectory(
            Path.Combine(directory.Path, "Projects", project.Id.ToString("D")));
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory.FullName, "project.json"),
            JsonSerializer.Serialize(project));
        var service = new ProjectService(directory.Path);

        var loaded = await service.LoadAsync(project.Id);
        Assert.AreEqual(unusualName, loaded.Name);

        await service.SaveAsync(loaded);
        Assert.AreEqual(unusualName, loaded.Name);
        Assert.AreEqual(unusualName, (await service.LoadAsync(project.Id)).Name);
    }

    [TestMethod]
    public async Task LoadAndSaveAsync_NormalizesExplicitAndMissingOffsetsToUtcWithoutLocalTimeDrift()
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), $$"""
            { "schemaVersion": 1, "id": "{{id}}", "name": "UTC project", "createdAt": "2026-08-09T23:59:59.9999999", "modifiedAt": "2026-08-10T02:00:00+03:00" }
            """);

        var savedAt = new DateTimeOffset(2026, 8, 11, 0, 0, 0, TimeSpan.Zero);
        var service = new ProjectService(directory.Path, utcNow: () => savedAt);
        var loaded = await service.LoadAsync(id);

        Assert.AreEqual(
            new DateTimeOffset(2026, 8, 9, 23, 59, 59, 999, 999, TimeSpan.Zero).AddTicks(9),
            loaded.CreatedAt);
        Assert.AreEqual(new DateTimeOffset(2026, 8, 9, 23, 0, 0, TimeSpan.Zero), loaded.ModifiedAt);
        Assert.AreEqual(TimeSpan.Zero, loaded.CreatedAt.Offset);
        Assert.AreEqual(TimeSpan.Zero, loaded.ModifiedAt.Offset);

        await service.SaveAsync(loaded);
        using var savedJson = JsonDocument.Parse(
            await File.ReadAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json")));

        Assert.AreEqual("2026-08-09T23:59:59.9999999+00:00", savedJson.RootElement.GetProperty("createdAt").GetString());
        Assert.AreEqual("2026-08-11T00:00:00+00:00", savedJson.RootElement.GetProperty("modifiedAt").GetString());
    }

    [TestMethod]
    public async Task LoadSaveLoadSaveAsync_StabilizesCanonicalValuesAfterFirstPass()
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var textId = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        var projectPath = Path.Combine(projectDirectory.FullName, "project.json");
        await File.WriteAllTextAsync(projectPath, $$"""
            {
              "schemaVersion": 1,
              "id": "{{id}}",
              "name": "Canonical values",
              "createdAt": "2026-08-09T12:34:56+03:00",
              "modifiedAt": "2026-08-09T12:34:56+03:00",
              "settings": {
                "width": 17,
                "height": 29,
                "frameRate": 60,
                "backgroundColor": "#ffaabbcc"
              },
              "assets": [
                {
                  "id": "{{assetId}}",
                  "kind": "Video",
                  "sourcePath": "C:\\Media\\.\\clip.mp4",
                  "fileName": "",
                  "durationMilliseconds": 5000
                }
              ],
              "textItems": [
                {
                  "id": "{{textId}}",
                  "startMilliseconds": -500,
                  "durationMilliseconds": 2000,
                  "fontFamily": " arial ",
                  "fontSize": 401,
                  "fontWeight": 0,
                  "textColor": "#aabbccdd",
                  "backgroundColor": "#001122aa",
                  "opacity": 2,
                  "normalizedX": 0.12345678901234568,
                  "normalizedY": -1
                }
              ]
            }
            """);
        var savedAt = new DateTimeOffset(2026, 8, 11, 0, 0, 0, TimeSpan.Zero);
        var service = new ProjectService(directory.Path, utcNow: () => savedAt);

        var firstPass = await service.LoadAsync(id);
        await service.SaveAsync(firstPass);
        var firstNormalizedJson = await File.ReadAllTextAsync(projectPath);

        var secondPass = await service.LoadAsync(id);
        await service.SaveAsync(secondPass);
        var secondNormalizedJson = await File.ReadAllTextAsync(projectPath);

        Assert.AreEqual(firstNormalizedJson, secondNormalizedJson);
        Assert.AreEqual(new DateTimeOffset(2026, 8, 9, 9, 34, 56, TimeSpan.Zero), secondPass.CreatedAt);
        Assert.AreEqual(savedAt, secondPass.ModifiedAt);
        Assert.AreEqual(@"C:\Media\clip.mp4", secondPass.Assets.Single().SourcePath);
        Assert.AreEqual("#FFAABBCC", secondPass.Settings.BackgroundColor);
        Assert.AreEqual("#AABBCCDD", secondPass.TextItems.Single().TextColor);
        Assert.AreEqual(0.12345678901234568, secondPass.TextItems.Single().NormalizedX);
    }

    [TestMethod]
    public async Task LoadSaveLoadSaveAsync_DoesNotProgressivelyTrimOrRemoveTimelineItems()
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var audioAssetId = Guid.NewGuid();
        var retainedVideoId = Guid.NewGuid();
        var secondVideoId = Guid.NewGuid();
        var audioItemId = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        var projectPath = Path.Combine(projectDirectory.FullName, "project.json");
        await File.WriteAllTextAsync(projectPath, $$"""
            {
              "schemaVersion": 1,
              "id": "{{id}}",
              "name": "Stable timeline repair",
              "assets": [
                {
                  "id": "{{audioAssetId}}",
                  "kind": "Audio",
                  "sourcePath": "C:\\Media\\sound.wav",
                  "durationMilliseconds": 2000
                }
              ],
              "videoItems": [
                {
                  "id": "{{retainedVideoId}}",
                  "sourceInMilliseconds": -1,
                  "sourceOutMilliseconds": 9223372036854775807,
                  "durationMilliseconds": 86399000
                },
                {
                  "id": "{{secondVideoId}}",
                  "durationMilliseconds": 1000
                }
              ],
              "audioItems": [
                {
                  "id": "{{audioItemId}}",
                  "assetId": "{{audioAssetId}}",
                  "startMilliseconds": -500,
                  "sourceInMilliseconds": -250,
                  "sourceOutMilliseconds": 9223372036854775807,
                  "fadeInMilliseconds": 9223372036854775807,
                  "fadeOutMilliseconds": -1
                }
              ]
            }
            """);
        var savedAt = new DateTimeOffset(2026, 8, 11, 0, 0, 0, TimeSpan.Zero);
        var service = new ProjectService(directory.Path, utcNow: () => savedAt);

        var firstPass = await service.LoadAsync(id);
        await service.SaveAsync(firstPass);
        var firstNormalizedJson = await File.ReadAllTextAsync(projectPath);

        var secondPass = await service.LoadAsync(id);
        await service.SaveAsync(secondPass);
        var secondNormalizedJson = await File.ReadAllTextAsync(projectPath);

        Assert.AreEqual(firstNormalizedJson, secondNormalizedJson);
        CollectionAssert.AreEqual(
            new[] { retainedVideoId, secondVideoId },
            secondPass.VideoItems.Select(item => item.Id).ToArray());
        Assert.AreEqual(86_399_000L, secondPass.VideoItems[0].DurationMilliseconds);
        Assert.AreEqual(1_000L, secondPass.VideoItems[1].DurationMilliseconds);
        var audio = secondPass.AudioItems.Single();
        Assert.AreEqual(audioItemId, audio.Id);
        Assert.AreEqual(0L, audio.StartMilliseconds);
        Assert.AreEqual(250L, audio.SourceInMilliseconds);
        Assert.AreEqual(2_000L, audio.SourceOutMilliseconds);
        Assert.AreEqual(1_750L, audio.DurationMilliseconds);
    }

    [TestMethod]
    public async Task LoadAsync_WhenV1CannotFitAllItems_RejectsInsteadOfRemovingItems()
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var firstItemId = Guid.NewGuid();
        var secondItemId = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), $$"""
            {
              "schemaVersion": 1,
              "id": "{{id}}",
              "videoItems": [
                { "id": "{{firstItemId}}", "durationMilliseconds": 86400000 },
                { "id": "{{secondItemId}}", "durationMilliseconds": 1000 }
              ]
            }
            """);

        var exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => new ProjectService(directory.Path).LoadAsync(id));

        StringAssert.Contains(exception.Message, "V1 timeline");
        Assert.IsTrue(File.Exists(Path.Combine(projectDirectory.FullName, "project.json")));
    }

    [TestMethod]
    public async Task LoadAsync_WhenTimelineItemIdentityIsMissingEmptyOrDuplicated_RejectsProject()
    {
        using var directory = new TemporaryDirectory();
        var duplicateId = Guid.NewGuid();
        var invalidTimelines = new[]
        {
            "\"videoItems\": [{ \"durationMilliseconds\": 1000 }]",
            "\"audioItems\": [{ \"id\": \"00000000-0000-0000-0000-000000000000\", \"sourceOutMilliseconds\": 1000 }]",
            $"\"textItems\": [{{ \"id\": \"{duplicateId}\" }}, {{ \"id\": \"{duplicateId}\" }}]",
            $"\"videoItems\": [{{ \"id\": \"{duplicateId}\", \"durationMilliseconds\": 1000 }}], \"audioItems\": [{{ \"id\": \"{duplicateId}\", \"sourceOutMilliseconds\": 1000 }}]"
        };

        foreach (var timelineJson in invalidTimelines)
        {
            var id = Guid.NewGuid();
            var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
            await File.WriteAllTextAsync(
                Path.Combine(projectDirectory.FullName, "project.json"),
                $$"""{ "schemaVersion": 1, "id": "{{id}}", {{timelineJson}} }""");

            var exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(
                () => new ProjectService(directory.Path).LoadAsync(id));
            StringAssert.Contains(exception.Message, "identities");
        }
    }

    [TestMethod]
    public async Task LoadAsync_WhenTimelineReferenceUsesWrongAssetKind_RejectsProject()
    {
        using var directory = new TemporaryDirectory();
        var invalidReferences = new[]
        {
            (ProjectAssetKind.Audio, @"C:\Media\source.wav", "videoItems", "\"durationMilliseconds\": 1000"),
            (ProjectAssetKind.Video, @"C:\Media\source.mp4", "audioItems", "\"sourceOutMilliseconds\": 1000")
        };

        foreach (var (assetKind, sourcePath, collectionName, itemTimingJson) in invalidReferences)
        {
            var id = Guid.NewGuid();
            var assetId = Guid.NewGuid();
            var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
            await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), $$"""
                {
                  "schemaVersion": 1,
                  "id": "{{id}}",
                  "assets": [{ "id": "{{assetId}}", "kind": "{{assetKind}}", "sourcePath": {{JsonSerializer.Serialize(sourcePath)}}, "durationMilliseconds": 1000 }],
                  "{{collectionName}}": [{ "id": "{{Guid.NewGuid()}}", "assetId": "{{assetId}}", {{itemTimingJson}} }]
                }
                """);

            var exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(
                () => new ProjectService(directory.Path).LoadAsync(id));
            StringAssert.Contains(exception.Message, "asset kind");
        }
    }

    [TestMethod]
    public async Task LoadAsync_WhenAssetMetadataIsAbsent_UsesBackwardCompatibleDefaults()
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), $$"""
            { "schemaVersion": 1, "id": "{{id}}", "name": "Older asset", "createdAt": "2026-08-04T12:00:00+00:00", "modifiedAt": "2026-08-04T12:00:00+00:00", "assets": [{ "id": "{{assetId}}", "kind": "Video", "sourcePath": "C:\\Media\\old.mp4", "durationMilliseconds": 1000, "isMissing": false }] }
            """);

        var loaded = await new ProjectService(directory.Path).LoadAsync(id);
        var asset = loaded.Assets.Single();

        Assert.AreEqual("old.mp4", asset.FileName);
        Assert.AreEqual(0, asset.Width);
        Assert.AreEqual(0, asset.Height);
        Assert.AreEqual(0UL, asset.FileSize);
        Assert.AreEqual(default, asset.LastWriteUtc);
        Assert.AreEqual(string.Empty, asset.ThumbnailCachePath);
    }

    [TestMethod]
    [DataRow(0L)]
    [DataRow(-1L)]
    [DataRow(9_000L)]
    public async Task LoadAsync_NormalizesVideoDurationToItsSourceRange(long storedDuration)
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), $$"""
            {
              "schemaVersion": 1,
              "id": "{{id}}",
              "name": "Stored video duration",
              "assets": [
                {
                  "id": "{{assetId}}",
                  "kind": "Video",
                  "sourcePath": "C:\\Media\\clip.mp4",
                  "durationMilliseconds": 10000
                }
              ],
              "videoItems": [
                {
                  "id": "{{itemId}}",
                  "assetId": "{{assetId}}",
                  "sourceInMilliseconds": 1000,
                  "sourceOutMilliseconds": 4000,
                  "durationMilliseconds": {{storedDuration}}
                }
              ]
            }
            """);

        var loaded = await new ProjectService(directory.Path).LoadAsync(id);

        Assert.AreEqual(3_000L, loaded.VideoItems.Single().DurationMilliseconds);
    }

    [TestMethod]
    public async Task LoadAsync_NormalizesMissingVideoSourceRangeAgainstStoredAssetDuration()
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), $$"""
            {
              "schemaVersion": 1,
              "id": "{{id}}",
              "name": "Missing bounded video",
              "assets": [
                {
                  "id": "{{assetId}}",
                  "kind": "Video",
                  "sourcePath": "C:\\Media\\missing.mp4",
                  "durationMilliseconds": 2000,
                  "isMissing": true
                }
              ],
              "videoItems": [
                {
                  "id": "{{Guid.NewGuid()}}",
                  "assetId": "{{assetId}}",
                  "sourceInMilliseconds": 1999,
                  "sourceOutMilliseconds": 9223372036854775807,
                  "durationMilliseconds": 5000
                }
              ]
            }
            """);

        var item = (await new ProjectService(directory.Path).LoadAsync(id)).VideoItems.Single();

        Assert.AreEqual(1_900L, item.SourceInMilliseconds);
        Assert.AreEqual(2_000L, item.SourceOutMilliseconds);
        Assert.AreEqual(ProjectDocument.MinimumItemDurationMilliseconds, item.DurationMilliseconds);
    }

    [TestMethod]
    public async Task LoadAsync_PreservesImageTimelineDurationIndependentOfAssetMetadataAndSourceRange()
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), $$"""
            {
              "schemaVersion": 1,
              "id": "{{id}}",
              "name": "Stored image duration",
              "assets": [
                {
                  "id": "{{assetId}}",
                  "kind": "Image",
                  "sourcePath": "C:\\Media\\still.jpg",
                  "durationMilliseconds": 5000
                }
              ],
              "videoItems": [
                {
                  "id": "{{itemId}}",
                  "assetId": "{{assetId}}",
                  "sourceInMilliseconds": 4999,
                  "sourceOutMilliseconds": 12000,
                  "durationMilliseconds": 12000
                }
              ]
            }
            """);

        var loaded = await new ProjectService(directory.Path).LoadAsync(id);

        Assert.AreEqual(5_000L, loaded.Assets.Single().DurationMilliseconds);
        Assert.AreEqual(4_900L, loaded.VideoItems.Single().SourceInMilliseconds);
        Assert.AreEqual(5_000L, loaded.VideoItems.Single().SourceOutMilliseconds);
        Assert.AreEqual(12_000L, loaded.VideoItems.Single().DurationMilliseconds);
    }

    [TestMethod]
    public async Task LoadAndSaveAsync_IgnoresLegacyAudioDurationAndSerializesOnlyNormalizedSourceRange()
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        var projectPath = Path.Combine(projectDirectory.FullName, "project.json");
        await File.WriteAllTextAsync(projectPath, $$"""
            {
              "schemaVersion": 1,
              "id": "{{id}}",
              "name": "Legacy audio duration",
              "audioItems": [
                {
                  "id": "{{itemId}}",
                  "sourceInMilliseconds": 4000,
                  "sourceOutMilliseconds": 1000,
                  "durationMilliseconds": 9000
                }
              ]
            }
            """);
        var service = new ProjectService(directory.Path);

        var loaded = await service.LoadAsync(id);
        var audio = loaded.AudioItems.Single();

        Assert.AreEqual(4_000L, audio.SourceInMilliseconds);
        Assert.AreEqual(4_100L, audio.SourceOutMilliseconds);
        Assert.AreEqual(ProjectDocument.MinimumItemDurationMilliseconds, audio.DurationMilliseconds);

        await service.SaveAsync(loaded);
        using var savedJson = JsonDocument.Parse(await File.ReadAllTextAsync(projectPath));
        var savedAudio = savedJson.RootElement.GetProperty("audioItems")[0];
        Assert.IsFalse(savedAudio.TryGetProperty("durationMilliseconds", out _));
    }

    [TestMethod]
    public async Task AudioCompatibilityFades_LoadRelinkAndProjectDuplicateKeepNormalizedValues()
    {
        using var directory = new TemporaryDirectory();
        var projectId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(
            Path.Combine(directory.Path, "Projects", projectId.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), $$"""
            {
              "schemaVersion": 1,
              "id": "{{projectId}}",
              "name": "Compatibility fades",
              "assets": [
                {
                  "id": "{{assetId}}",
                  "kind": "Audio",
                  "sourcePath": "C:\\Media\\known.wav",
                  "durationMilliseconds": 2000
                }
              ],
              "audioItems": [
                {
                  "id": "{{Guid.NewGuid()}}",
                  "assetId": "{{assetId}}",
                  "sourceInMilliseconds": 1000,
                  "sourceOutMilliseconds": 2000,
                  "fadeInMilliseconds": 9223372036854775807,
                  "fadeOutMilliseconds": -1
                }
              ]
            }
            """);
        var service = new ProjectService(directory.Path);

        var loaded = await service.LoadAsync(projectId);
        var audio = loaded.AudioItems.Single();
        Assert.AreEqual(1_000L, audio.DurationMilliseconds);
        Assert.AreEqual(1_000L, audio.FadeInMilliseconds);
        Assert.AreEqual(0L, audio.FadeOutMilliseconds);

        var replacement = new MediaFileMetadata(
            @"D:\New\known.wav",
            "known.wav",
            ProjectAssetKind.Audio,
            DurationMilliseconds: 2_000,
            Width: 0,
            Height: 0,
            FileSize: 1,
            LastWriteUtc: DateTimeOffset.UnixEpoch);
        Assert.IsTrue(MediaImportService.TryApplyRelink(loaded.Assets.Single(), replacement, loaded, out var error));
        Assert.IsNull(error);
        Assert.AreEqual(1_000L, audio.FadeInMilliseconds);
        Assert.AreEqual(0L, audio.FadeOutMilliseconds);

        await service.SaveAsync(loaded);
        var duplicate = await service.DuplicateAsync(projectId);
        var duplicatedAudio = duplicate.AudioItems.Single();
        Assert.AreEqual(1_000L, duplicatedAudio.DurationMilliseconds);
        Assert.AreEqual(1_000L, duplicatedAudio.FadeInMilliseconds);
        Assert.AreEqual(0L, duplicatedAudio.FadeOutMilliseconds);
    }

    [TestMethod]
    public async Task LoadAsync_NormalizesAudioSourceRangeAgainstKnownAssetDuration()
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), $$"""
            {
              "schemaVersion": 1,
              "id": "{{id}}",
              "name": "Bounded audio",
              "assets": [
                {
                  "id": "{{assetId}}",
                  "kind": "Audio",
                  "sourcePath": "C:\\Media\\known.wav",
                  "durationMilliseconds": 2000
                }
              ],
              "audioItems": [
                {
                  "id": "{{Guid.NewGuid()}}",
                  "assetId": "{{assetId}}",
                  "startMilliseconds": 9223372036854775807,
                  "sourceInMilliseconds": 1000,
                  "sourceOutMilliseconds": 9223372036854775807
                },
                {
                  "id": "{{Guid.NewGuid()}}",
                  "assetId": "{{assetId}}",
                  "startMilliseconds": 0,
                  "sourceInMilliseconds": 1900,
                  "sourceOutMilliseconds": -9223372036854775808
                }
              ]
            }
            """);

        var items = (await new ProjectService(directory.Path).LoadAsync(id)).AudioItems;

        Assert.AreEqual(1_000L, items[0].SourceInMilliseconds);
        Assert.AreEqual(2_000L, items[0].SourceOutMilliseconds);
        Assert.AreEqual(1_000L, items[0].DurationMilliseconds);
        Assert.AreEqual(
            ProjectDocument.MaximumTimelineDurationMilliseconds - items[0].DurationMilliseconds,
            items[0].StartMilliseconds);
        Assert.AreEqual(ProjectDocument.MaximumTimelineDurationMilliseconds, items[0].StartMilliseconds + items[0].DurationMilliseconds);
        Assert.AreEqual(1_900L, items[1].SourceInMilliseconds);
        Assert.AreEqual(2_000L, items[1].SourceOutMilliseconds);
        Assert.AreEqual(ProjectDocument.MinimumItemDurationMilliseconds, items[1].DurationMilliseconds);
    }

    [TestMethod]
    public async Task LoadAsync_RepairsAudioLeftEdgesLikeTrimWithoutMovingRightEdge()
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var firstItemId = Guid.NewGuid();
        var secondItemId = Guid.NewGuid();
        var overlapItemId = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), $$"""
            {
              "schemaVersion": 1,
              "id": "{{id}}",
              "name": "Audio left edges",
              "assets": [
                {
                  "id": "{{assetId}}",
                  "kind": "Audio",
                  "sourcePath": "C:\\Media\\known.wav",
                  "durationMilliseconds": 2000
                }
              ],
              "audioItems": [
                {
                  "id": "{{firstItemId}}",
                  "assetId": "{{assetId}}",
                  "startMilliseconds": 1000,
                  "sourceInMilliseconds": -500,
                  "sourceOutMilliseconds": 2000
                },
                {
                  "id": "{{secondItemId}}",
                  "assetId": "{{assetId}}",
                  "startMilliseconds": -500,
                  "sourceInMilliseconds": 0,
                  "sourceOutMilliseconds": 2000
                },
                {
                  "id": "{{overlapItemId}}",
                  "assetId": "{{assetId}}",
                  "startMilliseconds": 750,
                  "sourceInMilliseconds": 0,
                  "sourceOutMilliseconds": 1000
                }
              ]
            }
            """);

        var items = (await new ProjectService(directory.Path).LoadAsync(id)).AudioItems.ToDictionary(item => item.Id);

        Assert.AreEqual(1_500L, items[firstItemId].StartMilliseconds);
        Assert.AreEqual(0L, items[firstItemId].SourceInMilliseconds);
        Assert.AreEqual(3_500L, items[firstItemId].StartMilliseconds + items[firstItemId].DurationMilliseconds);
        Assert.AreEqual(0L, items[secondItemId].StartMilliseconds);
        Assert.AreEqual(500L, items[secondItemId].SourceInMilliseconds);
        Assert.AreEqual(1_500L, items[secondItemId].StartMilliseconds + items[secondItemId].DurationMilliseconds);
        Assert.AreEqual(750L, items[overlapItemId].StartMilliseconds);
        Assert.AreEqual(0L, items[overlapItemId].SourceInMilliseconds);
        Assert.AreEqual(1_000L, items[overlapItemId].SourceOutMilliseconds);
    }

    [TestMethod]
    public async Task LoadAsync_WhenAssetIdentityIsAbsentEmptyOrDuplicated_RejectsProject()
    {
        using var directory = new TemporaryDirectory();
        var assetId = Guid.NewGuid();
        var invalidAssets = new[]
        {
            $$"""[{ "kind": "Video", "sourcePath": "C:\\Media\\clip.mp4" }]""",
            """[{ "id": "00000000-0000-0000-0000-000000000000", "kind": "Video", "sourcePath": "C:\\Media\\clip.mp4" }]""",
            $$"""[{ "id": "{{assetId}}", "sourcePath": "C:\\Media\\clip.mp4" }]""",
            $$"""[{ "id": "{{assetId}}", "kind": "Video", "sourcePath": "C:\\Media\\clip.mp4" }, { "id": "{{assetId}}", "kind": "Video", "sourcePath": "D:\\Media\\clip.mp4" }]"""
        };

        foreach (var assetsJson in invalidAssets)
        {
            var id = Guid.NewGuid();
            var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
            await File.WriteAllTextAsync(
                Path.Combine(projectDirectory.FullName, "project.json"),
                $$"""{ "schemaVersion": 1, "id": "{{id}}", "name": "Invalid asset", "assets": {{assetsJson}} }""");

            await Assert.ThrowsExactlyAsync<InvalidDataException>(
                () => new ProjectService(directory.Path).LoadAsync(id));
        }
    }

    [TestMethod]
    public async Task LoadAsync_WhenAssetSourceIsRelative_QuarantinesItBeforeMissingRefreshOrTimelineUse()
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var sourcePath = Path.Combine(directory.Path, "relative-source.mp4");
        await File.WriteAllTextAsync(sourcePath, "not decoded during missing refresh");
        var relativeSourcePath = Path.GetRelativePath(Environment.CurrentDirectory, sourcePath);
        Assert.IsFalse(Path.IsPathFullyQualified(relativeSourcePath));
        Assert.IsTrue(File.Exists(relativeSourcePath));
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), $$"""
            {
              "schemaVersion": 1,
              "id": "{{id}}",
              "name": "Relative source",
              "assets": [
                {
                  "id": "{{assetId}}",
                  "kind": "Video",
                  "sourcePath": {{JsonSerializer.Serialize(relativeSourcePath)}},
                  "durationMilliseconds": 1000,
                  "thumbnailCachePath": "cache\\thumbnails\\stale.jpg",
                  "isMissing": false
                }
              ]
            }
            """);

        var loaded = await new ProjectService(directory.Path).LoadAsync(id);
        var asset = loaded.Assets.Single();

        Assert.AreEqual(string.Empty, asset.SourcePath);
        Assert.AreEqual(string.Empty, asset.ThumbnailCachePath);
        Assert.IsTrue(asset.IsMissing);

        await new MediaImportService().RefreshMissingAsync(loaded);

        Assert.IsTrue(asset.IsMissing);
        Assert.IsFalse(new EditorViewModel(loaded).AddAssetToTimeline(asset.Id));
    }

    [TestMethod]
    public async Task LoadAsync_WhenSettingsAndListsAreExplicitJsonNull_NormalizesSafeDefaults()
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), $$"""
            { "schemaVersion": 1, "id": "{{id}}", "name": "Null values", "createdAt": "2026-08-04T12:00:00+00:00", "modifiedAt": "2026-08-04T12:00:00+00:00", "settings": null, "assets": null, "videoItems": null, "audioItems": null, "textItems": null }
            """);

        var loaded = await new ProjectService(directory.Path).LoadAsync(id);

        Assert.IsNotNull(loaded.Settings);
        Assert.HasCount(0, loaded.Assets);
        Assert.HasCount(0, loaded.VideoItems);
        Assert.HasCount(0, loaded.AudioItems);
        Assert.HasCount(0, loaded.TextItems);
    }

    [TestMethod]
    [DataRow("assets")]
    [DataRow("videoItems")]
    [DataRow("audioItems")]
    [DataRow("textItems")]
    public async Task LoadAsync_WhenCollectionContainsNullElement_ThrowsInvalidDataException(string propertyName)
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), $$"""
            { "schemaVersion": 1, "id": "{{id}}", "name": "Null item", "{{propertyName}}": [null] }
            """);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => new ProjectService(directory.Path).LoadAsync(id));
    }

    [TestMethod]
    public async Task LoadAsync_WhenReferenceStringsAreExplicitJsonNull_NormalizesModelDefaults()
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), $$"""
            {
              "schemaVersion": 1,
              "id": "{{id}}",
              "name": null,
              "assets": [
                {
                  "id": "{{assetId}}",
                  "kind": "Video",
                  "sourcePath": null,
                  "fileName": null,
                  "thumbnailCachePath": null
                }
              ]
            }
            """);

        var loaded = await new ProjectService(directory.Path).LoadAsync(id);
        var asset = loaded.Assets.Single();

        Assert.AreEqual(string.Empty, loaded.Name);
        Assert.AreEqual(string.Empty, asset.SourcePath);
        Assert.AreEqual(string.Empty, asset.FileName);
        Assert.AreEqual(string.Empty, asset.ThumbnailCachePath);
        Assert.IsTrue(asset.IsMissing);
    }

    [TestMethod]
    public async Task SaveAndLoadAsync_NormalizesTimelineAndFontBoundaries()
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);
        var project = ProjectDocument.CreateNew("Unsafe text", DateTimeOffset.UnixEpoch);
        project.TextItems.AddRange(
        [
            new TextTimelineItem { Id = Guid.NewGuid(), StartMilliseconds = -1_000, DurationMilliseconds = 1, FontSize = 1 },
            new TextTimelineItem { Id = Guid.NewGuid(), StartMilliseconds = long.MaxValue, DurationMilliseconds = long.MaxValue, FontSize = 401 },
            new TextTimelineItem { Id = Guid.NewGuid(), FontSize = double.NaN },
            new TextTimelineItem { Id = Guid.NewGuid(), FontSize = double.PositiveInfinity }
        ]);
        project.VideoItems.Add(new VideoTimelineItem
        {
            Id = Guid.NewGuid(),
            SourceInMilliseconds = long.MaxValue,
            SourceOutMilliseconds = long.MaxValue,
            DurationMilliseconds = long.MaxValue
        });
        project.AudioItems.Add(new AudioTimelineItem
        {
            Id = Guid.NewGuid(),
            StartMilliseconds = long.MaxValue,
            SourceInMilliseconds = long.MaxValue,
            SourceOutMilliseconds = long.MaxValue
        });

        await service.SaveAsync(project);
        var loaded = await service.LoadAsync(project.Id);
        var items = loaded.TextItems;

        Assert.AreEqual(0L, items[0].StartMilliseconds);
        Assert.AreEqual(ProjectDocument.MinimumItemDurationMilliseconds, items[0].DurationMilliseconds);
        Assert.AreEqual(8d, items[0].FontSize);
        Assert.AreEqual(ProjectDocument.MaximumTimelineDurationMilliseconds - ProjectDocument.MinimumItemDurationMilliseconds, items[1].StartMilliseconds);
        Assert.AreEqual(ProjectDocument.MinimumItemDurationMilliseconds, items[1].DurationMilliseconds);
        Assert.AreEqual(ProjectDocument.MaximumTimelineDurationMilliseconds, items[1].StartMilliseconds + items[1].DurationMilliseconds);
        Assert.AreEqual(400d, items[1].FontSize);
        Assert.AreEqual(TextTimelineItem.DefaultFontSize, items[2].FontSize);
        Assert.AreEqual(TextTimelineItem.DefaultFontSize, items[3].FontSize);
        Assert.AreEqual(ProjectDocument.MaximumTimelineDurationMilliseconds - ProjectDocument.MinimumItemDurationMilliseconds, loaded.VideoItems[0].SourceInMilliseconds);
        Assert.AreEqual(ProjectDocument.MaximumTimelineDurationMilliseconds, loaded.VideoItems[0].SourceOutMilliseconds);
        Assert.AreEqual(ProjectDocument.MaximumTimelineDurationMilliseconds, loaded.VideoItems[0].DurationMilliseconds);
        Assert.AreEqual(ProjectDocument.MaximumTimelineDurationMilliseconds - ProjectDocument.MinimumItemDurationMilliseconds, loaded.AudioItems[0].StartMilliseconds);
        Assert.AreEqual(ProjectDocument.MaximumTimelineDurationMilliseconds - ProjectDocument.MinimumItemDurationMilliseconds, loaded.AudioItems[0].SourceInMilliseconds);
        Assert.AreEqual(ProjectDocument.MaximumTimelineDurationMilliseconds, loaded.AudioItems[0].SourceOutMilliseconds);
    }

    [TestMethod]
    public async Task SaveAndLoadAsync_NormalizesTextTimingWithoutMovingSafeEdges()
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);
        var project = ProjectDocument.CreateNew("Text timing", DateTimeOffset.UnixEpoch);
        project.TextItems.AddRange(
        [
            new TextTimelineItem { Id = Guid.NewGuid(), StartMilliseconds = -500, DurationMilliseconds = 2_000 },
            new TextTimelineItem { Id = Guid.NewGuid(), StartMilliseconds = 1_000, DurationMilliseconds = 0 },
            new TextTimelineItem { Id = Guid.NewGuid(), StartMilliseconds = ProjectDocument.MaximumTimelineDurationMilliseconds, DurationMilliseconds = 3_000 },
            new TextTimelineItem { Id = Guid.NewGuid(), StartMilliseconds = ProjectDocument.MaximumTimelineDurationMilliseconds - 1_000, DurationMilliseconds = 5_000 }
        ]);

        await service.SaveAsync(project);
        var loaded = await service.LoadAsync(project.Id);
        var items = loaded.TextItems;

        Assert.AreEqual(0L, items[0].StartMilliseconds);
        Assert.AreEqual(1_500L, items[0].DurationMilliseconds);
        Assert.AreEqual(1_000L, items[1].StartMilliseconds);
        Assert.AreEqual(ProjectDocument.MinimumItemDurationMilliseconds, items[1].DurationMilliseconds);
        Assert.AreEqual(ProjectDocument.MaximumTimelineDurationMilliseconds - ProjectDocument.MinimumItemDurationMilliseconds, items[2].StartMilliseconds);
        Assert.AreEqual(ProjectDocument.MinimumItemDurationMilliseconds, items[2].DurationMilliseconds);
        Assert.AreEqual(ProjectDocument.MaximumTimelineDurationMilliseconds - 1_000, items[3].StartMilliseconds);
        Assert.AreEqual(1_000L, items[3].DurationMilliseconds);
        Assert.IsTrue(TimelineEditingService.IsWithinProjectDurationLimit(loaded));
    }

    [TestMethod]
    public async Task SaveAsync_WhenSerializationFails_DoesNotDeleteAnUnownedTempFile()
    {
        using var directory = new TemporaryDirectory();
        var project = ProjectDocument.CreateNew("Safe project", DateTimeOffset.UnixEpoch);
        var originalModifiedAt = new DateTimeOffset(2026, 8, 4, 9, 0, 0, TimeSpan.Zero);
        project.ModifiedAt = originalModifiedAt;
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", project.Id.ToString("D")));
        var projectPath = Path.Combine(projectDirectory.FullName, "project.json");
        var temporaryPath = Path.Combine(projectDirectory.FullName, "project.json.tmp");
        const string originalJson = "{\"schemaVersion\":1,\"name\":\"previous\"}";
        await File.WriteAllTextAsync(projectPath, originalJson);
        await File.WriteAllTextAsync(temporaryPath, "stale temporary data");
        var service = new ProjectService(directory.Path, _ => throw new InvalidOperationException("Deterministic serializer failure"));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.SaveAsync(project));

        Assert.AreEqual(originalModifiedAt, project.ModifiedAt);
        Assert.AreEqual(originalJson, await File.ReadAllTextAsync(projectPath));
        Assert.AreEqual("stale temporary data", await File.ReadAllTextAsync(temporaryPath));
    }

    [TestMethod]
    public async Task SaveAsync_DoesNotPublishModifiedAtToTheProjectBeforeCommitSucceeds()
    {
        using var directory = new TemporaryDirectory();
        var project = ProjectDocument.CreateNew("Safe project", DateTimeOffset.UnixEpoch);
        var originalModifiedAt = project.ModifiedAt;
        var observedModifiedAt = default(DateTimeOffset);
        var service = new ProjectService(directory.Path, _ =>
        {
            observedModifiedAt = project.ModifiedAt;
            throw new InvalidOperationException("Deterministic serializer failure");
        });

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.SaveAsync(project));

        Assert.AreEqual(originalModifiedAt, observedModifiedAt);
        Assert.AreEqual(originalModifiedAt, project.ModifiedAt);
    }

    [TestMethod]
    public async Task SaveAsync_WhenPreCanceled_PreservesModifiedAtExistingJsonAndLeavesNoTempFile()
    {
        using var directory = new TemporaryDirectory();
        var project = ProjectDocument.CreateNew("Canceled save", DateTimeOffset.UnixEpoch);
        var originalModifiedAt = new DateTimeOffset(2026, 8, 4, 9, 0, 0, TimeSpan.Zero);
        project.ModifiedAt = originalModifiedAt;
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", project.Id.ToString("D")));
        var projectPath = Path.Combine(projectDirectory.FullName, "project.json");
        const string originalJson = "{\"schemaVersion\":1,\"name\":\"previous\"}";
        await File.WriteAllTextAsync(projectPath, originalJson);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => new ProjectService(directory.Path).SaveAsync(project, cancellation.Token));

        Assert.AreEqual(originalModifiedAt, project.ModifiedAt);
        Assert.AreEqual(originalJson, await File.ReadAllTextAsync(projectPath));
        Assert.IsFalse(File.Exists(projectPath + ".tmp"));
    }

    [TestMethod]
    public async Task SaveAsync_WhenCanceledDuringSerialization_DoesNotStartWritingOrChangePriorJson()
    {
        using var directory = new TemporaryDirectory();
        var initialService = new ProjectService(directory.Path);
        var project = await initialService.CreateAsync("Canceled serialization");
        var projectPath = Path.Combine(directory.Path, "Projects", project.Id.ToString("D"), "project.json");
        var originalJson = await File.ReadAllTextAsync(projectPath);
        var originalModifiedAt = project.ModifiedAt;
        using var cancellation = new CancellationTokenSource();
        var writeStarted = false;
        var service = new ProjectService(
            directory.Path,
            serialize: document =>
            {
                var json = JsonSerializer.Serialize(document);
                cancellation.Cancel();
                return json;
            },
            writeAndFlushAsync: (_, _, _) =>
            {
                writeStarted = true;
                return Task.CompletedTask;
            });
        project.Name = "Must not be written";

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => service.SaveAsync(project, cancellation.Token));

        Assert.IsFalse(writeStarted);
        Assert.AreEqual(originalModifiedAt, project.ModifiedAt);
        Assert.AreEqual(originalJson, await File.ReadAllTextAsync(projectPath));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SaveAsync_WhenCanceledAfterTempFlush_PreservesDestinationStateModifiedAtAndUnownedTempFile(
        bool destinationExists)
    {
        using var directory = new TemporaryDirectory();
        var initialService = new ProjectService(directory.Path);
        var project = destinationExists
            ? await initialService.CreateAsync("Canceled after flush")
            : ProjectDocument.CreateNew("Canceled first save", DateTimeOffset.UnixEpoch);
        var projectPath = Path.Combine(directory.Path, "Projects", project.Id.ToString("D"), "project.json");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        var unownedTemporaryPath = projectPath + ".tmp";
        var originalJson = destinationExists ? await File.ReadAllTextAsync(projectPath) : null;
        var originalModifiedAt = project.ModifiedAt;
        await File.WriteAllTextAsync(unownedTemporaryPath, "another operation");
        using var cancellation = new CancellationTokenSource();
        var service = new ProjectService(
            directory.Path,
            writeAndFlushAsync: async (path, json, _) =>
            {
                await File.WriteAllTextAsync(path, json);
                cancellation.Cancel();
            });
        project.Name = "Must not be committed";

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => service.SaveAsync(project, cancellation.Token));

        Assert.AreEqual(originalModifiedAt, project.ModifiedAt);
        Assert.AreEqual(destinationExists, File.Exists(projectPath));
        if (destinationExists)
        {
            Assert.AreEqual(originalJson, await File.ReadAllTextAsync(projectPath));
        }
        Assert.AreEqual("another operation", await File.ReadAllTextAsync(unownedTemporaryPath));
    }

    [TestMethod]
    public async Task SaveAsync_WhenOwnedTempCleanupIsUnauthorized_PreservesTheCancellationOutcome()
    {
        using var directory = new TemporaryDirectory();
        var initialService = new ProjectService(directory.Path);
        var project = await initialService.CreateAsync("Cleanup failure");
        var projectPath = Path.Combine(directory.Path, "Projects", project.Id.ToString("D"), "project.json");
        var originalJson = await File.ReadAllTextAsync(projectPath);
        var originalModifiedAt = project.ModifiedAt;
        using var cancellation = new CancellationTokenSource();
        string? ownedTemporaryPath = null;
        var service = new ProjectService(
            directory.Path,
            writeAndFlushAsync: async (path, json, _) =>
            {
                ownedTemporaryPath = path;
                await File.WriteAllTextAsync(path, json);
                File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
                cancellation.Cancel();
            });
        project.Name = "Must not be committed";

        try
        {
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(
                () => service.SaveAsync(project, cancellation.Token));

            Assert.AreEqual(originalModifiedAt, project.ModifiedAt);
            Assert.AreEqual(originalJson, await File.ReadAllTextAsync(projectPath));
            Assert.IsNotNull(ownedTemporaryPath);
            Assert.IsTrue(File.Exists(ownedTemporaryPath));
        }
        finally
        {
            if (ownedTemporaryPath is not null && File.Exists(ownedTemporaryPath))
            {
                File.SetAttributes(ownedTemporaryPath, FileAttributes.Normal);
                File.Delete(ownedTemporaryPath);
            }
        }
    }

    [TestMethod]
    public async Task SaveAsync_ConcurrentRequestsCommitInInvocationOrder()
    {
        using var directory = new TemporaryDirectory();
        var initialService = new ProjectService(directory.Path);
        var project = await initialService.CreateAsync("Initial");
        var firstRevisionSerialized = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serializationCount = 0;
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var service = new ProjectService(directory.Path, document =>
        {
            var json = JsonSerializer.Serialize(document, jsonOptions);
            if (Interlocked.Increment(ref serializationCount) == 1)
            {
                firstRevisionSerialized.TrySetResult();
                releaseFirstSave.Task.GetAwaiter().GetResult();
            }

            return json;
        });
        project.Name = "First revision";
        var firstSave = Task.Run(() => service.SaveAsync(project));
        await firstRevisionSerialized.Task.WaitAsync(TimeSpan.FromSeconds(5));

        project.Name = "Latest revision";
        var latestSave = service.SaveAsync(project);

        Assert.AreEqual(1, serializationCount);
        releaseFirstSave.TrySetResult();
        await Task.WhenAll(firstSave, latestSave);
        Assert.AreEqual(2, serializationCount);
        Assert.AreEqual("Latest revision", (await service.LoadAsync(project.Id)).Name);
    }

    [TestMethod]
    public async Task SaveAsync_WhenDestinationIsLocked_PreservesModifiedAtExistingJsonAndLeavesNoTempFile()
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);
        var project = ProjectDocument.CreateNew("Locked destination", DateTimeOffset.UnixEpoch);
        await service.SaveAsync(project);
        var projectPath = Path.Combine(directory.Path, "Projects", project.Id.ToString("D"), "project.json");
        var originalJson = await File.ReadAllTextAsync(projectPath);
        var originalModifiedAt = project.ModifiedAt;
        project.Name = "Changed while locked";

        await using (new FileStream(projectPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await Assert.ThrowsExactlyAsync<IOException>(() => service.SaveAsync(project));
        }

        Assert.AreEqual(originalModifiedAt, project.ModifiedAt);
        Assert.AreEqual(originalJson, await File.ReadAllTextAsync(projectPath));
        Assert.IsFalse(File.Exists(projectPath + ".tmp"));
    }

    [TestMethod]
    public async Task LoadAsync_WhenSchemaVersionIsMissing_ThrowsInvalidDataException()
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), $$"""
            { "id": "{{id}}", "name": "Missing schema", "createdAt": "2026-08-04T12:00:00+00:00", "modifiedAt": "2026-08-04T12:00:00+00:00" }
            """);

        var exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => new ProjectService(directory.Path).LoadAsync(id));

        Assert.AreEqual("The project schema version is missing.", exception.Message);
    }

    [TestMethod]
    [DataRow("null")]
    [DataRow("\"1\"")]
    [DataRow("true")]
    [DataRow("{}")]
    [DataRow("1.5")]
    public async Task LoadAsync_WhenSchemaVersionHasInvalidJsonValue_ThrowsSchemaError(string schemaVersion)
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), $$"""
            { "schemaVersion": {{schemaVersion}}, "id": "{{id}}", "name": "Invalid schema" }
            """);

        var exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => new ProjectService(directory.Path).LoadAsync(id));

        Assert.AreEqual("The project schema version is invalid.", exception.Message);
    }

    [TestMethod]
    public async Task LoadAsync_WhenSchemaVersionIsZero_ThrowsUnsupportedSchemaError()
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), $$"""
            { "schemaVersion": 0, "id": "{{id}}", "name": "Zero schema" }
            """);

        var exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => new ProjectService(directory.Path).LoadAsync(id));

        Assert.AreEqual("Unsupported project schema version 0.", exception.Message);
    }

    [TestMethod]
    public async Task SaveAsync_WhenSchemaVersionIsInvalid_PreservesExistingJsonAndDoesNotCreateTempFile()
    {
        using var directory = new TemporaryDirectory();
        var project = ProjectDocument.CreateNew("Invalid schema", DateTimeOffset.UnixEpoch);
        project.SchemaVersion = ProjectDocument.CurrentSchemaVersion + 1;
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", project.Id.ToString("D")));
        var projectPath = Path.Combine(projectDirectory.FullName, "project.json");
        const string originalJson = "{\"schemaVersion\":1,\"name\":\"previous\"}";
        await File.WriteAllTextAsync(projectPath, originalJson);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => new ProjectService(directory.Path).SaveAsync(project));

        Assert.AreEqual(originalJson, await File.ReadAllTextAsync(projectPath));
        Assert.IsFalse(File.Exists(projectPath + ".tmp"));
    }

    [TestMethod]
    public async Task LoadAsync_WhenJsonIsMalformed_ThrowsInvalidDataException()
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), "{ invalid JSON");

        var exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => new ProjectService(directory.Path).LoadAsync(id));

        Assert.AreEqual("The project JSON could not be read.", exception.Message);
    }

    [TestMethod]
    public async Task SettingsAndLogServices_PersistOnlyBoundedCallerSuppliedContent()
    {
        using var directory = new TemporaryDirectory();
        var settingsService = new SettingsService(directory.Path);
        var settings = new AppSettings { WindowWidth = 1440, WindowHeight = 900, TimelineZoom = 2.5, LoopPlayback = true };
        await settingsService.SaveAsync(settings);

        var loadedSettings = await settingsService.LoadAsync();
        Assert.AreEqual(1440d, loadedSettings.WindowWidth);
        Assert.IsTrue(loadedSettings.LoopPlayback);

        var sourceFile = Path.Combine(directory.Path, "external-media.txt");
        const string sourceContents = "media bytes must not become a log entry";
        await File.WriteAllTextAsync(sourceFile, sourceContents);
        var log = new SimpleLogService(directory.Path, maximumEntries: 2, maximumMessageLength: 24);
        await log.WriteAsync("first technical failure");
        await log.WriteAsync("second technical failure");
        await log.WriteAsync("third technical failure");

        var logText = await File.ReadAllTextAsync(Path.Combine(directory.Path, "cutflow.log"));
        Assert.AreEqual(2, File.ReadLines(Path.Combine(directory.Path, "cutflow.log")).Count());
        Assert.IsFalse(logText.Contains(sourceContents, StringComparison.Ordinal));
        Assert.IsFalse(logText.Contains("first technical failure", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task SettingsService_WhenJsonIsMalformed_ThrowsInvalidDataException()
    {
        using var directory = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "settings.json"), "{ invalid JSON");

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => new SettingsService(directory.Path).LoadAsync());
    }

    [TestMethod]
    public async Task SimpleLogService_ConcurrentWritesNormalizeAndTruncateEachLine()
    {
        using var directory = new TemporaryDirectory();
        var log = new SimpleLogService(directory.Path, maximumEntries: 20, maximumMessageLength: 8);

        await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => log.WriteAsync("line\r\none and more")));

        var lines = await File.ReadAllLinesAsync(Path.Combine(directory.Path, "cutflow.log"));
        Assert.AreEqual(12, lines.Length);
        Assert.IsTrue(lines.All(line => line.EndsWith("line one", StringComparison.Ordinal)));
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
