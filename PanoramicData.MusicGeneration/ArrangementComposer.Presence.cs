using System.Text;

namespace PanoramicData.MusicGeneration;

public static partial class ArrangementComposer
{
	private static ArrangementSettings Freeze(ArrangementSettings settings) => settings with
	{
		Layers = Array.AsReadOnly(settings.Layers.Select(l => l with
		{
			Presence = l.Presence with { Overrides = Array.AsReadOnly(l.Presence.Overrides.ToArray()) }
		}).ToArray())
	};

	private static void ValidateExpression(ArrangementSettings settings)
	{
		Range(settings.ProgressionVariation, 0, 1, "Progression variation");
		Range(settings.SectionContrast, 0, 1, "Section contrast");
		Range(settings.IntroVariation, 0, 1, "Intro variation");
		ArgumentNullException.ThrowIfNull(settings.Endings);
		var weights = settings.Endings;
		var values = new[] { weights.Fade, weights.Cadence, weights.Tag, weights.Stop };
		foreach (var value in values) { Range(value, 0, 1, "Ending weight"); }
		if (values.Sum() <= 0) { throw new ArgumentException("At least one ending weight must be positive."); }
	}

	private static void ValidatePresence(LayerPresence presence)
	{
		ArgumentNullException.ThrowIfNull(presence);
		var values = new[] { presence.Intro, presence.Verse, presence.Chorus, presence.Bridge, presence.Outro };
		foreach (var value in values) { Range(value, 0, 1, "Section presence"); }
		if (presence.Overrides is not { Count: <= 100 }) { throw new ArgumentException("A layer supports at most 100 section overrides."); }
		foreach (var item in presence.Overrides) { ValidateOverride(item); }
		var count = presence.Overrides.Select(o => (o.Kind, o.Occurrence)).Distinct().Count();
		if (count != presence.Overrides.Count) { throw new ArgumentException("Duplicate section occurrence overrides are ambiguous."); }
	}

	private static void ValidateOverride(SectionPresenceOverride item)
	{
		ArgumentNullException.ThrowIfNull(item);
		if (!Enum.IsDefined(item.Kind)) { throw new ArgumentException("Unknown section kind in a presence override."); }
		Range(item.Occurrence, 1, 100, "Section occurrence");
		Range(item.Probability, 0, 1, "Override probability");
	}

	private static List<LayerParticipation> PlanParticipation(ArrangementSettings settings, List<SectionEvent> sections)
	{
		var result = new List<LayerParticipation>();
		foreach (var layer in settings.Layers)
		{
			foreach (var section in sections)
			{
				var occurrence = sections.Take(section.Index + 1).Count(s => s.Kind == section.Kind);
				var probability = PresenceProbability(layer.Presence, section.Kind, occurrence);
				var stream = StableStream(layer.Id) + section.Index * 7919L;
				var present = new Pcg32(settings.Seed, stream).NextDouble() < probability;
				result.Add(new(layer.Id, section.Index, occurrence, probability, present));
			}
		}
		return result;
	}

	private static double PresenceProbability(LayerPresence presence, SectionKind kind, int occurrence)
	{
		var specific = presence.Overrides.SingleOrDefault(o => o.Kind == kind && o.Occurrence == occurrence);
		if (specific is not null) { return specific.Probability; }
		return kind switch
		{
			SectionKind.Intro => presence.Intro,
			SectionKind.Verse => presence.Verse,
			SectionKind.Chorus => presence.Chorus,
			SectionKind.Bridge => presence.Bridge,
			_ => presence.Outro
		};
	}

	private static long StableStream(string id)
	{
		var hash = 2166136261U;
		foreach (var value in Encoding.UTF8.GetBytes(id)) { hash = unchecked((hash ^ value) * 16777619U); }
		return hash;
	}
}
