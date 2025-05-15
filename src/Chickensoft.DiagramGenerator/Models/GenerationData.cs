namespace Chickensoft.DiagramGenerator.Models;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

public class GenerationData(ImmutableArray<AdditionalText> tscnFiles, ImmutableArray<GeneratorSyntaxContext> syntaxContexts, string? projectDir)
{
	public ImmutableArray<AdditionalText>  TscnFiles { get; } = tscnFiles;
	public ImmutableArray<GeneratorSyntaxContext>  SyntaxContexts { get; } = syntaxContexts;
	public string? ProjectDir { get; } = projectDir;
	
}