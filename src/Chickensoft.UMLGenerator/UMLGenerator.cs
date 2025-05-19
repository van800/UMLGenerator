namespace Chickensoft.UMLGenerator;

using System.IO;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Models;
using Righthand.GodotTscnParser.Engine.Grammar;

[Generator]
public class UMLGenerator : IIncrementalGenerator
{
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		var tscnProvider = context.AdditionalTextsProvider
			.Where(f => Path.GetExtension(f.Path).Equals(".tscn", StringComparison.OrdinalIgnoreCase))
			.Collect();

		var syntaxProvider = context.SyntaxProvider.CreateSyntaxProvider(
			(node, _) => node is TypeDeclarationSyntax,
			(syntaxContext, _) => syntaxContext
		).Collect();

		var projectDirProvider = context.AnalyzerConfigOptionsProvider
			.Select((optionsProvider, _) =>
			{
				optionsProvider.GlobalOptions
					.TryGetValue("build_property.projectdir", out var projectDir);

				return projectDir ?? Directory.GetCurrentDirectory();
			});

		var finalProvider = tscnProvider
			.Combine(syntaxProvider)
			.Combine(projectDirProvider)
			.Select((x, _) => 
				new GenerationData(x.Left.Left, x.Left.Right, x.Right)
			);

		context.RegisterImplementationSourceOutput(finalProvider, GenerateDiagram);
	}

	private void GenerateDiagram(SourceProductionContext context, GenerationData data)
	{
		Dictionary<string, BaseHierarchy> hierarchyList = [];
		//Look at all TSCN's in the project which are marked as AdditionalFiles
		foreach (var additionalText in data.TscnFiles)
		{
			var tscnContent = additionalText.GetText(context.CancellationToken)?.ToString();
			if (string.IsNullOrWhiteSpace(tscnContent))
				continue;

			var listener = RunTscnBaseListener(tscnContent!, context.ReportDiagnostic, additionalText.Path);

			var nodeHierarchy = new NodeHierarchy(listener, additionalText, data);
			hierarchyList.Add(nodeHierarchy.Name, nodeHierarchy);
		}

		//Look at all TypedFiles
		foreach (var syntaxContextGrouping in data.SyntaxContexts
			         .Where(x => x.Node is ClassDeclarationSyntax)
			         .GroupBy(x => x.SemanticModel.SyntaxTree.FilePath))
		{
			var name = Path.GetFileNameWithoutExtension(syntaxContextGrouping.Key);
			if (!hierarchyList.TryGetValue(name, out var nodeHierarchy))
			{
				var classHierarchy = new ClassHierarchy(syntaxContextGrouping, data);
				hierarchyList.Add(classHierarchy.Name, classHierarchy);
			}
			else
			{
				nodeHierarchy.AddContextList(syntaxContextGrouping.ToList());
			}
		}
		
		foreach (var hierarchy in hierarchyList.Values)
		{
			hierarchy.GenerateHierarchy(hierarchyList);
		}

		var nodesWithAttribute = hierarchyList.Values.Where(x => x.HasUMLAttribute());

		foreach (var node in nodesWithAttribute)
		{
			var fileName = node.Name + ".g.puml";
			var filePath = Path.Combine(Path.GetDirectoryName(node.FilePath) ?? string.Empty, fileName);

			var depth = filePath.Split('\\', '/').Length - 1;
			
			var source =
			$"""
			@startuml
			{node.GetDiagram(depth)}
			@enduml
			""";
			var destFile = Path.Combine(data.ProjectDir!, filePath);
			
			File.WriteAllText(destFile, source);
		}
		
	}
	
	private TscnListener RunTscnBaseListener(string text, Action<Diagnostic> reportDiagnostic, string filePath)
	{
		var input = new AntlrInputStream(text);
		var lexer = new TscnLexer(input);
		lexer.AddErrorListener(new SyntaxErrorListener());
		var tokens = new CommonTokenStream(lexer);
		var parser = new TscnParser(tokens)
		{
			BuildParseTree = true
		};
		parser.AddErrorListener(new ErrorListener());
		var tree = parser.file();
		var listener = new TscnListener(reportDiagnostic, filePath);
		ParseTreeWalker.Default.Walk(listener, tree);
		return listener;
	}
	
	public class SyntaxErrorListener : IAntlrErrorListener<int>
	{
		public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol,
			int line, int charPositionInLine, string msg, RecognitionException e)
		{
			throw new Exception(msg, e);
		}
	}

	public class ErrorListener : BaseErrorListener
	{
		public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
			int line, int charPositionInLine, string msg, RecognitionException e)
		{
			throw new Exception(msg, e);
		}
	}
}