namespace Chickensoft.DiagramGenerator.Sample;

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Godot;

/// <summary>
///     Utility class that loads and instantiates scenes.
/// </summary>
public interface IInstantiator
{
	/// <summary>Scene tree.</summary>
	public SceneTree SceneTree { get; }

	/// <summary>
	///     Loads and instantiates the given scene.
	/// </summary>
	/// <param name="path">Path to the scene.</param>
	/// <typeparam name="T">Type of the scene's root.</typeparam>
	/// <returns>Instance of the scene.</returns>
	T LoadAndInstantiateScene<T>(string path = "") where T : Node;
	T LoadAndInstantiateResource<T>(string path = "") where T : Resource;
}

/// <summary>
///     Utility class that loads and instantiates scenes.
/// </summary>
public class Instantiator : IInstantiator
{
	public SceneTree SceneTree { get; }
	private Dictionary<string, PackedScene> _sceneDictionary { get; }
	private Dictionary<string, Resource> _resourceDictionary { get; }

	public Instantiator(SceneTree sceneTree)
	{
		SceneTree = sceneTree;
		_sceneDictionary = new Dictionary<string, PackedScene>();
		_resourceDictionary = new Dictionary<string, Resource>();
	}

	public T LoadAndInstantiateScene<T>(string extension = "") where T : Node
	{
		var path = GetScenePath<T>(extension);
		var scene = LoadScene(path);
		return scene.Instantiate<T>();
	}

	public T LoadAndInstantiateResource<T>(string extension = "") where T : Resource
	{
		var path = GetResourcePath<T>(extension);
		return LoadResource<T>(path);
	}

	public static string GetScenePath<T>(string extension = "") where T : Node
	{
		return FindPath<T>(extension + ".tscn");
	}
	
	public static string GetResourcePath<T>(string extension = "") where T : Resource
	{
		return FindPath<T>(extension + ".tres");
	}

	private PackedScene LoadScene(string path)
	{
		if (!_sceneDictionary.TryGetValue(path, out var scene))
		{
			scene = _sceneDictionary[path] = GD.Load<PackedScene>(path);
		}
		return scene;
	}

	private T LoadResource<T>(string path) where T : Resource
	{
		if (!_resourceDictionary.TryGetValue(path, out var resource))
		{
			resource = _resourceDictionary[path] = GD.Load<T>(path);
		}
		return (T)resource;
	}

	private static string FindPath<T>(string extension)
	{
		var customAttribute = typeof(T).GetCustomAttribute<ScriptPathAttribute>();
		if (customAttribute != null)
			return Path.ChangeExtension(customAttribute.Path, null) + extension;

		throw new InvalidOperationException($"Type '{typeof(T)}' does not have a ScriptPathAttribute");
	}
}