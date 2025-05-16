namespace Chickensoft.DiagramGenerator.Models;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public abstract class BaseHierarchy(IEnumerable<GeneratorSyntaxContext> syntaxContexts)
{
	public IEnumerable<GeneratorSyntaxContext> LocalSyntaxContexts => 
		syntaxContexts
			.Where(x =>
			{
				var sourceFileName = Path.GetFileNameWithoutExtension(x.Node.SyntaxTree.FilePath);
				return sourceFileName == Name;
			});
	
	public ClassDeclarationSyntax? ClassSyntax => LocalSyntaxContexts.Select(x => x.Node)
		.FirstOrDefault(x => x is ClassDeclarationSyntax) as ClassDeclarationSyntax;
	public InterfaceDeclarationSyntax? InterfaceSyntax => LocalSyntaxContexts.Select(x => x.Node)
		.FirstOrDefault(x => x is InterfaceDeclarationSyntax ctx && ctx.Identifier.Value?.ToString() == $"I{Name}") as InterfaceDeclarationSyntax;
	
	public bool IsRoot => DictOfParents.Count == 0;
	public abstract string FilePath { get; }
	public abstract string ScriptPath { get; }
	public string Name => Path.GetFileNameWithoutExtension(FilePath);
	
	private Dictionary<string, BaseHierarchy> _dictOfChildren = [];
	private Dictionary<string, BaseHierarchy> _dictOfParents = [];
	public virtual List<GeneratorSyntaxContext> ContextList { get; private set; }
	
	public IReadOnlyDictionary<string, BaseHierarchy> DictOfChildren => _dictOfChildren;
	public IReadOnlyDictionary<string, BaseHierarchy> DictOfParents => _dictOfParents;

	public abstract void GenerateHierarchy(Dictionary<string, BaseHierarchy> nodeHierarchyList);
	public abstract string GetDiagram();

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

		var properties = ClassSyntax
			.Members.OfType<PropertyDeclarationSyntax>()
			.Where(x => 
				!x.AttributeLists.SelectMany(x => x.Attributes)
					.Any(x => x.Name.ToString() == "Dependency"));
		
		foreach (var property in properties)
		{
			var type = property.Type.ToString();
			var childContexts = syntaxContexts
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

	internal string GetClassDefinition()
	{
		if (string.IsNullOrEmpty(ScriptPath))
			return string.Empty;
		var interfaceMembersString = string.Empty;
		
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
					$"[[{ScriptPath}:{x?.GetLineNumber()} {x?.Identifier.Value}()]]"
				)
			);
		}

		return 
		$$"""

		class {{Name}} {
			[[{{ScriptPath}} ScriptFile]]{{interfaceMembersString}}
		}

		""";
	}
	
	internal string GetPackageDefinition()
	{
		if (DictOfChildren.Count == 0)
			return String.Empty;
		
		var childrenDefinitions = string.Join("\n\t",
			DictOfChildren.Values.Select(x =>
				x.GetDiagram()
			)
		);

		var childrenRelationships = string.Join("\n\t",
			DictOfChildren.Values.Select(x =>
				$"{Name} --> {x.Name}" //Todo: Add comments on arrows
			)
		);
			
		return 
		$$"""

		package {{Name}}Scene [[{{FilePath}}]] {
			{{childrenDefinitions}}
			{{childrenRelationships}}
		}

		""";
	}
}