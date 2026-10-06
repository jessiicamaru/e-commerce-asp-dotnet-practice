using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Ecommerce.Inventory.Tests;

/// <summary>
/// MassTransit stays on exactly 8.3.6 (specs/155, #353). On 8.5.11 a RabbitMQ outage under load left receive endpoints
/// stopped for good - Inventory's ReserveInventory in two runs, three of Order's queues in a third - and every new order
/// stayed Submitted until the service was restarted; 0 of 12 runs on 8.3.6. Nothing in CI can see that: only a broker
/// outage under load does (<c>loadtest/fault.sh broker</c>), so a green build says nothing about another version - not
/// even a patch, since the regression lies somewhere after 8.3.6. Dependabot proposes no MassTransit version, and this
/// test stops a bump by hand, which Dependabot cannot.
/// </summary>
public partial class MassTransitVersionTests
{
    private const string Pinned = "8.3.6";

    [GeneratedRegex(@"^MassTransit(\..+)?$")]
    private static partial Regex MassTransitPackage();

    private static DirectoryInfo SolutionRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Ecommerce.slnx")))
            {
                return dir;
            }
        }

        throw new InvalidOperationException("Ecommerce.slnx not found above " + AppContext.BaseDirectory);
    }

    [Fact]
    public void Every_project_references_MassTransit_8_3_6()
    {
        var root = SolutionRoot();
        var references = root.EnumerateFiles("*.csproj", SearchOption.AllDirectories)
            .Where(f => !f.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.FullName.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .SelectMany(f => XDocument.Load(f.FullName).Descendants("PackageReference")
                .Select(r => (Project: f.Name, Package: (string?)r.Attribute("Include") ?? "", Version: (string?)r.Attribute("Version") ?? "")))
            .Where(r => MassTransitPackage().IsMatch(r.Package))
            .ToList();

        Assert.NotEmpty(references);
        var elsewhere = references.Where(r => r.Version != Pinned)
            .Select(r => $"{r.Project}: {r.Package} {r.Version}")
            .ToList();
        Assert.True(elsewhere.Count == 0,
            "MassTransit is pinned to 8.3.6 until #353 is understood (specs/155) - run loadtest/fault.sh broker several " +
            "times before moving it:\n" + string.Join("\n", elsewhere));
    }

    [Fact]
    public void The_running_MassTransit_is_8_3_6()
    {
        // What the build actually resolved, which a transitive reference could change without any csproj saying so.
        var version = typeof(MassTransit.IBus).Assembly.GetName().Version!;
        Assert.Equal((8, 3, 6), (version.Major, version.Minor, version.Build));
    }
}
