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
			var pitch = 36 + bar.Chord.Root + (index % 2 != 0 ? bar.Chord.Intervals[2] : 0);
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

	private static bool IsBeat(double at) => Math.Abs(at - Math.Round(at)) < 1e-8;

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
