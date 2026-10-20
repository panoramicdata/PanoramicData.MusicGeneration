namespace PanoramicData.MusicGeneration;

public static partial class ArrangementComposer
{
	private static readonly double[][] RhythmCells =
	[
		[0, 1.5, 2.5], [0.5, 1.5, 2.5], [0, 0.5, 2], [0, 1, 1.5, 2.5],
		[0, 0.75, 1.5, 2.5], [0, 1, 2], [0, 2.5], [0.5, 1, 2, 2.5],
		[0, 0.5, 1.5, 2, 2.5], [0, 1.75, 2.5], [0.5, 2.5], [0, 0.75, 2, 2.75]
	];

	private static int MelodicSeed(ArrangementSettings settings) => settings.MelodySeed ?? settings.Seed;

	private static System.Collections.ObjectModel.ReadOnlyCollection<MelodyMotif> CreateMotifs(ArrangementSettings settings)
	{
		var rng = new Pcg32(MelodicSeed(settings), 900);
		var centre = rng.Next(5, 9);
		var gesture = CreateGesture(rng);
		return Array.AsReadOnly(new[]
		{
			CreateMotif(settings, "Verse", null, gesture, centre, 901),
			CreateMotif(settings, "Chorus", "Verse", gesture, centre + rng.Next(0, 3), 902),
			CreateMotif(settings, "Bridge", "Verse", [.. gesture.Select(d => -d)], centre - 1, 903)
		});
	}

	private static int[] CreateGesture(Pcg32 rng)
	{
		var direction = rng.Next(0, 2) == 0 ? -1 : 1;
		var step = rng.Next(1, 3);
		var leap = rng.Next(2, 5);
		return [0, direction * step, direction * leap, direction * (leap - 1), rng.Next(-2, 3), 0];
	}

	private static MelodyMotif CreateMotif(ArrangementSettings settings, string id, string? source,
		int[] gesture, int centre, int stream)
	{
		var rng = new Pcg32(MelodicSeed(settings), stream);
		var notes = new List<MotifTone>();
		for (var bar = 0; bar < 2; bar++)
		{
			var limit = bar == 0 ? Beats(settings) : Beats(settings) - AnswerSpace(settings);
			var times = RhythmCell(settings, rng).Where(t => t < limit).ToArray();
			WriteMotifBar(notes, times, gesture, centre, bar * Beats(settings), limit, rng);
		}
		return new(id, source, Beats(settings), notes.AsReadOnly());
	}

	private static void WriteMotifBar(List<MotifTone> notes, double[] times, int[] gesture,
		int centre, double offset, double limit, Pcg32 rng)
	{
		for (var i = 0; i < times.Length; i++)
		{
			var next = i + 1 < times.Length ? times[i + 1] : limit;
			var duration = (next - times[i]) * (0.7 + 0.25 * rng.NextDouble());
			var degree = centre + gesture[i % gesture.Length];
			var accent = IsBeat(times[i]) ? 2 : -10;
			notes.Add(new(degree, offset + times[i], duration, accent));
		}
	}

	private static double[] RhythmCell(ArrangementSettings settings, Pcg32 rng)
	{
		var choice = settings.Beat switch
		{
			MusicBeat.Reggae => new[] { 1, 7, 10 }[rng.Next(0, 3)],
			MusicBeat.Stride or MusicBeat.Arpeggio => new[] { 3, 8, 11 }[rng.Next(0, 3)],
			_ => rng.Next(0, RhythmCells.Length)
		};
		return RhythmCells[choice];
	}

	private static double AnswerSpace(ArrangementSettings settings) => Beats(settings) == 3 ? 0.5 : 1;
}
