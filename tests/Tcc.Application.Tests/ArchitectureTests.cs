using System.Xml.Linq;

namespace Tcc.Application.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void Application_only_references_domain()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Tcc.slnx")))
            root = root.Parent;
        Assert.NotNull(root);
        var project = XDocument.Load(Path.Combine(root.FullName, "src/Tcc.Application/Tcc.Application.csproj"));
        var reference = Assert.Single(project.Descendants("ProjectReference"));
        Assert.EndsWith("Tcc.Domain.csproj", reference.Attribute("Include")?.Value);
        Assert.Empty(project.Descendants("PackageReference"));
        Assert.Empty(project.Descendants("FrameworkReference"));
    }
}
