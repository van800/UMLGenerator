namespace Chickensoft.DiagramGenerator.Models;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public class NodeHierarchy(TscnListener listener, AdditionalText additionalText, IEnumerable<GeneratorSyntaxContext> syntaxContexts) : BaseHierarchy
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
	
	public Node Node { get; } = listener.RootNode!;
	public override string Name { get; } = listener.RootNode?.Name!;
	public override string? ScriptPath { get; } = listener.Script?.Path.Replace("res://", "");
	public string? ScenePath { get; } = additionalText.Path.Replace(Directory.GetCurrentDirectory(), "");

	public override void GenerateHierarchy(Dictionary<string, BaseHierarchy> nodeHierarchyList)
	{
		foreach (var child in Node.AllChildren)
		{
			if (!nodeHierarchyList.TryGetValue(child.Name, out var childNodeHierarchy)) 
				continue;
			
			AddChild(childNodeHierarchy);
			childNodeHierarchy.AddParent(this);
		}

		var listOfChildContexts = new List<GeneratorSyntaxContext>();

		if (ClassSyntax != null)
		{
			foreach (var property in ClassSyntax.Members.OfType<PropertyDeclarationSyntax>())
			{
				var type = property.Type.ToString();
				var childContexts = syntaxContexts
					.Where(x =>
					{
						var sourceFileName = Path.GetFileNameWithoutExtension(x.Node.SyntaxTree.FilePath);
						return sourceFileName == type && !DictOfChildren.ContainsKey(type);
					});
				
				listOfChildContexts.AddRange(childContexts);
			}
		}

		if (listOfChildContexts.Count != 0)
		{
			foreach (var ctx in listOfChildContexts)
			{
				var className = Path.GetFileNameWithoutExtension(ctx.SemanticModel.SyntaxTree.FilePath);
				if (!nodeHierarchyList.TryGetValue(className, out var childNodeHierarchy))
				{
					var classHierarchy = new ClassHierarchy(ctx);
					AddChild(classHierarchy);
					continue;
				}
				
				AddChild(childNodeHierarchy);
				childNodeHierarchy.AddParent(this);
			}
		}
	}

	public override string GetDiagram()
	{
		var classDefinition = string.Empty;
		
		if (!string.IsNullOrEmpty(ScriptPath))
		{
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

			classDefinition = 
			$$"""
			
			class {{Name}} {
				[[{{ScriptPath}} ScriptFile]]{{interfaceMembersString}}
			}
			
			""";
		}
		
		var packageDefinition = string.Empty;
		if (DictOfChildren.Count != 0)
		{
			var childrenDefinitions = string.Join("\n\t",
				DictOfChildren.Values.Select(x =>
					x.GetDiagram()
				)
			);

			var childrenRelationships = string.Join("\n\t",
				DictOfChildren.Values.Select(x =>
					$"{Name} --> {x.Name} : Is Child"
				)
			);
			
			packageDefinition +=
			$$"""
			
			package {{Name}}Scene [[{{ScenePath}}]] {
				{{childrenDefinitions}}
				{{childrenRelationships}}
			}
			
			""";
		}

		return classDefinition + packageDefinition;
	}
}