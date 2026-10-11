using System.Xml.Linq;

namespace Tcc.Domain.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void Domain_has_no_external_dependencies()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Tcc.slnx")))
            root = root.Parent;
        Assert.NotNull(root);
        var project = XDocument.Load(Path.Combine(root.FullName, "src/Tcc.Domain/Tcc.Domain.csproj"));
        Assert.Empty(project.Descendants("ProjectReference"));
        Assert.Empty(project.Descendants("PackageReference"));
        Assert.Empty(project.Descendants("FrameworkReference"));
    }
}
