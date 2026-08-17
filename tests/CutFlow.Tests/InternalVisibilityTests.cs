using System.Reflection;
using System.Runtime.CompilerServices;
using CutFlow.Models;
using CutFlow.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class InternalVisibilityTests
{
    private static readonly string[] expected = new[] { "CutFlow.Tests" };

    [TestMethod]
    public void AppAssembly_ExposesInternalsOnlyToTheIntendedTestAssembly()
    {
        var friendAssemblies = typeof(ProjectService).Assembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName)
            .ToArray();

        Assert.AreSequenceEqual(expected, friendAssemblies);
    }

    [TestMethod]
    public void DeterministicTestHooks_AreNotPartOfThePublicApi()
    {
        var appAssembly = typeof(ProjectService).Assembly;
        var renderDelegate = appAssembly.GetType("CutFlow.Services.ExportRenderAsync");
        Assert.IsNotNull(renderDelegate);
        Assert.IsFalse(renderDelegate.IsPublic);

        var publicConstructors = typeof(ProjectService).GetConstructors();
        Assert.HasCount(1, publicConstructors);
        Assert.HasCount(0, publicConstructors[0].GetParameters());

        var refreshMissingParameterTypes = new[]
        {
            typeof(ProjectDocument),
            typeof(Func<string, bool>),
            typeof(CancellationToken)
        };
        Assert.IsNull(typeof(MediaImportService).GetMethod(
            "RefreshMissing",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            refreshMissingParameterTypes,
            modifiers: null));
        Assert.IsNotNull(typeof(MediaImportService).GetMethod(
            "RefreshMissing",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            refreshMissingParameterTypes,
            modifiers: null));
    }
}
