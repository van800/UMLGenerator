using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Chickensoft.DiagramGenerator.Tests.Utils;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Chickensoft.DiagramGenerator.Tests;

public class DiagramGeneratorTests
{
	private const string _tscnText =
		"""
		[gd_scene load_steps=8 format=3 uid="uid://dqjk33fq44wxh"]
		
		[ext_resource type="Script" uid="uid://d4chmqryqnpum" path="res://World/World.cs" id="1_1kawg"]
		[ext_resource type="PackedScene" uid="uid://1axllc7l5uml" path="res://Camera/Camera.tscn" id="2_kpuu5"]
		[ext_resource type="PackedScene" uid="uid://cmqhdoo5hv6nb" path="res://Player/Player.tscn" id="3_361h0"]
		[ext_resource type="PackedScene" uid="uid://sy4j1aiba33y" path="res://World/UI/WorldUI.tscn" id="6_sm85h"]
		
		[sub_resource type="ProceduralSkyMaterial" id="ProceduralSkyMaterial_v4d2o"]
		
		[sub_resource type="Sky" id="Sky_3uant"]
		sky_material = SubResource("ProceduralSkyMaterial_v4d2o")
		
		[sub_resource type="Environment" id="Environment_3prqi"]
		background_mode = 2
		sky = SubResource("Sky_3uant")
		sky_rotation = Vector3(0, 0, 6.28319)
		sdfgi_use_occlusion = true
		glow_enabled = true
		glow_normalized = true
		glow_intensity = 1.0
		glow_strength = 1.05
		glow_bloom = 0.2
		volumetric_fog_density = 0.0
		adjustment_enabled = true
		adjustment_brightness = 1.2
		adjustment_contrast = 1.1
		
		[node name="World" type="Node"]
		script = ExtResource("1_1kawg")
		
		[node name="WorldEnvironment" type="WorldEnvironment" parent="."]
		environment = SubResource("Environment_3prqi")
		
		[node name="Camera" parent="." instance=ExtResource("2_kpuu5")]
		unique_name_in_owner = true
		
		[node name="Player" parent="." instance=ExtResource("3_361h0")]
		unique_name_in_owner = true
		
		[node name="WorldUI" parent="." instance=ExtResource("6_sm85h")]
		unique_name_in_owner = true
		""";

	[Fact]
	public void TestGenerateDiagramFile()
	{
		// Create an instance of the source generator.
		var generator = new DiagramGenerator();

		// Source generators should be tested using 'GeneratorDriver'.
		GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

		// Add the additional file separately from the compilation.
		driver = driver.AddAdditionalTexts(
			ImmutableArray.Create<AdditionalText>(
				new TestAdditionalFile("./test.tscn", _tscnText))
		);

		// To run generators, we can use an empty compilation.
		var compilation = CSharpCompilation.Create(nameof(DiagramGeneratorTests));

		// Run generators. Don't forget to use the new compilation rather than the previous one.
		driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

		File.Exists(Directory.GetCurrentDirectory() + "./test.g.puml");
	}
}