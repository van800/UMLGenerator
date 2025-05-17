namespace Chickensoft.UMLGenerator;

using Microsoft.CodeAnalysis.CSharp.Syntax;

public static class MethodDeclarationSyntaxExtensions
{
	public static int GetLineNumber(this MethodDeclarationSyntax method)
	{
		var source = method.SyntaxTree.ToString();
		var subSource = source.Substring(0, method.SpanStart);
		var lineNumber = subSource.Split('\n');
		return lineNumber.Length;
	}
}