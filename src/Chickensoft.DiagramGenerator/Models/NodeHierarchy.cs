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

	public void AddConnection(NodeHierarchy node)
	{
		_listOfChildren.Add(node);
		node.AddParent(this);
	}

	private void AddParent(NodeHierarchy node)
	{
		_listOfParents.Add(node);
	}

	public string GetDiagram()
	{
		var classDefinition = string.Empty;
		
		var interfaceSyntax = syntaxContexts?.Select(x => x.Node)
			.FirstOrDefault(x => x is InterfaceDeclarationSyntax) as InterfaceDeclarationSyntax;

		var interfaceName = interfaceSyntax?.Identifier.Value?.ToString();
		
		if (!string.IsNullOrEmpty(ScriptPath))
		{
			var memberString = string.Empty;
			
			if(interfaceName == $"I{Name}")
			{
				var methods = interfaceSyntax?.Members.Select(x =>
				{
					if (x is MethodDeclarationSyntax stx)
						return stx;
					return null;
				}).Where(x => x != null);

				memberString = string.Concat(
					methods.Select(x =>
						x.Identifier.Value + "()\n"
					)
				);
			}
			

			classDefinition = 
			$$"""

			class {{Name}} {
				[[{{ScriptPath}} ScriptFile]]
				{{
					memberString
				}}
			}

			""";
		}
		
		var packageDefinition = string.Empty;
		if (ListOfChildren.Count != 0)
		{
			packageDefinition +=
			$$"""
			
			package {{Name}}Scene [[{{ScenePath}}]] {
				{{
					string.Concat(
						ListOfChildren.Select(x => 
							x.GetDiagram()
						)
					)
				}}
				{{
					string.Concat(
						ListOfChildren.Select(x => 
							Name + "-->" + x.Name + ": Is Child\n" 
						)
					)
				}}
			}
			
			""";
		}

		return classDefinition + packageDefinition;
	}
}