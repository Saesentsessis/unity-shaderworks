using NUnit.Framework;
using UnityEngine;

namespace Saesentsessis.Shaders.Stripping.Tests
{
	/// <summary>
	/// Settings survive schema changes: values are stored by keyword, groups and schemas by id (with former ids).
	/// </summary>
	public class SettingsTests
	{
		private static ShaderKeywordSchema Schema(string schemaId, KeywordGroup group) => new(schemaId, "Hidden/Test", "Test", new[] { group });

		[Test]
		public void CustomChoices_FollowValuesWhenTheyAreReordered()
		{
			var before = Schema("s", new KeywordGroup("g", "G", new[] { KeywordValue.None("Off"), KeywordValue.Of("_X", "X") }));
			var settings = new ShaderStrippingSettings();
			settings.SetDenied(before, 0, 1, true);

			// A package update inserts a value before _X.
			var after = Schema("s", new KeywordGroup("g", "G", new[] { KeywordValue.None("Off"), KeywordValue.Of("_Y", "Y"), KeywordValue.Of("_X", "X") }));

			Assert.IsTrue(settings.IsDenied(after, 0, 2), "_X is still denied.");
			Assert.IsFalse(settings.IsDenied(after, 0, 1), "The new value is kept by default.");
			Assert.AreEqual(0b011UL, after.GetValues(settings.GetCustomPattern(after), 0));
		}

		[Test]
		public void Renames_AreFollowedThroughFormerIds()
		{
			var before = Schema("old", new KeywordGroup("oldGroup", "G", new[] { KeywordValue.None("Off"), KeywordValue.Of("_OLD", "On") }));
			var settings = new ShaderStrippingSettings();
			settings.SetDenied(before, 0, 1, true);
			settings.AddExclusion(before).SetValue(before, 0, 1);

			var after = new ShaderKeywordSchema("new", "Hidden/Test", "Test",
				new[] { new KeywordGroup("newGroup", "G", new[] { KeywordValue.None("Off"), KeywordValue.Of("_NEW", "On", "_OLD") }, "oldGroup") },
				"old");

			Assert.IsTrue(settings.IsDenied(after, 0, 1));
			Assert.IsTrue(after.Matches(settings.Exclusions[0].SchemaId));
			Assert.AreEqual(1, settings.Exclusions[0].GetValue(after, 0));

			// Editing rewrites the entry with the current ids.
			settings.SetDenied(after, 0, 0, true);
			Assert.IsTrue(settings.IsDenied(after, 0, 0) && settings.IsDenied(after, 0, 1));
		}

		[Test]
		public void SetCustomPattern_TicksExactlyThePatternsValues()
		{
			var group = new KeywordGroup("g", "G", new[] { KeywordValue.None("Off"), KeywordValue.Of("_A", "A"), KeywordValue.Of("_B", "B") });
			var schema = Schema("s", group);
			var settings = new ShaderStrippingSettings();

			settings.SetCustomPattern(schema, schema.WithValues(schema.AllValues, group, 0b101UL));

			Assert.AreEqual(0b101UL, schema.GetValues(settings.GetCustomPattern(schema), 0));
		}

		[Test]
		public void Settings_SurviveSerialization()
		{
			var group = new KeywordGroup("g", "G", new[] { KeywordValue.None("Off"), KeywordValue.Of("_A", "A") });
			var schema = Schema("s", group);
			var settings = new ShaderStrippingSettings { Mode = StrippingMode.Custom, CountInactiveSources = true };
			settings.SetDenied(schema, 0, 1, true);
			settings.AddExclusion(schema).SetValue(schema, 0, 0);

			var json = JsonUtility.ToJson(new Holder { settings = settings });
			var copy = JsonUtility.FromJson<Holder>(json).settings;

			Assert.AreEqual(StrippingMode.Custom, copy.Mode);
			Assert.IsTrue(copy.CountInactiveSources);
			Assert.IsTrue(copy.IsDenied(schema, 0, 1));
			Assert.AreEqual(0, copy.Exclusions[0].GetValue(schema, 0));
			Assert.AreEqual(ShaderStrippingSettings.CurrentVersion, copy.Version);
		}

		[System.Serializable]
		private class Holder
		{
			public ShaderStrippingSettings settings;
		}
	}
}
