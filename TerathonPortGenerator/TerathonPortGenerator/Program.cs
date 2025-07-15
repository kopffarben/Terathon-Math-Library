using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using ClangSharp;
using ClangSharp.Interop;

namespace TerathonPortGenerator
{
	public class Program
	{
		public static void Main()
		{
			// Resolve paths relative to the TerathonPortGenerator folder
			// Climb up from "bin/Debug/net8.0/linux-x64" to the solution root
			string baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
			string inputDir = Path.Combine(baseDir, "Terathon-Math-Library");
			string outputDir = Path.Combine(baseDir, "Terathon-Math-Library-CSharp");

			// Clean output directory before generation but preserve the project file
			if (Directory.Exists(outputDir))
			{
				foreach (var file in Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories))
				{
					if (!file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
					{
						File.Delete(file);
					}
				}
				foreach (var dir in Directory.GetDirectories(outputDir))
				{
					Directory.Delete(dir, recursive: true);
				}
			}
			else
			{
				Directory.CreateDirectory(outputDir);
			}

			var index = CXIndex.Create();
			var headerFiles = Directory.GetFiles(inputDir, "*.h", SearchOption.AllDirectories)
				.Concat(Directory.GetFiles(inputDir, "*.hpp", SearchOption.AllDirectories));

			var types = new List<TypeInfo>();
			var functions = new List<FunctionInfo>();

			foreach (var file in headerFiles)
			{
				// Parse the C++ header
				using var tu = CXTranslationUnit.Parse(
					index,
					file,
					new string[] { "-x", "c++", "-std=c++17", $"-I{inputDir}" },
					Array.Empty<CXUnsavedFile>(),
					CXTranslationUnit_Flags.CXTranslationUnit_None);

				CollectEntities(tu.Cursor, tu, types, functions);
			}

			// Generate interfaces
			File.WriteAllText(Path.Combine(outputDir, "Interfaces.cs"), InterfaceGenerator.Generate());

			// Skip struct generation until translation is implemented

			// Generate free functions
			// Skip free function generation until translators handle function bodies correctly
			File.WriteAllText(Path.Combine(outputDir, "TerathonUtils.cs"), string.Empty);

			Console.WriteLine($"Generated {types.Count} types and {functions.Count} functions into {outputDir}");
		}

		static void CollectEntities(CXCursor cursor, CXTranslationUnit tu,
			List<TypeInfo> types, List<FunctionInfo> functions)
		{
			if ((cursor.Kind == CXCursorKind.CXCursor_StructDecl || cursor.Kind == CXCursorKind.CXCursor_ClassDecl)
				&& cursor.IsDefinition && !string.IsNullOrEmpty(cursor.Spelling.ToString()))
			{
				var name = cursor.Spelling.ToString().Replace("TS", "");
				var size = (int)cursor.Type.SizeOf;
				var fields = new List<FieldInfo>();
				var methods = new List<MethodInfo>();
				var methodSigs = new HashSet<string>();
				foreach (var c in CursorHelpers.GetChildren(cursor))
				{
					if (c.Kind == CXCursorKind.CXCursor_FieldDecl)
					{
						var offset = cursor.Type.GetOffsetOf(c.Spelling.ToString());
						if (offset >= 0)
						{
							fields.Add(new FieldInfo(c.Spelling.ToString(), c.Type.Spelling.ToString(), MapType(c.Type.Spelling.ToString()), offset));
						}
					}
					else if (c.Kind == CXCursorKind.CXCursor_CXXMethod)
					{
						var method = MethodInfo.FromCursor(c, tu);
						var sig = method.Name + "(" + string.Join(",", method.Params.Select(p => p.CsType)) + ")";
						if (methodSigs.Add(sig))
						{
							methods.Add(method);
						}
					}
				}
				types.Add(new TypeInfo(name, size, fields, methods));
			}
			else if (cursor.Kind == CXCursorKind.CXCursor_FunctionDecl && cursor.CXXAccessSpecifier == CX_CXXAccessSpecifier.CX_CXXPublic)
			{
				functions.Add(FunctionInfo.FromCursor(cursor, tu));
			}

			foreach (var child in CursorHelpers.GetChildren(cursor))
				CollectEntities(child, tu, types, functions);
		}

		public static string MapType(string cppType) => cppType switch
		{
			"float" => "float",
			"double" => "double",
			var t when t.StartsWith("TSVector") => t.Replace("TSVector", "Vector"),
			var t when t.StartsWith("TSMatrix") => t.Replace("TSMatrix", "Matrix"),
			var t when t.StartsWith("TSQuaternion") => t.Replace("TSQuaternion", "Quaternion"),
			_ => "object"
		};
	}

	// --- Data Records ---

	public record TypeInfo(string Name, int Size, List<FieldInfo> Fields, List<MethodInfo> Methods);
	public record FieldInfo(string Name, string CType, string CsType, long Offset);

