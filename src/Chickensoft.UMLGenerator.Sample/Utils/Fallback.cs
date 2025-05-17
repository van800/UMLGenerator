namespace Chickensoft.UMLGenerator.Sample;

using Chickensoft.AutoInject;
using Godot;

public static class Fallback
{
	public static TValue GetFallback<TValue>() where TValue : class
	{
		if (Engine.IsEditorHint())
			return null!;
		throw new ProviderNotFoundException(typeof(TValue));
	}
}