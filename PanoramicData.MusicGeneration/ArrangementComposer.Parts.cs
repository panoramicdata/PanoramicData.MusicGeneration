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
		if (ChordStarts(bar.Settings.Beat).Length == 1) { HeldChords(bar, notes); return; }
		var changes = bar.Harmony.Where(c => c.StartSeconds > bar.StartSeconds && c.StartSeconds < bar.StartSeconds + bar.BeatsPerBar * bar.BeatSeconds)
			.Select(c => (c.StartSeconds - bar.StartSeconds) / bar.BeatSeconds);
		foreach (var at in ChordStarts(bar.Settings.Beat).Concat(changes).Distinct().Order().Where(at => at < bar.BeatsPerBar))
		{
			if (AccompanimentRest(bar, at)) { continue; }
			var current = AtHarmony(bar, at);
			var remaining = (current.Chord.EndSeconds - bar.StartSeconds) / bar.BeatSeconds - at;
			foreach (var midi in current.Voicing) { Add(current, notes, midi, at, Math.Min(0.35, remaining), -12); }
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
			var current = AtHarmony(bar, starts[index]);
			var pitch = 36 + current.Chord.Root + (index % 2 != 0 ? current.Chord.Intervals[2] : 0);
			var remaining = (current.Chord.EndSeconds - bar.StartSeconds) / bar.BeatSeconds - starts[index];
			Add(current, notes, pitch, starts[index], Math.Min(0.8, remaining), index == 0 ? 4 : -5);
		}
		BassApproach(bar, notes);
	}

	private static void Arpeggio(BarContext bar, List<NoteEvent> notes)
	{
		for (var step = 0; step < bar.BeatsPerBar * 2; step++)
		{
			if (AccompanimentRest(bar, step * 0.5)) { continue; }
			var current = AtHarmony(bar, step * 0.5);
			var index = step % (current.Voicing.Length * 2 - 2);
			index = index < current.Voicing.Length ? index : current.Voicing.Length * 2 - 2 - index;
			if (PhrasePosition(bar) == 2 && FlowChoice(bar, 1520, bar.Settings.AccompanimentVariation)) { index = current.Voicing.Length - 1 - index; }
			var pitch = current.Voicing[index] + (current.Voicing[index] < 60 ? 12 : 0);
			Add(current, notes, pitch, step * 0.5, 0.48, step % 2 == 0 ? -8 : -16);
		}
	}

	private static bool IsBeat(double at) => Math.Abs(at - Math.Round(at)) < 1e-8;

	private static void Drums(BarContext bar, List<NoteEvent> notes)
	{
		if (bar.Settings.Beat is MusicBeat.Waltz or MusicBeat.Arpeggio) { return; }
		if (bar.Settings.DrumVariation + bar.Settings.DrumFills + bar.Settings.DrumHumanity > 0)
		{
			ExpressiveDrums(bar, notes);
			return;
		}
		LiteralDrums(bar, notes);
	}

	private static void LiteralDrums(BarContext bar, List<NoteEvent> notes)
	{
		var starts = DrumStarts(bar.Settings.Beat, bar.Layer.Pattern);
		var key = DrumKey(bar);
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
