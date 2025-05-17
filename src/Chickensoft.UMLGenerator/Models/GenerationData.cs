namespace Chickensoft.UMLGenerator.Models;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

public class GenerationData
{
	public GenerationData(ImmutableArray<AdditionalText> tscnFiles, ImmutableArray<GeneratorSyntaxContext> syntaxContexts, string projectDir)
	{
		ProjectDir = projectDir;
		TscnFiles = tscnFiles;
		SyntaxContexts = syntaxContexts
			.Where(x => 
				x.Node.SyntaxTree.FilePath.Contains(projectDir));
	}

	public ImmutableArray<AdditionalText>  TscnFiles { get; }
	public IEnumerable<GeneratorSyntaxContext> SyntaxContexts { get; }
	public string? ProjectDir { get; }
	
}