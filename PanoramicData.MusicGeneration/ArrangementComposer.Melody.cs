namespace PanoramicData.MusicGeneration;

public static partial class ArrangementComposer
{
	private static readonly int[] MajorMelodicScale = [0, 2, 4, 5, 7, 9, 11];
	private static readonly int[] MinorMelodicScale = [0, 2, 3, 5, 7, 8, 10];

	private static void Melody(BarContext bar, List<NoteEvent> notes, bool answer)
	{
		var motif = MotifForBar(bar);
		if (answer) { WriteAnswer(bar, notes, motif); return; }
		var offset = SecondMotifBar(bar) ? bar.BeatsPerBar : 0;
		var tones = motif.Notes.Where(n => n.Beat >= offset && n.Beat < offset + bar.BeatsPerBar).ToArray();
		for (var i = 0; i < tones.Length; i++)
		{
			if (OmitMelodyTone(bar, i)) { continue; }
			var tone = tones[i];
			var at = tone.Beat - offset;
			var degree = DevelopedDegree(bar, tone.Degree, i == tones.Length - 1);
			var current = AtHarmony(bar, at);
			var pitch = MelodyPitch(current, RegisteredTarget(bar, degree), IsBeat(at));
			WriteLeadTone(current, notes, tone, pitch, i == tones.Length - 1);
		}
	}

	private static MelodyMotif MotifForBar(BarContext bar)
	{
		if (bar.Section.Kind == SectionKind.Bridge) { return bar.Motifs[2]; }
		return bar.Motifs[UsesHook(bar) || bar.Section.Kind == SectionKind.Intro ? 1 : 0];
	}

	private static bool DevelopPhrase(BarContext bar)
	{
		if (PhrasePosition(bar) == 0) { return false; }
		return new Pcg32(MelodicSeed(bar.Settings), 1000 + bar.Section.Index * 13L + bar.LocalBar / 2)
			.NextDouble() < bar.Settings.Variation;
	}

	private static bool OmitMelodyTone(BarContext bar, int index)
	{
		if (SparseIntro(bar) && index > 0) { return true; }
		return PhrasePosition(bar) == 2 && index % 2 != 0 && DevelopPhrase(bar);
	}

	private static int DevelopedDegree(BarContext bar, int degree, bool last)
	{
		if (!DevelopPhrase(bar)) { return degree; }
		return PhrasePosition(bar) switch
		{
			1 => last ? degree + 1 : degree,
			2 => MotifForBar(bar).Notes[0].Degree * 2 - degree,
			_ => last && SecondMotifBar(bar) ? ClosingDegree(bar) : degree
		};
	}

	private static int ClosingDegree(BarContext bar) => UsesHook(bar) ? 7 : 11;

	private static int PhrasePosition(BarContext bar) => (bar.LocalBar / 2) & 3;

	private static bool SecondMotifBar(BarContext bar) => (bar.LocalBar & 1) != 0;

	private static void WriteAnswer(BarContext bar, List<NoteEvent> notes, MelodyMotif motif)
	{
		if (!SecondMotifBar(bar)) { return; }
		var start = bar.BeatsPerBar - AnswerSpace(bar.Settings);
		var count = bar.BeatsPerBar == 3 ? 1 : 2;
		for (var i = 0; i < count; i++)
		{
			var at = start + i * AnswerSpace(bar.Settings) / 2;
			var target = RegisteredTarget(bar, motif.Notes[^(i + 1)].Degree - 2);
			var current = AtHarmony(bar, at);
			Add(current, notes, MelodyPitch(current, target, IsBeat(at)), at, AnswerSpace(bar.Settings) * 0.35, -12);
		}
	}

	private static int DegreePitch(ArrangementSettings settings, int degree)
	{
		var scale = settings.Minor ? MinorMelodicScale : MajorMelodicScale;
		var octave = (int)Math.Floor(degree / 7.0);
		return 48 + settings.Key + octave * 12 + scale[(degree % 7 + 7) % 7];
	}

	private static int RegisteredTarget(BarContext bar, int degree)
	{
		var highest = MotifForBar(bar).Notes.Max(n => DegreePitch(bar.Settings, n.Degree));
		return DegreePitch(bar.Settings, degree) - (highest > 72 ? 12 : 0);
	}

	private static int MelodyPitch(BarContext bar, int target, bool strong)
	{
		var pitches = Enumerable.Range(48, 25);
		var scale = bar.Settings.Minor ? MinorMelodicScale : MajorMelodicScale;
		var allowed = strong
			? pitches.Where(p => bar.Chord.Intervals.Contains((p - bar.Chord.Root + 120) % 12))
			: pitches.Where(p => scale.Contains((p - bar.Settings.Key + 120) % 12));
		return allowed.OrderBy(p => Math.Abs(p - target)).ThenBy(p => p).First();
	}
}
