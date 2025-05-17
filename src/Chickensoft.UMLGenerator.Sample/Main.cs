namespace Chickensoft.UMLGenerator.Sample;

using Godot;
using Chickensoft.GameTools.Displays;

#if DEBUG
using System.Reflection;
using Chickensoft.GoDotTest;
#endif

// This entry-point file is responsible for determining if we should run tests.

public partial class Main : Node2D
{
	public Vector2I DesignResolution => Display.FullHD;

#if DEBUG
	public TestEnvironment Environment = null!;
#endif

	public override void _Ready()
	{
		var test = ConstructorInfo.GetCurrentMethod();
		GetWindow().LookGood(WindowScaleBehavior.UIFixed, DesignResolution);
#if DEBUG
		// If this is a debug build, use GoDotTest to examine the
		// command line arguments and determine if we should run tests.
		Environment = TestEnvironment.From(OS.GetCmdlineArgs());
		if (Environment.ShouldRunTests)
		{
			CallDeferred(nameof(RunTests));
			return;
		}
#endif

		// If we don't need to run tests, we can just switch to the game scene.
		CallDeferred(nameof(StartApp));
	}

#if DEBUG
	private void RunTests()
		=> _ = GoTest.RunTests(Assembly.GetExecutingAssembly(), this, Environment);
#endif

	private void StartApp()
		=> GetTree().ChangeSceneToFile(Instantiator.GetScenePath<Game>());
}