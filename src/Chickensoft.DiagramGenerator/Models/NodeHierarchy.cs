namespace Chickensoft.DiagramGenerator.Models;

using System.Collections.Generic;
using System.Linq;
using Godot;

public class NodeHierarchy(TscnListener listener)
{
	public bool IsRootNode => ListOfParents.Count == 0;
	
	private List<NodeHierarchy> _listOfChildren = [];
	private List<NodeHierarchy> _listOfParents = [];
	public IReadOnlyCollection<NodeHierarchy> ListOfChildren => _listOfChildren;
	public IReadOnlyCollection<NodeHierarchy> ListOfParents => _listOfParents;
	
	public Node Node { get; } = listener.RootNode!;
	public string Name { get; } = listener.RootNode?.Name!;
	public string? ScriptPath { get; } = listener.Script?.Path.Replace("res://", "");

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
		if (string.IsNullOrEmpty(ScriptPath))
		{
			classDefinition = 
			$$"""

			class {{Name}} {
				[[{{ScriptPath}} ScriptFile]]
			}

			""";
		}
		
		var packageDefinition = string.Empty;
		if (ListOfChildren.Count != 0)
		{
			packageDefinition +=
			$$"""
			
			package {{Name}}Layer [[{{ScriptPath}}]] {
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
							Name + "-->" + x.Name + "\n"
						)
					)
				}}
			}
			
			""";
		}

		return classDefinition + packageDefinition;
	}
}