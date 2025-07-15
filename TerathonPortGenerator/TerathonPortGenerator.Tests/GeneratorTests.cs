using System.Collections.Generic;
using TerathonPortGenerator;
using Xunit;

namespace TerathonPortGenerator.Tests;

public class GeneratorTests
{
	[Fact]
	public void StructGeneratorDoesNotImplementIVector()
	{
		var typeInfo = new TypeInfo(
			"Vector2D",
			8,
			new List<FieldInfo>{
				new FieldInfo("X", "float", "float", 0),
				new FieldInfo("Y", "float", "float", 4)
			},
			new List<MethodInfo>());

		var code = StructGenerator.Generate(typeInfo);
		Assert.DoesNotContain("IVector", code);
	}

	[Fact]
	public void InterfaceGeneratorReturnsEmpty()
	{
		var code = InterfaceGenerator.Generate();
		Assert.True(string.IsNullOrWhiteSpace(code) || !code.Contains("IVector"));
	}
}
