namespace PanoramicData.MusicGeneration;

public static partial class ArrangementComposer
{
	private static void WriteBar(BarContext bar, List<NoteEvent> notes)
	{
		if (WriteEndingHit(bar, notes)) { return; }
		if (IntroRest(bar)) { return; }
		switch (bar.Layer.Pattern)
		{
			case LayerPattern.Chords: Chords(bar, notes); break;
			case LayerPattern.Bass: Bass(bar, notes); break;
			case LayerPattern.Melody: Melody(bar, notes, false); break;
			case LayerPattern.Countermelody: Melody(bar, notes, true); break;
			case LayerPattern.Arpeggio: Arpeggio(bar, notes); break;
			default: Drums(bar, notes); break;
		}
	}

	private static void Chords(BarContext bar, List<NoteEvent> notes)
	{
		var starts = ChordStarts(bar.Settings.Beat);
		var sustain = starts.Length == 1 ? bar.BeatsPerBar * 0.95 : 0.35;
		foreach (var at in starts.Where(at => at < bar.BeatsPerBar))
		{
			foreach (var midi in bar.Voicing) { Add(bar, notes, midi, at, sustain, -12); }
		}
	}

	private static double[] ChordStarts(MusicBeat beat) => beat switch
	{
		MusicBeat.Reggae => [0.5, 1.5, 2.5, 3.5],
		MusicBeat.BossaNova => [0, 0.75, 1.5, 2.5, 3],
		MusicBeat.Stride => [1, 3],
		MusicBeat.Waltz => [1, 2],
		MusicBeat.Rock => [0, 1.5, 2, 3.5],
		_ => [0.0]
	};

	private static void Bass(BarContext bar, List<NoteEvent> notes)
	{
		var starts = bar.Settings.Beat switch
		{
			MusicBeat.BossaNova => new[] { 0.0, 1.5, 2.5 },
			MusicBeat.Reggae => [0, 1.5, 2, 2.75],
			MusicBeat.Swing or MusicBeat.March => [0, 1, 2, 3],
			MusicBeat.Waltz => [0.0],
			_ => [0, 2]
		};
		for (var index = 0; index < starts.Length; index++)
		{
			var pitch = 36 + bar.Chord.Root + (index % 2 != 0 ? 7 : 0);
			Add(bar, notes, pitch, starts[index], 0.8, index == 0 ? 4 : -5);
		}
	}

	private static void Arpeggio(BarContext bar, List<NoteEvent> notes)
	{
		for (var step = 0; step < bar.BeatsPerBar * 2; step++)
		{
			var index = step % (bar.Voicing.Length * 2 - 2);
			index = index < bar.Voicing.Length ? index : bar.Voicing.Length * 2 - 2 - index;
			var pitch = bar.Voicing[index] + (bar.Voicing[index] < 60 ? 12 : 0);
			Add(bar, notes, pitch, step * 0.5, 0.48, step % 2 == 0 ? -8 : -16);
		}
	}

	private static void Melody(BarContext bar, List<NoteEvent> notes, bool answer)
	{
		if (answer && bar.LocalBar % 2 == 0) { return; }
		var times = MelodyRhythm(bar, answer);
		var rng = new Pcg32(bar.Settings.Seed, (bar.LocalBar % 2) + (answer ? 301 : 201));
		var motif = Motif(rng);
		for (var i = 0; i < times.Length; i++)
		{
			var degree = PhraseDegree(bar, motif[(i + bar.LocalBar % 2 * 3) % motif.Length]);
			var variation = MotifVariation(bar, rng);
			var target = MelodyTarget(bar, degree + variation, answer);
			var strong = IsBeat(times[i]);
			var pitch = MelodyPitch(bar, target, strong);
			var next = i + 1 < times.Length ? times[i + 1] : bar.BeatsPerBar - 1.0;
			Add(bar, notes, pitch, times[i], Math.Max(0.15, next - times[i]) * 0.9, strong ? 2 : -12);
		}
	}

