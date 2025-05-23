namespace Chickensoft.UMLGenerator.Exceptions;

using System;
using Antlr4.Runtime;

public class ParserException : Exception
{
	public int LineNumber { get; }
	public int CharPositionInLine { get; }
		
	public ParserException(string msg, int line, int charPositionInLine, RecognitionException recognitionException) : base(msg, recognitionException)
	{
		LineNumber = line;
		CharPositionInLine = charPositionInLine;
	}

	public string GetMessageWithFilePath(string filePath)
	{
		return 
		$"""
        Fatal error when parsing through TSCN file: {filePath}:{LineNumber}:{CharPositionInLine}
        Error message: {Message}
        """;
	}
}