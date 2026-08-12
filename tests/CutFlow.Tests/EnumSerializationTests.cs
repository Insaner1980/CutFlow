using System.Text.Json;
using CutFlow.Models;
using CutFlow.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class EnumSerializationTests
{
    [TestMethod]
    public void ModelJsonEnums_RoundTripEveryValueAsItsCanonicalString()
    {
        AssertCanonicalValues<AspectRatioPreset>();
        AssertCanonicalValues<ProjectAssetKind>();
        AssertCanonicalValues<TextHorizontalAlignment>();
        AssertCanonicalValues<TextPreset>();
    }

    [TestMethod]
    public void ModelJsonEnums_RejectNonCanonicalValues()
    {
        AssertRejected<AspectRatioPreset>("0");
        AssertRejected<AspectRatioPreset>("\"landscape16by9\"");
        AssertRejected<AspectRatioPreset>("\"FutureAspect\"");
        AssertRejected<ProjectAssetKind>("0");
        AssertRejected<ProjectAssetKind>("\"video\"");
        AssertRejected<ProjectAssetKind>("\"FutureAsset\"");
        AssertRejected<TextHorizontalAlignment>("0");
        AssertRejected<TextHorizontalAlignment>("\"center\"");
        AssertRejected<TextHorizontalAlignment>("\"FutureAlignment\"");
        AssertRejected<TextPreset>("0");
        AssertRejected<TextPreset>("\"default\"");
        AssertRejected<TextPreset>("\"FuturePreset\"");

        AssertUndefinedValueRejected((AspectRatioPreset)99);
        AssertUndefinedValueRejected((ProjectAssetKind)99);
        AssertUndefinedValueRejected((TextHorizontalAlignment)99);
        AssertUndefinedValueRejected((TextPreset)99);
    }

    [TestMethod]
    [DataRow("\"settings\": { \"aspectRatio\": 0 }")]
    [DataRow("\"settings\": { \"aspectRatio\": 99 }")]
    [DataRow("\"settings\": { \"aspectRatio\": \"landscape16by9\" }")]
    [DataRow("\"settings\": { \"aspectRatio\": \"FutureAspect\" }")]
    [DataRow("\"assets\": [{ \"kind\": 0 }]")]
    [DataRow("\"assets\": [{ \"kind\": 99 }]")]
    [DataRow("\"assets\": [{ \"kind\": \"video\" }]")]
    [DataRow("\"assets\": [{ \"kind\": \"FutureAsset\" }]")]
    [DataRow("\"textItems\": [{ \"alignment\": 0 }]")]
    [DataRow("\"textItems\": [{ \"alignment\": 99 }]")]
    [DataRow("\"textItems\": [{ \"alignment\": \"center\" }]")]
    [DataRow("\"textItems\": [{ \"alignment\": \"FutureAlignment\" }]")]
    public async Task LoadAsync_WhenPersistedEnumValueIsNotCanonical_RejectsProject(string enumProperty)
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory.FullName, "project.json"),
            $$"""{ "schemaVersion": 1, "id": "{{id}}", {{enumProperty}} }""");

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => new ProjectService(directory.Path).LoadAsync(id));
    }

    private static void AssertRejected<TEnum>(string json)
        where TEnum : struct, Enum =>
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<TEnum>(json));

    private static void AssertCanonicalValues<TEnum>()
        where TEnum : struct, Enum
    {
        foreach (var value in Enum.GetValues<TEnum>())
        {
            var name = Enum.GetName(value)!;
            var json = JsonSerializer.Serialize(value);

            Assert.AreEqual(JsonSerializer.Serialize(name), json);
            Assert.AreEqual(value, JsonSerializer.Deserialize<TEnum>(json));
        }
    }

    private static void AssertUndefinedValueRejected<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Serialize(value));

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
