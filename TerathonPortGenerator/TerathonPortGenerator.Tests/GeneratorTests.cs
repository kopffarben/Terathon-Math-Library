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

        [Fact]
        public void StructGeneratorGeneratesOffsetsAndSwizzles()
        {
                var typeInfo = new TypeInfo(
                        "Vector3D",
                        12,
                        new List<FieldInfo>{
                                new FieldInfo("X", "float", "float", 0),
                                new FieldInfo("Y", "float", "float", 4),
                                new FieldInfo("Z", "float", "float", 8)
                        },
                        new List<MethodInfo>());

                var code = StructGenerator.Generate(typeInfo);

                Assert.Contains("StructLayout(LayoutKind.Explicit", code);
                Assert.Contains("[FieldOffset(0)] public float X;", code);
                Assert.Contains("[FieldOffset(4)] public float Y;", code);
                Assert.Contains("public Vector3D XY", code);
        }
}
