using System.Collections;
using System.Reflection;
using FireChart.Viewer.Core;
using FireChart.Viewer.Core.Forms;
using FireChart.Viewer.Library;

namespace FireChart.Viewer.Tests.Viewer;

// The vendor search forms of src/FireChart.Viewer.Library/search-forms/{release}/{type}.json (git first): the assembly carries every file, each file is a
// valid form in its canonical text, and every resource type of a release with search parameters has one (once the files of the release are there)
public class VendorSearchFormsTests
{
    // The releases of the FHIR core packages and the Firely assembly that knows their search parameters
    private static readonly Dictionary<string, string> s_releases = new(StringComparer.Ordinal)
    {
        ["r3"] = "Hl7.Fhir.STU3",
        ["r4"] = "Hl7.Fhir.R4",
        ["r4b"] = "Hl7.Fhir.R4B",
        ["r5"] = "Hl7.Fhir.R5"
    };

    private static string Folder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FireChart.Viewer.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "src", "FireChart.Viewer.Library", "search-forms");
    }

    private static IEnumerable<string> Files(string release)
    {
        var folder = Path.Combine(Folder(), release);
        return Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "*.json") : [];
    }

    // The resource types with search parameters of their own that a form can show (not composite, special or of every resource)
    private static SortedSet<string> SearchableTypes(string release)
    {
        var modelInfo = Type.GetType($"Hl7.Fhir.Model.ModelInfo, {s_releases[release]}", true)!;
        var member = (MemberInfo?)modelInfo.GetProperty("SearchParameters", BindingFlags.Public | BindingFlags.Static) ?? modelInfo.GetField("SearchParameters", BindingFlags.Public | BindingFlags.Static);
        var parameters = (IEnumerable)(member is PropertyInfo property ? property.GetValue(null) : ((FieldInfo)member!).GetValue(null))!;
        var types = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var parameter in parameters)
        {
            var resource = (string?)parameter.GetType().GetProperty("Resource")!.GetValue(parameter);
            var name = (string?)parameter.GetType().GetProperty("Name")!.GetValue(parameter);
            var type = parameter.GetType().GetProperty("Type")!.GetValue(parameter)!.ToString();
            if (resource is not (null or "Resource" or "DomainResource") && name is not null && !name.StartsWith('_') && type is not ("Composite" or "Special"))
            {
                types.Add(resource);
            }
        }

        return types;
    }

    [Test]
    public void TheAssembly_CarriesEveryFile_AndEveryFileIsAFormInItsCanonicalText()
    {
        var folders = Directory.Exists(Folder()) ? Directory.EnumerateDirectories(Folder()).Select(Path.GetFileName).ToList() : [];
        Assert.That(folders, Is.SubsetOf(s_releases.Keys), "a folder per release of a FHIR core package, lower case");

        foreach (var release in s_releases.Keys)
        {
            var files = Files(release).ToList();
            Assert.That(VendorSearchForms.ResourceTypes(release), Is.EquivalentTo(files.Select(Path.GetFileNameWithoutExtension)), release);
            foreach (var file in files)
            {
                var text = File.ReadAllText(file).ReplaceLineEndings("\n");
                Assert.That(ViewerJson.Write(ViewerJson.Read<SearchFormDefinition>(text)), Is.EqualTo(text), file);
            }
        }
    }

    [Test]
    public void EveryResourceTypeWithSearchParameters_HasAForm_AndEveryFormIsOfAResourceType([ValueSource(nameof(Releases))] string release)
    {
        var files = Files(release).Select(Path.GetFileNameWithoutExtension).ToHashSet(StringComparer.Ordinal);
        if (files.Count == 0)
        {
            Assert.Ignore($"No vendor search forms of {release} yet: run \"Create the missing files\" in the Files mode and commit them.");
        }

        var types = SearchableTypes(release);
        Assert.Multiple(() =>
        {
            Assert.That(types.Where(type => !files.Contains(type)), Is.Empty, "without a form");
            Assert.That(files.Where(file => !types.Contains(file!)), Is.Empty, "not a resource type with search parameters");
        });
    }

    // The list the completeness is checked against is read from Firely; it must not be empty before the files are there
    [Test]
    public void TheSearchableTypes_OfEveryRelease_AreReadFromFirely([ValueSource(nameof(Releases))] string release)
    {
        var types = SearchableTypes(release);
        Assert.That(types, Does.Contain("Patient").And.Contain("Observation").And.Not.Contain("Resource"));
    }

    private static IEnumerable<string> Releases()
    {
        return s_releases.Keys;
    }
}
