namespace Chickensoft.UMLGenerator.Models;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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

	public abstract void GenerateHierarchy(IDictionary<string, BaseHierarchy> nodeHierarchyList);

	private AttributeSyntax? GetClassDiagramAttribute()
	{
		var attributeName = nameof(ClassDiagramAttribute).Replace("Attribute", "");
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
	
	internal string GetDiagram(int depth, bool useVSCodePaths)
	{
		var classDefinition = GetClassDefinition(depth, useVSCodePaths, out var properties);
		
		if (DictOfChildren.Count == 0)
			return classDefinition;
		
		var newFilePath = useVSCodePaths ? GetVSCodePath(FullFilePath) : GetPathWithDepth(FilePath, depth);
		
		var childrenDefinitions = string.Join("\n\t",
			DictOfChildren.Values.Select(x =>
				x.GetDiagram(depth, useVSCodePaths)
			)
		);

		var childrenRelationships = string.Join("\n\t",
			DictOfChildren.Values.Select(x =>
			{
				var memberName = properties.FirstOrDefault(prop => prop.Value == x.Name || prop.Value == $"I{x.Name}" ).Key ?? x.Name;
				return $"{Name}::{memberName} {(x.DictOfChildren.Count == 0 ? string.Empty : "-")}--> {x.Name}";
			})
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
			  	{{classDefinition}}
			  	{{childrenDefinitions}}
			  	{{childrenRelationships}}
			  }

			  """;
	}

	private string GetClassDefinition(int depth, bool useVSCodePaths, out IDictionary<string,string> properties)
	{
		properties = ImmutableDictionary<string, string>.Empty;
		
		var hasScript = !string.IsNullOrEmpty(ScriptPath);
		var filePath = hasScript ? ScriptPath : FilePath;
		var fullFilePath = hasScript ? FullScriptPath : FullFilePath;
		
		var interfaceMethodsString = string.Empty;
		var interfacePropertiesString = string.Empty;

		var newScriptPath = useVSCodePaths ? GetVSCodePath(fullFilePath) : GetPathWithDepth(filePath, depth);
		
		if(InterfaceSyntax != null)
		{
			var classProperties =
				(from interfaceMember in InterfaceSyntax.Members
				from classMember in ClassSyntax.Members
				where classMember is PropertyDeclarationSyntax classProperty &&
				      interfaceMember is PropertyDeclarationSyntax interfaceProperty &&
				      classProperty.Identifier.Value == interfaceProperty.Identifier.Value
				select classMember as PropertyDeclarationSyntax).ToList();

			properties = classProperties.ToDictionary(syntax => syntax.Identifier.Value!.ToString(), syntax => syntax.Type.ToString());

			interfacePropertiesString = string.Join("\n\t",
				classProperties.Select(x =>
					$"+ [[{newScriptPath}:{x?.GetLineNumber()} {x?.Identifier.Value}]]"
				)
			);

			if (!string.IsNullOrWhiteSpace(interfacePropertiesString))
				interfacePropertiesString = "\n--\n" + interfacePropertiesString;
			
			var classMethods = 
				from interfaceMember in InterfaceSyntax.Members
				from classMember in ClassSyntax.Members
				where classMember is MethodDeclarationSyntax classMethod &&
				      interfaceMember is MethodDeclarationSyntax interfaceMethod &&
				      classMethod.Identifier.Value == interfaceMethod.Identifier.Value
				select classMember as MethodDeclarationSyntax;

			interfaceMethodsString = string.Join("\n\t",
				classMethods.Select(x =>
					$"[[{newScriptPath}:{x?.GetLineNumber()} {x?.Identifier.Value}()]]"
				)
			);

			if (!string.IsNullOrWhiteSpace(interfaceMethodsString))
				interfaceMethodsString = "\n--\n" + interfaceMethodsString;
		}
		
		var fileType = hasScript ? "Script" : "Scene";

		return 
		$$"""

		class {{Name}} {
			[[{{newScriptPath}} {{fileType}}File]]{{interfacePropertiesString}}{{interfaceMethodsString}}
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
		return $"vscode://file/{path}";
	}
}