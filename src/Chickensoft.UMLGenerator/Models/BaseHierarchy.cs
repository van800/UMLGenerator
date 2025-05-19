namespace Chickensoft.UMLGenerator.Models;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public abstract class BaseHierarchy(GenerationData data)
{
	public IEnumerable<GeneratorSyntaxContext> LocalSyntaxContexts => 
		data.SyntaxContexts
			.Where(x =>
			{
				var sourceFileName = Path.GetFileNameWithoutExtension(x.Node.SyntaxTree.FilePath);
				return sourceFileName == Name;
			});
	
	public ClassDeclarationSyntax? ClassSyntax => LocalSyntaxContexts.Select(x => x.Node)
		.FirstOrDefault(x => x is ClassDeclarationSyntax) as ClassDeclarationSyntax;
	public InterfaceDeclarationSyntax? InterfaceSyntax => LocalSyntaxContexts.Select(x => x.Node)
		.FirstOrDefault(x => x is InterfaceDeclarationSyntax ctx && ctx.Identifier.Value?.ToString() == $"I{Name}") as InterfaceDeclarationSyntax;
	
	public string FilePath => FullFilePath.Replace($"{data.ProjectDir}", "");
	public abstract string FullFilePath { get; }
	public string ScriptPath => FullScriptPath.Replace($"{data.ProjectDir}", "");
	public abstract string FullScriptPath { get; }
	public string Name => Path.GetFileNameWithoutExtension(FilePath);
	
	private Dictionary<string, BaseHierarchy> _dictOfChildren = [];
	private Dictionary<string, BaseHierarchy> _dictOfParents = [];
	public virtual List<GeneratorSyntaxContext> ContextList { get; private set; }
	
	public IReadOnlyDictionary<string, BaseHierarchy> DictOfChildren => _dictOfChildren;
	public IReadOnlyDictionary<string, BaseHierarchy> DictOfParents => _dictOfParents;

	public abstract void GenerateHierarchy(Dictionary<string, BaseHierarchy> nodeHierarchyList);

	public string GetDiagram(int depth, bool useVSCodePaths)
	{
		var classDefinition = GetClassDefinition(depth, useVSCodePaths);
		var packageDefinition = GetPackageDefinition(depth, useVSCodePaths);

		return classDefinition + packageDefinition;
	}

	private AttributeSyntax? GetClassDiagramAttribute()
	{
		var attributeName = nameof(ClassDiagramAttribute).TrimEnd("Attribute").ToString();
		var classDiagramAttribute = LocalSyntaxContexts
			.Select(x => (x.Node as TypeDeclarationSyntax)?.AttributeLists.SelectMany(x => x.Attributes))
			.SelectMany(x => x)
			.FirstOrDefault(x => x.Name.ToString() == attributeName);
		
		return classDiagramAttribute;
	}

	public bool ShouldUseVSCode()
	{
		var attribute = GetClassDiagramAttribute();
		return attribute?.ArgumentList?.Arguments.Any(arg =>
			arg.NameEquals is NameEqualsSyntax nameEquals &&
			nameEquals.Name.ToString() == nameof(ClassDiagramAttribute.UseVSCodePaths) &&
			arg.Expression is LiteralExpressionSyntax { Token.ValueText: "true" }) ?? false;
	}
	
	public bool HasClassDiagramAttribute() => GetClassDiagramAttribute() != null;

	internal void AddChild(BaseHierarchy node)
	{
		if(!_dictOfChildren.ContainsKey(node.Name))
			_dictOfChildren.Add(node.Name, node);
		else
			Console.WriteLine($"Found duplicate {node.Name} in {Name}");
	}

	internal void AddParent(BaseHierarchy node)
	{
		if(!_dictOfParents.ContainsKey(node.Name))
			_dictOfParents.Add(node.Name, node);
		else
			Console.WriteLine($"Found duplicate {node.Name} in {Name}");
	}

	public void AddContextList(List<GeneratorSyntaxContext> list)
	{
		ContextList = list;
	}

	public List<GeneratorSyntaxContext> GetPropertyDeclarations()
	{
		var listOfChildContexts = new List<GeneratorSyntaxContext>();

		if (ClassSyntax == null)
			return listOfChildContexts;
			
		var properties = ClassSyntax
			.Members.OfType<PropertyDeclarationSyntax>()
			.Where(x => 
				!x.AttributeLists.SelectMany(x => x.Attributes)
					.Any(x => x.Name.ToString() == "Dependency"));
	
		foreach (var property in properties)
		{
			var type = property.Type.ToString();
			var childContexts = data.SyntaxContexts
				.Where(x =>
				{
					var typeSyntax = x.Node as TypeDeclarationSyntax;
					var sourceFileName = typeSyntax?.Identifier.ValueText;
					return sourceFileName == type && !DictOfChildren.ContainsKey(type);
				});
			
			listOfChildContexts.AddRange(childContexts);
		}

		return listOfChildContexts;
	}

	internal string GetClassDefinition(int depth, bool useVSCodePaths)
	{
		if (string.IsNullOrEmpty(ScriptPath))
			return string.Empty;
		var interfaceMembersString = string.Empty;

		var newScriptPath = useVSCodePaths ? GetVSCodePath(FullScriptPath) : GetPathWithDepth(ScriptPath, depth);
		
		if(InterfaceSyntax != null)
		{
			var classMethods = 
				from interfaceMember in InterfaceSyntax.Members
				from classMember in ClassSyntax.Members
				where classMember is MethodDeclarationSyntax classMethod &&
				      interfaceMember is MethodDeclarationSyntax interfaceMethod &&
				      classMethod.Identifier.Value == interfaceMethod.Identifier.Value
				select classMember as MethodDeclarationSyntax;

			interfaceMembersString = "\n\t" + string.Join("\n\t",
				classMethods.Select(x =>
					$"[[{newScriptPath}:{x?.GetLineNumber()} {x?.Identifier.Value}()]]"
				)
			);
		}

		return 
		$$"""

		class {{Name}} {
			[[{{newScriptPath}} ScriptFile]]{{interfaceMembersString}}
		}

		""";
	}
	
	internal string GetPackageDefinition(int depth, bool useVSCodePaths)
	{
		if (DictOfChildren.Count == 0)
			return string.Empty;
		
		var newFilePath = useVSCodePaths ? GetVSCodePath(FullFilePath) : GetPathWithDepth(FilePath, depth);
		
		var childrenDefinitions = string.Join("\n\t",
			DictOfChildren.Values.Select(x =>
				x.GetDiagram(depth, useVSCodePaths)
			)
		);

		var childrenRelationships = string.Join("\n\t",
			DictOfChildren.Values.Select(x =>
				$"{Name} --> {x.Name}" //Todo: Add comments on arrows
			)
		);

		var packageType = this switch
		{
			ClassHierarchy => "Class",
			NodeHierarchy => "Scene",
			_ => throw new NotImplementedException()
		};
			
		return 
		$$"""

		package {{Name}}-{{packageType}} [[{{newFilePath}}]] {
			{{childrenDefinitions}}
			{{childrenRelationships}}
		}

		""";
	}

	public string GetPathWithDepth(string path, int depth)
	{
		var depthString = string.Join("", Enumerable.Repeat("../", depth));
		return depthString + path;
	}

	public string GetVSCodePath(string path)
	{
		return $"vscode://file{path}";
	}
}