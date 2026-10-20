namespace PanoramicData.MusicGeneration.SoundFonts;

internal static class ArrangementEndingRenderer
{
	internal static double Duration(ArrangedScore score) =>
		score.PerformedSeconds > 0 ? score.PerformedSeconds : score.Settings.Seconds;

	internal static void Validate(ArrangedScore score)
	{
		ValidateParticipation(score);
		if (!double.IsFinite(score.PerformedSeconds) || score.PerformedSeconds < 0)
		{
			throw new ArgumentException("Invalid performed score duration.", nameof(score));
		}
		var duration = Duration(score);
		if (duration > score.Settings.Seconds) { throw new ArgumentException("Performance exceeds the requested ceiling.", nameof(score)); }
		if (score.Ending is null) { return; }
		ValidateEndingPlan(score.Ending, duration);
	}

	private static void ValidateParticipation(ArrangedScore score)
	{
		ArgumentNullException.ThrowIfNull(score.Participation);
		if (score.Participation.Count > score.Tracks.Count * score.Sections.Count)
		{
			throw new ArgumentException("Too many participation decisions.", nameof(score));
		}
		foreach (var choice in score.Participation)
		{
			ArgumentNullException.ThrowIfNull(choice);
			ValidateChoice(score, choice);
		}
	}

	private static void ValidateChoice(ArrangedScore score, LayerParticipation choice)
	{
		if (!score.Tracks.Any(t => t.Instrument.Id == choice.LayerId) || choice.SectionIndex < 0 || choice.SectionIndex >= score.Sections.Count)
		{
			throw new ArgumentException("Unknown layer or section in a participation decision.", nameof(score));
		}
		if (!double.IsFinite(choice.Probability) || choice.Probability is < 0 or > 1 || choice.Occurrence < 1)
		{
			throw new ArgumentException("Invalid participation probability or occurrence.", nameof(score));
		}
	}

	private static void ValidateEndingPlan(EndingPlan ending, double duration)
	{
		if (!Enum.IsDefined(ending.Kind)) { throw new ArgumentException("Unknown ending type.", nameof(ending)); }
		SoundFontRenderer.ValidateMarker(ending.StartSeconds, ending.EndSeconds, duration);
		if (!double.IsFinite(ending.ReleaseSeconds) || ending.ReleaseSeconds is < 0 or > 2)
		{
			throw new ArgumentException("Invalid ending release duration.", nameof(ending));
		}
		if (Math.Abs(ending.EndSeconds + ending.ReleaseSeconds - duration) > 1e-6)
		{
			throw new ArgumentException("Ending and performed duration do not agree.", nameof(ending));
		}
	}

	internal static void Shape(double[] mix, int rate, EndingPlan? ending)
	{
		if (ending is null) { return; }
		var fadeStart = (int)Math.Round(ending.StartSeconds * rate);
		var fadeEnd = Math.Min(mix.Length - 1, (int)Math.Round(ending.EndSeconds * rate));
		for (var frame = fadeStart; frame < mix.Length; frame++)
		{
			var gain = 1.0;
			if (ending.Kind == EndingKind.Fade)
			{
				var remaining = Math.Clamp((double)(fadeEnd - frame) / Math.Max(1, fadeEnd - fadeStart), 0, 1);
				gain = remaining * remaining;
			}
			var release = Math.Clamp((double)(mix.Length - 1 - frame) / Math.Max(1, rate / 50), 0, 1);
			mix[frame] *= gain * release;
		}
	}
}