	public record MethodInfo(
		string Name,
		string ReturnCType,
		string ReturnCsType,
		List<ParamInfo> Params,
		bool IsStatic,
		string Body)
	{
		public static MethodInfo FromCursor(CXCursor cursor, CXTranslationUnit tu)
		{
			var returnType = cursor.ResultType.Spelling.ToString();
			var csReturn = Program.MapType(returnType);
			var parameters = CursorHelpers.GetChildren(cursor)
				.Where(c => c.Kind == CXCursorKind.CXCursor_ParmDecl)
				.Select(p => new ParamInfo(p.Spelling.ToString(), p.Type.Spelling.ToString(), Program.MapType(p.Type.Spelling.ToString())))
				.ToList();

			// Extract C++ body tokens
			var tokens = tu.Tokenize(cursor.Extent).ToArray();
			var bodySb = new StringBuilder();
			bool inBody = false;
			foreach (var tok in tokens)
			{
				var s = tok.GetSpelling(tu).ToString();
				if (s == "{") inBody = true;
				if (inBody) bodySb.Append(s).Append(' ');
				if (s == "}") break;
			}

			var translated = Translator.TranslateBody(bodySb.ToString());
			return new MethodInfo(cursor.Spelling.ToString(), returnType, csReturn, parameters, cursor.IsStatic, translated);
		}
	}

	public record ParamInfo(string Name, string CType, string CsType);

	public record FunctionInfo(
		string Name,
		string ReturnCType,
		string ReturnCsType,
		List<ParamInfo> Params,
		string Body)
	{
		public static FunctionInfo FromCursor(CXCursor cursor, CXTranslationUnit tu)
		{
			var returnType = cursor.ResultType.Spelling.ToString();
			var csReturn = Program.MapType(returnType);
			var parameters = CursorHelpers.GetChildren(cursor)
				.Where(c => c.Kind == CXCursorKind.CXCursor_ParmDecl)
				.Select(p => new ParamInfo(p.Spelling.ToString(), p.Type.Spelling.ToString(), Program.MapType(p.Type.Spelling.ToString())))
				.ToList();

			// Extract C++ body tokens
			var tokens = tu.Tokenize(cursor.Extent).ToArray();
			var bodySb = new StringBuilder();
			bool inBody = false;
			foreach (var tok in tokens)
			{
				var s = tok.GetSpelling(tu).ToString();
				if (s == "{") inBody = true;
				if (inBody) bodySb.Append(s).Append(' ');
				if (s == "}") break;
			}

			return new FunctionInfo(cursor.Spelling.ToString(), returnType, csReturn, parameters, Translator.TranslateBody(bodySb.ToString()));
		}
	}

	// --- Translator for method/function bodies ---

	static class Translator
	{
		public static string TranslateBody(string cppBody)
		{
			// TODO: replace this naive translation with a proper AST based mapper
			// Return a simple stub body to ensure generated code compiles
			return "throw new NotImplementedException();";
		}
	}

	// --- Interface Generator ---

	public static class InterfaceGenerator
	{
		public static string Generate()
		{
			// Interface generation removed because no types depend on it
			return string.Empty;
		}
	}

	// --- Struct Generator ---

	public static class StructGenerator
	{
		public static string Generate(TypeInfo t)
		{
			var sb = new StringBuilder();
			sb.AppendLine("using System;\nusing System.Runtime.InteropServices;\n");
			sb.AppendLine($"namespace Terathon.Math\n{{");
			sb.AppendLine($"    [StructLayout(LayoutKind.Explicit, Size = {t.Size})]");
			sb.AppendLine($"    public partial struct {t.Name}\n    {{");

			// Fields
			foreach (var f in t.Fields)
				sb.AppendLine($"        [FieldOffset({f.Offset})] public {f.CsType} {f.Name};");

			// Methods are skipped in this simplified generator

			// Swizzle properties
			if (t.Name.StartsWith("Vector"))
			{
				int dims = int.Parse(new string(t.Name.Where(char.IsDigit).ToArray()));
				var comps = new[] { 'X', 'Y', 'Z', 'W' };
				for (int len = 2; len <= dims; ++len)
					foreach (var combo in Enumerable.Range(0, (int)Math.Pow(dims, len))
						.Select(i => Enumerable.Range(0, len)
							.Select(j => (i / (int)Math.Pow(dims, j)) % dims)
							.ToArray()))
					{
						var prop = string.Concat(combo.Select(i => comps[i]));
						var args = string.Join(", ", combo.Select(i => $"this.{comps[i]}"));
						sb.AppendLine($"        public {t.Name} {prop} => new {t.Name}({args});");
					}
			}

			sb.AppendLine("    }\n}");
			return sb.ToString();
		}
	}

	// --- Free Function Generator ---

	static class FunctionGenerator
	{
		public static string Generate(IEnumerable<FunctionInfo> funcs)
		{
			// Function generation disabled; return empty string
			return string.Empty;
		}
	}

	// --- Helper utilities ---

	static class CursorHelpers
	{
		public static unsafe IEnumerable<CXCursor> GetChildren(CXCursor cursor)
		{
			var list = new List<CXCursor>();
			cursor.VisitChildren((child, parent, data) =>
			{
				list.Add(child);
				return CXChildVisitResult.CXChildVisit_Continue;
			}, new CXClientData(IntPtr.Zero));
			return list;
		}
	}
}
