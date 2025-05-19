namespace Chickensoft.UMLGenerator.Models;

using System.Collections.Generic;
using System.IO;
using Godot;
using Microsoft.CodeAnalysis;

public class NodeHierarchy(TscnListener listener, AdditionalText additionalText, IEnumerable<GeneratorSyntaxContext> syntaxContexts) : BaseHierarchy(syntaxContexts)
{
	public Node? Node { get; } = listener.RootNode;
	public override string? FilePath { get; } = additionalText.Path.Replace($"{Directory.GetCurrentDirectory()}/", "");
	public override string? ScriptPath { get; } = listener.Script?.Path.Replace("res://", "");

	public override void GenerateHierarchy(Dictionary<string, BaseHierarchy> nodeHierarchyList)
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

	public override string GetDiagram()
	{
		var classDefinition = GetClassDefinition();
		var packageDefinition = GetPackageDefinition();

		return classDefinition + packageDefinition;
	}
}