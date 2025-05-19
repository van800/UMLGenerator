namespace Chickensoft.UMLGenerator.Models;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public class ClassHierarchy(IGrouping<string, GeneratorSyntaxContext> contextGrouping, IEnumerable<GeneratorSyntaxContext> syntaxContexts) : BaseHierarchy(syntaxContexts)
{
	public List<GeneratorSyntaxContext> ContextList { get; } = contextGrouping.ToList();
	public override string FilePath => contextGrouping.Key.Replace($"{Directory.GetCurrentDirectory()}/", "");
	public override string ScriptPath => FilePath;

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

	public override string GetDiagram(int depth)
	{
		return GetClassDefinition(depth);
	}
}