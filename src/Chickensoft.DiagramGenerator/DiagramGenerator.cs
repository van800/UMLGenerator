using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis;

namespace Chickensoft.DiagramGenerator;

using System;

/// <summary>
/// A sample source generator that creates C# classes based on the text file (in this case, Domain Driven Design ubiquitous language registry).
/// When using a simple text file as a baseline, we can create a non-incremental source generator.
/// </summary>
[Generator]
public class DiagramGenerator : IIncrementalGenerator
{
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		var provider = context.AdditionalTextsProvider
			.Where(f => Path.GetExtension(f.Path).Equals(".tscn", StringComparison.OrdinalIgnoreCase))
			.Collect();

		context.RegisterSourceOutput(provider, GenerateCode);
	}

	private void GenerateCode(SourceProductionContext context, ImmutableArray<AdditionalText> files)
	{
		foreach (var file in files)
		{
			// Get the text of the file.
			var lines = file.GetText(context.CancellationToken)?.ToString().Split('\n');
			if (lines == null)
				continue;

			string source = string.Empty;

			foreach (var line in lines)
			{
				var className = line.Trim();

				// Build up the source code.
				source += line;
			}
			
			var fileName = Path.GetFileNameWithoutExtension(file.Path);
			
			//context.AddSource($"{fileName}.g.puml", source);
		}
	}
}