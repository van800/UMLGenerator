namespace Chickensoft.UMLGenerator.Models;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;

public class ClassHierarchy(IGrouping<string, GeneratorSyntaxContext> contextGrouping, GenerationData data) : BaseHierarchy(data)
{
	public override List<GeneratorSyntaxContext> ContextList { get; } = contextGrouping.ToList();
	public override string FullFilePath => contextGrouping.Key;
	public override string FullScriptPath => FullFilePath;
}