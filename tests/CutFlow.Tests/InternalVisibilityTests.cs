using System.Reflection;
using System.Runtime.CompilerServices;
using CutFlow.Models;
using CutFlow.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class InternalVisibilityTests
{
    [TestMethod]
    public void AppAssembly_ExposesInternalsOnlyToTheIntendedTestAssembly()
    {
        var friendAssemblies = typeof(ProjectService).Assembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName)
            .ToArray();

        CollectionAssert.AreEqual(new[] { "CutFlow.Tests" }, friendAssemblies);
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
            typeof(Func<string, bool>)
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