	private static int PhraseDegree(BarContext bar, int degree)
	{
		var phrase = bar.LocalBar / 2;
		return phrase == 2 ? degree + 2 : degree;
	}

	private static int MelodyTarget(BarContext bar, int degree, bool answer)
	{
		var lift = UsesHook(bar) ? 7 : 0;
		return 55 + bar.Settings.Key + degree + (answer ? 5 : 0) + lift;
	}

	private static bool IsBeat(double at) => Math.Abs(at - Math.Round(at)) < 1e-8;

	private static int MotifVariation(BarContext bar, Pcg32 rng) =>
		bar.LocalBar % 8 >= 6 && rng.NextDouble() < bar.Settings.Variation ? 1 : 0;

	private static int[] Motif(Pcg32 rng) => rng.Next(0, 4) switch
	{
		0 => [0, 2, 4, 2, 1, 3, 2, 0],
		1 => [4, 3, 1, 2, 0, 2, 4, 1],
		2 => [0, 4, 3, 1, 2, 5, 3, 2],
		_ => [2, 1, 0, 3, 4, 2, 1, 0]
	};

	private static double[] MelodyRhythm(BarContext bar, bool answer)
	{
		if (bar.BeatsPerBar == 3) { return answer ? [2.5] : [0, 1, 1.5]; }
		if (answer) { return [3, 3.5]; }
		return LeadRhythm(bar);
	}

	private static double[] LeadRhythm(BarContext bar)
	{
		if (SparseIntro(bar)) { return [0, 1.5]; }
		if (bar.Section.Kind == SectionKind.Bridge) { return [0.5, 1.5, 2.5]; }
		if (UsesHook(bar)) { return bar.LocalBar % 2 == 0 ? [0, 0.5, 1.5, 2.5] : [0, 1, 2]; }
		if (bar.Settings.Beat == MusicBeat.Stride) { return [0, 0.5, 1.5, 2.5, 3.5]; }
		return bar.LocalBar % 2 == 0 ? [0, 0.75, 1.5, 2.5] : [0, 0.5, 1.5];
	}

	private static int MelodyPitch(BarContext bar, int target, bool strong)
	{
		var pitches = Enumerable.Range(48, 25);
		var scale = bar.Settings.Minor ? new[] { 0, 2, 3, 5, 7, 8, 10 } : [0, 2, 4, 5, 7, 9, 11];
		var allowed = strong
			? pitches.Where(p => bar.Chord.Intervals.Contains((p - bar.Chord.Root + 120) % 12))
			: pitches.Where(p => scale.Contains((p - bar.Settings.Key + 120) % 12));
		return allowed.OrderBy(p => Math.Abs(p - target)).ThenBy(p => p).First();
	}

	private static void Drums(BarContext bar, List<NoteEvent> notes)
	{
		if (bar.Settings.Beat is MusicBeat.Waltz or MusicBeat.Arpeggio) { return; }
		var starts = DrumStarts(bar.Settings.Beat, bar.Layer.Pattern);
		var key = bar.Layer.Pattern switch { LayerPattern.Kick => 36, LayerPattern.Snare => 38, _ => 42 };
		if (bar.Settings.Beat == MusicBeat.BossaNova && key == 38) { key = 37; }
		foreach (var at in starts) { Add(bar, notes, key, at, 0.18, IsBeat(at) ? 0 : -16); }
		TransitionFill(bar, notes, key);
	}

	private static double[] DrumStarts(MusicBeat beat, LayerPattern pattern)
	{
		if (pattern == LayerPattern.HiHat) { return [0, 0.5, 1, 1.5, 2, 2.5, 3, 3.5]; }
		if (beat == MusicBeat.Reggae) { return [2]; }
		if (pattern == LayerPattern.Snare) { return [1, 3]; }
		return beat == MusicBeat.BossaNova ? [0, 1.5, 2, 3.5] : [0, 2];
	}
}
