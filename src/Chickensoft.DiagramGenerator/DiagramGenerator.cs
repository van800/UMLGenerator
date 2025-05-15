namespace Chickensoft.DiagramGenerator;

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
public class DiagramGenerator : IIncrementalGenerator
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
		Dictionary<string, BaseHierarchy> nodeHierarchyList = [];
		foreach (var additionalText in data.TscnFiles)
		{
			// Get the text of the file.
			var tscnContent = additionalText.GetText(context.CancellationToken)?.ToString();
			if (tscnContent == null)
				continue;

			var listener = RunTscnBaseListener(tscnContent, context.ReportDiagnostic, additionalText.Path);

			var nodeHierarchy = new NodeHierarchy(listener, additionalText, data.SyntaxContexts);
			nodeHierarchyList.Add(nodeHierarchy.Name, nodeHierarchy);
		}
		
		foreach (var hierarchy in nodeHierarchyList.Values)
		{
			hierarchy.GenerateHierarchy(nodeHierarchyList);
		}

		var rootNodes = nodeHierarchyList.Values.Where(x => x.IsRoot);

		foreach (var node in rootNodes)
		{
			var source =
				$"""
				 @startuml
				 {node.GetDiagram()}
				 @enduml
				 """;
			
			var fileName = node.Name + ".g.puml";
			var destFile = Path.Combine(data.ProjectDir!, fileName);
			
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