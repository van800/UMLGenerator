namespace Chickensoft.DiagramGenerator.Models;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public class NodeHierarchy(TscnListener listener, AdditionalText additionalText, IEnumerable<GeneratorSyntaxContext>? syntaxContexts)
{
	public bool IsRootNode => ListOfParents.Count == 0;
	
	private List<NodeHierarchy> _listOfChildren = [];
	private List<NodeHierarchy> _listOfParents = [];
	public IReadOnlyCollection<NodeHierarchy> ListOfChildren => _listOfChildren;
	public IReadOnlyCollection<NodeHierarchy> ListOfParents => _listOfParents;
	
	public Node Node { get; } = listener.RootNode!;
	public string Name { get; } = listener.RootNode?.Name!;
	public string? ScriptPath { get; } = listener.Script?.Path.Replace("res://", "");
	public string? ScenePath { get; } = Path.GetRelativePath(Directory.GetCurrentDirectory(), additionalText.Path);

	private void AddChild(NodeHierarchy node)
	{
		_listOfChildren.Add(node);
	}

	private void AddParent(NodeHierarchy node)
	{
		_listOfParents.Add(node);
	}

	public void GenerateHierarchy(Dictionary<string, NodeHierarchy> nodeHierarchyList)
	{
		foreach (var child in Node.AllChildren)
		{
			if (!nodeHierarchyList.TryGetValue(child.Name, out var childNodeHierarchy)) 
				continue;
			
			AddChild(childNodeHierarchy);
			childNodeHierarchy.AddParent(this);
		}
	}

	public string GetDiagram()
	{
		var classDefinition = string.Empty;
		
		var interfaceSyntax = syntaxContexts?.Select(x => x.Node)
			.FirstOrDefault(x => x is InterfaceDeclarationSyntax) as InterfaceDeclarationSyntax;

		var interfaceName = interfaceSyntax?.Identifier.Value?.ToString();
		
		if (!string.IsNullOrEmpty(ScriptPath))
		{
			var interfaceMembersString = string.Empty;
			
			if(interfaceName == $"I{Name}")
			{
				var methods = interfaceSyntax?.Members.Select(x =>
				{
					if (x is MethodDeclarationSyntax stx)
						return stx;
					return null;
				}).Where(x => x != null);

				interfaceMembersString = string.Concat(
					methods.Select(x =>
						x.Identifier.Value + "()\n"
					)
				);
			}
			

			classDefinition = 
			$$"""

			class {{Name}} {
				[[{{ScriptPath}} ScriptFile]]
				{{interfaceMembersString}}
			}

			""";
		}
		
		var packageDefinition = string.Empty;
		if (ListOfChildren.Count != 0)
		{
			var childrenDefinitions = string.Concat(
				ListOfChildren.Select(x =>
					x.GetDiagram()
				)
			);

			var childrenRelationships = string.Concat(
				ListOfChildren.Select(x =>
					Name + "-->" + x.Name + ": Is Child\n"
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