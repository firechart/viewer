using System.Reflection;
using FireChart.Viewer.Core;
using FireChart.Viewer.Library;

namespace FireChart.Viewer.Tests.Viewer;

// The Viewer is a component of its own over the public client libraries of Flox: its assemblies know nothing of the rest of Flox and of
// FireChart except its texts. It is also the boundary of the public part: the allowed names are exactly the public projects
public class ViewerIsolationTests
{
    private static readonly Assembly[] s_viewer = [typeof(FhirRenderer).Assembly, typeof(VendorTemplates).Assembly, typeof(FireChart.Viewer.ViewerSession).Assembly];

    // The libraries of Flox that are public, the only ones the Viewer may use
    private static readonly HashSet<string> s_publicFlox = new(StringComparer.Ordinal)
    {
        "Flox.Core",
        "Flox.Resources",
        "Flox.Shared"
    };

    [Test]
    public void TheAssembliesOfTheViewer_ReferenceOnlyThePublicProjects()
    {
        var forbidden = s_viewer.SelectMany(assembly => assembly.GetReferencedAssemblies()
                                                                .Where(reference => IsForbidden(reference.Name ?? string.Empty))
                                                                .Select(reference => $"{assembly.GetName().Name} -> {reference.Name}"))
                                .ToList();

        Assert.That(forbidden, Is.Empty);
    }

    [Test]
    public void ThePublicFlox_IsAllowed_TheRestIsNot()
    {
        Assert.Multiple(() =>
        {
            Assert.That(IsForbidden("Flox.Core"), Is.False);
            Assert.That(IsForbidden("Flox.Shared"), Is.False);
            Assert.That(IsForbidden("Flox.Server"), Is.True, "any other library of Flox");
            Assert.That(IsForbidden("FireChart.Server"), Is.True, "any other project of FireChart");
            Assert.That(IsForbidden("FireChart.Resources"), Is.False, "the texts are kept in one place");
            Assert.That(IsForbidden("FireChart.Viewer.Core"), Is.False);
        });
    }

    private static bool IsForbidden(string name)
    {
        return (name.StartsWith("Flox", StringComparison.Ordinal) && !s_publicFlox.Contains(name))
               || (name.StartsWith("FireChart.", StringComparison.Ordinal) && !name.StartsWith("FireChart.Viewer", StringComparison.Ordinal)
                   && name != "FireChart.Resources");
    }
}
