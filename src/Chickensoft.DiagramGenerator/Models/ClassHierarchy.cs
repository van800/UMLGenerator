namespace Chickensoft.DiagramGenerator.Models.Godot;

using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public class ClassHierarchy(GeneratorSyntaxContext ctx) : BaseHierarchy
{
	public ClassDeclarationSyntax? ClassSyntax => ctx.Node as ClassDeclarationSyntax;
	public override string Name => (ctx.Node as TypeDeclarationSyntax)!.Identifier.ToString();
	public override string ScriptPath => ctx.SemanticModel.SyntaxTree.FilePath.Replace(Directory.GetCurrentDirectory(), "");

	public override void GenerateHierarchy(Dictionary<string, BaseHierarchy> nodeHierarchyList)
	{
		
	}

	public override string GetDiagram()
	{
		return 
		$$"""
		
		class {{Name}} {
			[[{{ScriptPath}} ScriptFile]]
		}
		
		""";
	}
}