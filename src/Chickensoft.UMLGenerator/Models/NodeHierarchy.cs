namespace Chickensoft.UMLGenerator.Models;

using System.Collections.Generic;
using System.IO;
using Godot;
using Microsoft.CodeAnalysis;

public class NodeHierarchy(TscnListener listener, AdditionalText additionalText, GenerationData data) : BaseHierarchy(data)
{
	public Node? Node { get; } = listener.RootNode;
	public override string? FullFilePath { get; } = additionalText.Path;
	public override string? FullScriptPath { get; } = data.ProjectDir + listener.Script?.Path.Replace("res://", "");

	public override void GenerateHierarchy(IDictionary<string, BaseHierarchy> nodeHierarchyList)
	{
		if (Node?.AllChildren != null)
			foreach (var child in Node.AllChildren)
			{
				if (!nodeHierarchyList.TryGetValue(child.Name, out var childNodeHierarchy))
					continue;

				AddChild(childNodeHierarchy);
				childNodeHierarchy.AddParent(this);
			}

		var propertyDeclarations = GetPropertyDeclarations();
		foreach (var ctx in propertyDeclarations)
		{
			var className = Path.GetFileNameWithoutExtension(ctx.SemanticModel.SyntaxTree.FilePath);
			if (!nodeHierarchyList.TryGetValue(className, out var childNodeHierarchy)) 
				continue;
			
			AddChild(childNodeHierarchy);
			childNodeHierarchy.AddParent(this);
		}
	}
}