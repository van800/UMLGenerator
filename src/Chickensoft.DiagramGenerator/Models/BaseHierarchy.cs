namespace Chickensoft.DiagramGenerator.Models;

using System.Collections.Generic;
using Microsoft.CodeAnalysis;

public abstract class BaseHierarchy
{
	public bool IsRoot => DictOfParents.Count == 0;
	public abstract string Name { get; }
	public abstract string? ScriptPath { get; }
	
	private Dictionary<string, BaseHierarchy> _dictOfChildren = [];
	private Dictionary<string, BaseHierarchy> _dictOfParents = [];
	protected List<GeneratorSyntaxContext> _listOfChildContexts = [];
	
	public IReadOnlyDictionary<string, BaseHierarchy> DictOfChildren => _dictOfChildren;
	public IReadOnlyDictionary<string, BaseHierarchy> DictOfParents => _dictOfParents;
	public IReadOnlyCollection<GeneratorSyntaxContext> ListOfChildContexts => _listOfChildContexts;

	public abstract void GenerateHierarchy(Dictionary<string, BaseHierarchy> nodeHierarchyList);
	public abstract string GetDiagram();

	public void AddChild(BaseHierarchy node)
	{
		_dictOfChildren.Add(node.Name, node);
	}

	public void AddParent(BaseHierarchy node)
	{
		_dictOfParents.Add(node.Name, node);
	}
}