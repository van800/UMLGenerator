namespace Chickensoft.UMLGenerator;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using Microsoft.CodeAnalysis;
using Models.Godot;
using Righthand.GodotTscnParser.Engine.Grammar;
using static Righthand.GodotTscnParser.Engine.Grammar.TscnParser;

public class TscnListener : TscnBaseListener
{
    private readonly Action<Diagnostic> _reportDiagnostic;
    private readonly string _fileName;
    private Node? _lastNode;
    public Node? RootNode { get; private set; }
    public Script? Script { get; private set; }
    public Dictionary<string, Script> Scripts { get; } = new();
    public Dictionary<string, ExtResource> ExtResources { get; } = new();
    public TscnListener(Action<Diagnostic> reportDiagnostic, string fileName)
    {
        _reportDiagnostic = reportDiagnostic;
        _fileName = fileName;
    }
    
    public override void ExitNode([NotNull] NodeContext context)
    {
        var pairs = context.complexPair().GetComplexPairs();
        if (pairs.TryGetValue("name", out var nameValue))
        {
            string? name = nameValue.value()?.GetString();
            Script? script = null;
            if (!string.IsNullOrEmpty(name))
            {
                string? type = null;
                if (pairs.TryGetValue("script", out var scriptExtResourceValue))
                {
                    script = GetExtResourceScript(scriptExtResourceValue);
                    type = script?.ClassName;
                }
                else if (pairs.TryGetValue("type", out var typeValue))
                {
                    type = typeValue.value()?.GetString();
                }
                else if (pairs.TryGetValue("instance", out var instanceValue))
                {
                    type = GetClassNameFromInstance(instanceValue);
                }
                if (!string.IsNullOrEmpty(type))
                {
                    //var subResourceReferences = pairs.Where(p => )
                    HashSet<string> groups = new HashSet<string>();
                    if (pairs.TryGetValue("groups", out var groupsValue))
                    {
                        var groupStrings =
                            from cv in groupsValue.complexValueArray()?.complexValue()
                            let g = cv.value()?.GetString()
                            where !string.IsNullOrWhiteSpace(g)
                            select g;
                        foreach (var g in groupStrings)
                        {
                            groups.Add(g);
                        }
                    }
                    if (pairs.TryGetValue("parent", out var parentValue))
                    {
                        string parentPath = parentValue.value().GetString();
                        Node? parent = _lastNode;
                        if (string.Equals(parentPath, ".", StringComparison.Ordinal))
                        {
                            parent = RootNode;
                        }
                        else if (!string.Equals(_lastNode?.FullName, parentPath, StringComparison.Ordinal))
                        {
                            while (parent is not null && !string.Equals(parent.FullName, parentPath, StringComparison.Ordinal))
                            {
                                parent = parent.Parent;
                            }
                        }
                        if (parent is not null)
                        {
                            _lastNode = new Node(name!, type!, parent, parentPath, script, groups);
                            parent.Children.Add(_lastNode);
                        }
                        else
                        {
                            _reportDiagnostic(Diagnostic.Create(
                                new DiagnosticDescriptor(
                                    "GTSG0002",
                                    $"TSCN parsing error on {_fileName}",
                                    $"File {_fileName}: Could not find parent node for node {name} with parent path {parentPath}",
                                    "Parsing tscn",
                                    DiagnosticSeverity.Warning, true), null));
                        }
                    }
                    else
                    {
                        RootNode = _lastNode = new Node(name!, type!, null, null, script, groups: groups);
                        Script = script;
                    }
                }
            }
        }
        base.ExitNode(context);
    }

    internal string? GetClassNameFromInstance(ComplexValueContext context)
    {
        var extResourceRef = context.extResourceRef()?.resourceRef()?.GetString();
        if (extResourceRef is not null)
        {
            if (ExtResources.TryGetValue(extResourceRef, out var extResource))
            {
                return GetClassName(extResource.Path);
            }
        }
        return null;
    }

    internal Script? GetExtResourceScript(ComplexValueContext context)
    {
        var extResourceRef = context.extResourceRef()?.resourceRef()?.GetString();
        if (extResourceRef is not null)
        {
            if (Scripts.TryGetValue(extResourceRef, out var script))
            {
                return script;
            }
        }
        return null;
    }

    public override void EnterExtResource([NotNull] ExtResourceContext context)
    {
        var pairs = context.pair().GetStringPairs();
        if (pairs.TryGetValue("type", out var type))
        {
            if (pairs.TryGetValue("path", out var path) && !string.IsNullOrEmpty(path)
                                                        && pairs.TryGetValue("id", out var id) && !string.IsNullOrEmpty(id))
            {
                switch (type)
                {
                    case "Script":
                        string className = GetClassName(path);
                        Scripts.Add(id, new Script(className, path));
                        break;
                    default:
                        if (pairs.TryGetValue("uid", out var uid) && !string.IsNullOrEmpty(uid))
                        {
                            ExtResources.Add(id, new ExtResource(uid, id, type, path));
                        }
                        break;
                }
            }
        }
        base.EnterExtResource(context);
    }

    public static string GetClassName(string fileName)
    {
        string rawName = Path.GetFileNameWithoutExtension(fileName);
        string className;
        if (rawName.Contains('.'))
        {
            int index = rawName.IndexOf('.');
            className = rawName.Substring(0, index);
        }
        else
        {
            className = rawName;
        }
        return className.ToPascalCase()!;
    }
}

public static class ListenerExtensions
{
    internal static ImmutableDictionary<string, string> GetStringPairs(this PairContext[] context)
        => context.EnumerateStringPairs().ToImmutableDictionary(p => p.Key, p => p.Value);
    
    internal static IEnumerable<KeyValuePair<string, string>> EnumerateStringPairs(this PairContext[] context)
    {
        foreach (var p in context)
        {
            var terminal = p.children[1] as TerminalNodeImpl;
            if (terminal != null && terminal.Symbol.Type == T__15)
            {
                yield return new(p.children[0].GetText(), p.children[2].GetText().Trim('\"'));
            }
        }
    }
    internal static ImmutableDictionary<string, ComplexValueContext> GetComplexPairs(this ComplexPairContext[] context)
        => context.EnumerateComplexPairs().ToImmutableDictionary(p => p.Key, p => p.Value);
   
    internal static IEnumerable<KeyValuePair<string, ComplexValueContext>> EnumerateComplexPairs(
        this ComplexPairContext[] context)
    {
        foreach (var p in context)
        {
            string name = p.complexPairName().GetText();
            yield return new(name, p.complexValue());
        }
    }
    
    internal static string GetString(this RuleContext context)
    {
        var terminal = context.GetChild(0) as TerminalNodeImpl;
        if (terminal != null && terminal.Symbol.Type == STRING)
        {
            return terminal.Symbol.Text.Trim('\"');
        }
        throw new Exception($"{context.GetText()} is not a STRING");
    }
}