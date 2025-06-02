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
	public virtual List<GeneratorSyntaxContext> ContextList { get; } = [];
	
	public TypeDeclarationSyntax? TypeSyntax => ContextList.Select(x => x.Node)
		.FirstOrDefault(x => x is ClassDeclarationSyntax or RecordDeclarationSyntax or StructDeclarationSyntax) as TypeDeclarationSyntax;
	public InterfaceDeclarationSyntax? InterfaceSyntax => ContextList.Select(x => x.Node)
		.FirstOrDefault(x => x is InterfaceDeclarationSyntax ctx && ctx.Identifier.ValueText == $"I{Name}") as InterfaceDeclarationSyntax;
	
	public string FilePath => FullFilePath.Replace($"{data.ProjectDir}", "");
	public abstract string FullFilePath { get; }
	public string ScriptPath => FullScriptPath.Replace($"{data.ProjectDir}", "");
	public abstract string FullScriptPath { get; }
	public string Name => Path.GetFileNameWithoutExtension(FilePath);
	
	private Dictionary<string, BaseHierarchy> _dictOfChildren = [];
	private Dictionary<string, BaseHierarchy> _dictOfParents = [];
	
	public IReadOnlyDictionary<string, BaseHierarchy> DictOfChildren => _dictOfChildren;
	public IReadOnlyDictionary<string, BaseHierarchy> DictOfParents => _dictOfParents;

	public virtual void GenerateHierarchy(IDictionary<string, BaseHierarchy> nodeHierarchyList)
	{
		var propertyDeclarations = GetSyntaxContextForPropertyDeclarations();
		foreach (var ctx in propertyDeclarations)
		{
			var typeName = Path.GetFileNameWithoutExtension(ctx.SemanticModel.SyntaxTree.FilePath);
			if (!nodeHierarchyList.TryGetValue(typeName, out var childNodeHierarchy)) 
				continue;
			
			AddChild(childNodeHierarchy);
			childNodeHierarchy.AddParent(this);
		}
	}

	private AttributeSyntax? GetClassDiagramAttribute()
	{
		var attributeName = nameof(ClassDiagramAttribute).Replace("Attribute", "");
		var classDiagramAttribute = ContextList
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
		ContextList.AddRange(list);
	}

	/// <summary>
	/// Returns all SyntaxContexts for properties which don't have a Dependency attribute
	/// </summary>
	/// <returns></returns>
	private IList<GeneratorSyntaxContext> GetSyntaxContextForPropertyDeclarations()
	{
		if (TypeSyntax == null)
			return ImmutableList<GeneratorSyntaxContext>.Empty;
		
		var listOfChildContexts = new List<GeneratorSyntaxContext>();
			
		var properties = TypeSyntax
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

	/// <summary>
	/// This will return type properties which exist in the interface
	/// </summary>
	/// <returns></returns>
	private IEnumerable<PropertyDeclarationSyntax> GetInterfacePropertyDeclarations()
	{
		if (InterfaceSyntax == null) return [];
		return from interfaceMember in InterfaceSyntax.Members
			from typeMember in TypeSyntax.Members
			where typeMember is PropertyDeclarationSyntax property &&
			      interfaceMember is PropertyDeclarationSyntax interfaceProperty &&
			      property.Identifier.Value == interfaceProperty.Identifier.Value
			orderby (typeMember as PropertyDeclarationSyntax).Identifier.ValueText
			select typeMember as PropertyDeclarationSyntax;
	}
	
	/// <summary>
	/// This will return type methods which exist in the interface
	/// </summary>
	/// <returns></returns>
	private IEnumerable<MethodDeclarationSyntax> GetInterfaceMethodDeclarations()
	{
		if (InterfaceSyntax == null) return [];
		return from interfaceMember in InterfaceSyntax!.Members
			from typeMember in TypeSyntax.Members
			where typeMember is MethodDeclarationSyntax typeMethod &&
			      interfaceMember is MethodDeclarationSyntax interfaceMethod &&
			      typeMethod.Identifier.Value == interfaceMethod.Identifier.Value
			orderby (typeMember as MethodDeclarationSyntax).Identifier.ValueText
			select typeMember as MethodDeclarationSyntax;
	}
	
	internal string GetDiagram(int depth, bool useVSCodePaths)
	{
		var typeDefinition = GetTypeDefinition(depth, useVSCodePaths, out var properties);
		
		var childrenToDraw = DictOfChildren.Values
			.Where(x => x.DictOfChildren.Count != 0 ||
			            x.GetInterfacePropertyDeclarations().Any() ||
			            x.GetInterfaceMethodDeclarations().Any() ||
						!properties.Values.Contains(x)
			).ToList();
		
		if (childrenToDraw.Count == 0)
			return typeDefinition;
		
		var newFilePath = useVSCodePaths ? GetVSCodePath(FullFilePath) : GetPathWithDepth(FilePath, depth);
		
		var childrenDefinitions = string.Join("\n\t",
			childrenToDraw.Select(x =>
				x.GetDiagram(depth, useVSCodePaths)
			)
		);

		var childrenRelationships = string.Join("\n\t",
			childrenToDraw.Select(x =>
			{
				var memberName = (this as NodeHierarchy)?
				                 .Node?
				                 .AllChildren
				                 .FirstOrDefault(node => node.Type == x.Name)?.Name
				                 ?? properties.FirstOrDefault(prop => x == prop.Value).Key 
				                 ?? x.Name;
				
				return $"{Name}::{memberName} {(x.DictOfChildren.Count == 0 ? string.Empty : "-")}--> {x.Name}";
			})
		);

		var packageType = this switch
		{
			TypeHierarchy => "Type",
			NodeHierarchy => "Scene",
			_ => throw new NotImplementedException()
		};
			
		return 
			$$"""

			  package {{Name}}-{{packageType}} [[{{newFilePath}}]] {
			  	{{typeDefinition}}
			  	{{childrenDefinitions}}
			  	{{childrenRelationships}}
			  }

			  """;
	}

	private string GetTypeDefinition(int depth, bool useVSCodePaths, out IDictionary<string, BaseHierarchy> children)
	{
		children = ImmutableDictionary<string, BaseHierarchy>.Empty;
		
		var hasScript = !string.IsNullOrEmpty(ScriptPath);
		var filePath = hasScript ? ScriptPath : FilePath;
		var fullFilePath = hasScript ? FullScriptPath : FullFilePath;
		
		var interfaceMethodsString = string.Empty;
		var interfacePropertiesString = string.Empty;

		var newScriptPath = useVSCodePaths ? GetVSCodePath(fullFilePath) : GetPathWithDepth(filePath, depth);
		
		var propertiesFromInterface = GetInterfacePropertyDeclarations().ToList();
		var allProperties = TypeSyntax?.Members.OfType<PropertyDeclarationSyntax>() ?? [];
			
		var props = 
			from child in DictOfChildren
			from prop in allProperties
			where prop.Type.ToString() == child.Key || prop.Type.ToString() == $"I{child.Key}"
			select (prop.Identifier.ValueText, child.Value);

		children =
			(from child in DictOfChildren
				join prop in props on child.Key equals prop.Value.Name into grouping
				from prop in grouping.DefaultIfEmpty()
				orderby prop.Item1, child.Key
				select (prop.Item1 ?? child.Key, child.Value))
			.ToDictionary(x => x.Item1, x => x.Value);

		var insideProp = children;
			
		var externalChildrenString = string.Join("\n\t",
			children.Where(x => propertiesFromInterface
					.All(y => y.Identifier.ValueText != x.Key))
				.Select(x =>
				{
					var scriptDefinitions = string.Empty;
					var propName = x.Key;
					var value = string.Empty;
					var scriptPath = string.Empty;
					
					var hasScript = !string.IsNullOrEmpty(x.Value.ScriptPath);
					var filePath = hasScript ? x.Value.ScriptPath : x.Value.FilePath;
					var fullFilePath = hasScript ? x.Value.FullScriptPath : x.Value.FullFilePath;
					
					scriptPath = useVSCodePaths ? 
						GetVSCodePath(fullFilePath) : 
						GetPathWithDepth(filePath, depth);
					
					var propertyDeclarationSyntax = TypeSyntax?
						.Members
						.OfType<PropertyDeclarationSyntax>()
						.FirstOrDefault(x => x.Identifier.ValueText == propName);
					
					//Get direct link to property declaration
					if (propertyDeclarationSyntax != null)
						value = $"[[{newScriptPath}:{propertyDeclarationSyntax.GetLineNumber()} {propName}]]";
					else if (this is NodeHierarchy nodeHierarchy)
					{
						value = nodeHierarchy
							.Node?
							.AllChildren
							.FirstOrDefault(node => node.Type == propName)?.Name;
					}
					
					if(string.IsNullOrEmpty(value))
						value = propName;
					
					var fileType = hasScript ? "Script" : "Scene";
					
					if(!string.IsNullOrWhiteSpace(scriptPath))
						scriptDefinitions = $" - [[{scriptPath} {fileType}]]";
					
					return value + scriptDefinitions;
				})
		);
		
		if (!string.IsNullOrWhiteSpace(externalChildrenString))
			externalChildrenString = "\n--\n" + externalChildrenString;
		
		if(InterfaceSyntax != null)
		{
			interfacePropertiesString = string.Join("\n\t",
				propertiesFromInterface.Select(x =>
				{
					var propName = x?.Identifier.ValueText;
					var value = $"+ [[{newScriptPath}:{x?.GetLineNumber()} {propName}]]";
					
					if(!insideProp.TryGetValue(propName!, out var child))
						return value;
					
					var scriptPath = useVSCodePaths ? GetVSCodePath(child.FullScriptPath) : GetPathWithDepth(child.ScriptPath, depth);
					
					var scriptDefinitions = $" - [[{scriptPath} Script]]";
					
					return value + scriptDefinitions;
				})
			);

			if (!string.IsNullOrWhiteSpace(interfacePropertiesString))
				interfacePropertiesString = "\n--\n" + interfacePropertiesString;

			var methodsFromInterface = GetInterfaceMethodDeclarations();

			interfaceMethodsString = string.Join("\n\t",
				methodsFromInterface.Select(x =>
					$"[[{newScriptPath}:{x?.GetLineNumber()} {x?.Identifier.Value}()]]"
				)
			);

			if (!string.IsNullOrWhiteSpace(interfaceMethodsString))
				interfaceMethodsString = "\n--\n" + interfaceMethodsString;
		}
		
		var fileType = hasScript ? "Script" : "Scene";

		var spotCharacter = hasScript ? "" : "<< (S,black) >>";

		return 
		$$"""

		class {{Name}} {{spotCharacter}} {
			[[{{newScriptPath}} {{fileType}}File]]{{interfacePropertiesString}}{{interfaceMethodsString}}{{externalChildrenString}}
		}

		""";
	}

	private string GetPathWithDepth(string path, int depth)
	{
		if (string.IsNullOrWhiteSpace(path)) 
			return string.Empty;
		
		var depthString = string.Join("", Enumerable.Repeat("../", depth));
		return depthString + path;
	}

	private string GetVSCodePath(string path)
	{
		if (string.IsNullOrWhiteSpace(path)) 
			return string.Empty;

		return $"vscode://file/{path}";
	}
}