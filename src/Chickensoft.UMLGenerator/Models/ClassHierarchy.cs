namespace Chickensoft.UMLGenerator.Models;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;

public class ClassHierarchy(IGrouping<string, GeneratorSyntaxContext> contextGrouping, GenerationData data) : BaseHierarchy(data)
{
	public List<GeneratorSyntaxContext> ContextList { get; } = contextGrouping.ToList();
	public override string FullFilePath => contextGrouping.Key;
	public override string FullScriptPath => FullFilePath;

	public override void GenerateHierarchy(Dictionary<string, BaseHierarchy> nodeHierarchyList)
	{
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