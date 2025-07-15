# TerathonPortGenerator Documentation

This document provides an exhaustive overview of the **TerathonPortGenerator**, a C#-based Source Generator that automatically ports the [Terathon Math Library](https://github.com/EricLengyel/Terathon-Math-Library) (C++ codebase) into idiomatic C# code. It covers architecture, components, installation, usage, extension points, and troubleshooting.

---

## 1. Overview

The TerathonPortGenerator:

* **Parses** all C++ headers and sources using **ClangSharp**
* **Discovers** structs, classes, methods, and free functions
* **Extracts** field layouts, method bodies, and token streams
* **Translates** C++ syntax into C#-equivalent constructs
* **Emits** C# files featuring:

  * `[StructLayout(LayoutKind.Explicit)]` + `[FieldOffset]` for memory overlays and unions
  * `static abstract` interfaces (`IVector<T>`) to capture template-based generics
  * Full **Swizzle** generation for 2D–4D vectors
  * Method bodies ported via a **Translator** module
  * Free functions collected into a `TerathonFunctions` static class

## 2. Architecture & Components

```
+-------------------------------+
|        Program.Main()         |
|  - index C++ files            |
|  - CollectEntities            |
|  - Write Interfaces.cs        |
|  - Write <Type>.cs for each   |
|  - Write TerathonUtils.cs     |
+-------------------------------+
            /           \
           /             \
+----------------+   +----------------+
|   CollectEntities |   |   Translators   |
|  - Struct/Class   |   |  TranslateBody  |
|  - FieldDecl      |   +----------------+
|  - CXXMethod      |
|  - FunctionDecl   |
+-------------------+
           |
           v
+-------------------------------+
|        TypeInfo, FieldInfo,   |
|        MethodInfo, FunctionInfo|
+-------------------------------+
           |
           v
+-------------------------------+
| Generator Modules             |
| - InterfaceGenerator          |
| - StructGenerator             |
| - FunctionGenerator           |
+-------------------------------+
           |
           v
+-------------------------------+
|       Output C# Files         |
+-------------------------------+
```

### 2.1 ClangSharp Integration

* **`CXIndex.Create()`**: Initializes the Clang index
* **`CXTranslationUnit.Parse(...)`**: Parses each `.h`/`.hpp` file with `-std=c++17`
* **`tu.TranslationUnitCursor`**: Root AST cursor
* **`CursorVisitor`** logic in `CollectEntities` walks the AST

### 2.2 Data Records

* **`TypeInfo`**: `{ Name, Size, List<FieldInfo>, List<MethodInfo> }`
* **`FieldInfo`**: `{ Name, CType, CsType, Offset }`
* **`MethodInfo`**: `{ Name, ReturnCType, ReturnCsType, Params, IsStatic, Body }`
* **`FunctionInfo`**: `{ Name, ReturnCType, ReturnCsType, Params, Body }`

### 2.3 Translators

* **`Translator.TranslateBody(string cppBody)`**: Performs naive token‐based `string.Replace` mappings for:

  * Pointer access: `->` → `.`
  * Null pointers: `nullptr` → `null`
  * Boolean constants: `TRUE`/`FALSE` → `true`/`false`
  * Casts: `float(...)` → `(float)...`, `double(...)` → `(double)...`
  * Cleans up trailing `; }` artifacts

*Define additional rules here to handle more complex C++ features (e.g. `constexpr`, `template` parameters).*

### 2.4 Generator Modules

#### 2.4.1 InterfaceGenerator

* Emits a **single** `Interfaces.cs` containing:

  ```csharp
  public interface IVector<T>
      where T : struct, IVector<T>
  {
      static abstract T Zero { get; }
      T Add(T other);
      T Subtract(T other);
      T Multiply(float scalar);
      float Dot(T other);
      static abstract T operator +(T a, T b);
      static abstract T operator -(T a, T b);
      static abstract T operator *(T a, float b);
  }
  ```

#### 2.4.2 StructGenerator

For each `TypeInfo`:

* Generates:

  * `using System.Runtime.InteropServices;`
  * `namespace Terathon.Math`
  * `[StructLayout(LayoutKind.Explicit, Size = <Size>)]`
  * `public partial struct <Name> : IVector<<Name>>`
* Emits **fields** with `[FieldOffset(offset)]`
* Emits **methods** including translated bodies:

  ```csharp
  public <ReturnType> MethodName(<params>)
  {
      <translated body>
  }
  ```
* **Swizzle properties** (2D–4D):

  * Generates all combinations of length 2..N over `{X,Y,Z,W}`
  * Emits e.g. `public Vector3 XY => new Vector3(this.X, this.Y);`

#### 2.4.3 FunctionGenerator

* Wraps all free functions (`FunctionInfo`) into a static class `TerathonFunctions`
* Each function is emitted as:

  ```csharp
  public static <ReturnType> FunctionName(<params>)
  {
      <translated body>
  }
  ```

## 3. Installation & Usage

1. **Clone the Generator**

   ```bash
   git clone <this-repo-url>
   cd TerathonPortGenerator
   ```

2. **Add ClangSharp**

   ```bash
   dotnet add package ClangSharp --version x.y.z
   ```

3. **Build**

   ```bash
   dotnet build
   ```

4. **Run**

   ```bash
   dotnet run --project TerathonPortGenerator.csproj <path/to/Terathon-Repo> <output-directory>
   ```

5. **Review Output**

   * `Interfaces.cs`
   * `<Type>.cs` per struct/class
   * `TerathonUtils.cs`

6. **Integrate** into your C# solution

   * Add the generated `.cs` files
   * Reference `System.Runtime.Intrinsics` or `System.Numerics` as needed

## 4. Extension Points

* **Enhance `Translator.TranslateBody`**:

  * Add regex rules for template syntax, inline functions, default parameters
  * Map `constexpr` → `const`, `enum class` → `enum`

* **Support Additional C++ Features**:

  * **Templates**: Manual pattern-based instantiation or Roslyn-based code generation
  * **CRTP**: Identify and map base-method chaining
  * **Macro Constants**: Parse `#define` via a preprocessor step

* **Performance Intrinsics**:

  * Wrap `[UseIntrinsics]` attributes to generate `if (Sse.IsSupported) ... else ...`

* **Unit Testing**:

  * Generate NUnit/xUnit test stubs comparing results with native C++ for validation

## 5. Troubleshooting

* **Missing AST nodes**: Ensure include paths (`-I...`) cover all dependencies
* **Translation gaps**: Inspect generated `.cs` stubs for `throw new NotImplementedException()` occurrences
* **Swizzle explosion**: Filter or limit swizzle lengths in `StructGenerator` if compile times explode

---

*End of AGENTS.md*: This file consolidates generator design, usage, and extension strategies to ensure a **complete 1:1 port** of Terathon Math Library from C++ to C#. Feel free to contribute improvements, translation rules, and additional feature support.
