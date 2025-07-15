using System;
using System.IO;
using Xunit;

namespace TerathonPortGenerator.Tests;

public class GenerationTests
{
    [Fact(Skip="libclang not available in test environment")]
    public void GeneratorProducesVector2D()
    {
        TerathonPortGenerator.Program.Main();
        string baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string output = Path.Combine(baseDir, "Terathon-Math-Library-CSharp", "Vector2D.cs");
        Assert.True(File.Exists(output));
    }
}
