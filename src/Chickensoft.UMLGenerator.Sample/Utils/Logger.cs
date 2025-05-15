namespace Chickensoft.DiagramGenerator.Sample;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Chickensoft.GodotNodeInterfaces;
using Godot;

public enum LogLevel
{
	Trace,
	Debug,
	Info,
	Warning,
	Error
}

public static class Logger
{
	private static Dictionary<string, LogLevel> _classLogLevel = new();
	
	static Logger()
	{
		var appSettings = FileAccess.GetFileAsString("appsettings.json");
		var parsedJson = Json.ParseString(appSettings).AsGodotDictionary();
		var classLogLevel = parsedJson["LogLevel"].AsGodotDictionary<string, string>();
		foreach (var (className, logLevelName) in classLogLevel)
		{
			if(Enum.TryParse(logLevelName, true, out LogLevel logLevel))
				_classLogLevel[className] = logLevel;
		}
	}
	
	private static string Log(LogLevel level, object obj, string message, int lineNumber, string path)
	{
		var header = GetHeader(level, obj, out var inf);
		var errorMessage = $"{header} {message}";
		var errorPath = $"[indent][indent][color=gray]{path}:{lineNumber}[/color]";

		if (ShouldPrintLog(level, inf))
		{
			GD.PrintRich(errorMessage);
			GD.PrintRich(errorPath);
		}

		return $"{errorMessage}\n{errorPath}";
	}

	private static bool ShouldPrintLog(LogLevel level, NodeInfo inf)
	{
		return _classLogLevel["Default"] <= level ||
		       _classLogLevel.TryGetValue(inf.NodeName, out var logLevel1) && logLevel1 <= level ||
		       _classLogLevel.TryGetValue(inf.BaseType, out var logLevel2) && logLevel2 <= level ||
		       _classLogLevel.TryGetValue(inf.TypeName, out var logLevel3) && logLevel3 <= level;

}
	
	private static string GetHeader(LogLevel level, object obj, out NodeInfo? nodeInf)
	{
		nodeInf = new NodeInfo
			{
				NodeName = "",
				TypeName = obj.GetType().Name,
				BaseType = ""
			};

		if (obj is INode node)
		{
			nodeInf.NodeName = $"{node.Name}";
			nodeInf.BaseType = $"{node.GetClass()}";
		}
		else if (obj is Type type)
		{
			nodeInf.TypeName = type.Name;
			nodeInf.BaseType = type.BaseType?.Name;
		}
		else if(obj.GetType().BaseType?.Name != nameof(Object))
		{
			nodeInf.BaseType = obj.GetType().BaseType?.Name ?? "";
		}
		else if (obj.GetType().GetInterfaces().Length != 0)
		{
			nodeInf.BaseType = obj.GetType().GetInterfaces().First().Name;
		}
		
		var nameString = !string.IsNullOrWhiteSpace(nodeInf.NodeName) ? $"[color=magenta]{nodeInf.NodeName}[/color]@" : "";
		var typeNameString = !string.IsNullOrWhiteSpace(nodeInf.TypeName) ? $"[color=pink]{nodeInf.TypeName}[/color]" : "";
		var baseTypeString = !string.IsNullOrWhiteSpace(nodeInf.BaseType) ? $":[color=cyan]{nodeInf.BaseType}[/color]" : "";

		var classBlock = $"{{{nameString}{typeNameString}{baseTypeString}}}";

		var levelName = level.ToString().ToUpper();
		var color = level switch
		{
			LogLevel.Info => "white",
			LogLevel.Debug => "orange",
			LogLevel.Trace => "green",
			LogLevel.Warning => "yellow",
			LogLevel.Error => "red",
			_ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
		};
		var typeBlock = $"[color={color}][{levelName}][/color]";
		
		return typeBlock + classBlock;
	}

	public static void Trace(this object obj, string message,
		[CallerLineNumber] int lineNumber = 0,
		[CallerFilePath] string path = null!)
	{
		Log(LogLevel.Trace, obj, message, lineNumber, path);
	}

	public static void Debug(this object obj, string message,
		[CallerLineNumber] int lineNumber = 0,
		[CallerFilePath] string path = null!)
	{
		Log(LogLevel.Debug, obj, message, lineNumber, path);
	}

	public static void Info(this object obj, string message,
		[CallerLineNumber] int lineNumber = 0,
		[CallerFilePath] string path = null!)
	{
		Log(LogLevel.Info, obj, message, lineNumber, path);
	}

	public static void Warning(this object obj, string message,
		[CallerLineNumber] int lineNumber = 0,
		[CallerFilePath] string path = null!)
	{
		Log(LogLevel.Warning, obj, message, lineNumber, path);
	}

	public static void Error(this object obj, string message,
		[CallerLineNumber] int lineNumber = 0,
		[CallerFilePath] string path = null!)
	{
		var error = Log(LogLevel.Error, obj, message, lineNumber, path);
		throw new InvalidOperationException(error);
	}

	private class NodeInfo
	{
		public string NodeName { get; set; }
		public string TypeName { get; set; }
		public string BaseType { get; set; }
	}
}

