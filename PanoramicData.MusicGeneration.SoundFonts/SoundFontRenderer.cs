using System.Security.Cryptography;
using MeltySynth;

namespace PanoramicData.MusicGeneration.SoundFonts;

/// <summary>Optional SF2 rendering of an existing score. Does not download banks or change composition.</summary>
public sealed partial class SoundFontRenderer
{
	private readonly SoundFont _bank;
	private readonly string _name;
	private readonly string _hash;

	/// <summary>Loads caller-supplied SF2 bytes, at most 64 MiB. The caller remains responsible for asset terms.</summary>
	public SoundFontRenderer(byte[] bank, string name)
	{
		ArgumentNullException.ThrowIfNull(bank);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		if (bank.Length is < 12 or > 64 * 1024 * 1024)
		{
			throw new ArgumentException("Supply an SF2 bank between 12 bytes and 64 MiB.", nameof(bank));
		}
		using var stream = new MemoryStream(bank, false);
		_bank = new SoundFont(stream);
		_name = name;
		_hash = Convert.ToHexString(SHA256.HashData(bank));
		Presets = Array.AsReadOnly(_bank.Presets.Select(p => new SoundFontPreset(p.BankNumber, p.PatchNumber, p.Name)).ToArray());
	}

	/// <summary>Available presets; absent assignments fail rather than silently selecting a fallback instrument.</summary>
	public IReadOnlyList<SoundFontPreset> Presets { get; }

	/// <summary>Renders a score with the default GM assignments and sample rate.</summary>
	public Task<SoundFontRenderResult> RenderAsync(GeneratedSong score) => RenderAsync(score, null, null, CancellationToken.None);

	/// <summary>Renders a score with selected assignments and sample rate.</summary>
	public Task<SoundFontRenderResult> RenderAsync(GeneratedSong score, SoundFontRenderOptions options) =>
		RenderAsync(score, options, null, CancellationToken.None);

	/// <summary>Renders the same notes into mono PCM and independent raw stems; clips release tails at the requested duration.</summary>
	/// <param name="score">An existing composition; its PCM is ignored.</param>
	/// <param name="options">Instrument assignments and PCM rate; null uses the defaults.</param>
	/// <param name="progress">Optional asynchronous callback; browser hosts can yield to paint here.</param>
	/// <param name="cancellationToken">Cancels between short rendering blocks.</param>
	public async Task<SoundFontRenderResult> RenderAsync(GeneratedSong score, SoundFontRenderOptions? options,
		Func<SoundFontRenderProgress, Task>? progress, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(score);
		options ??= new SoundFontRenderOptions();
		ArgumentNullException.ThrowIfNull(options.Parts);
		options = options with { Parts = Array.AsReadOnly(options.Parts.ToArray()) };
		Validate(score, options);
		var length = checked((int)Math.Round(score.Duration.TotalSeconds * options.SampleRate));
		var stems = new Dictionary<VoiceKind, double[]>();
		foreach (var part in options.Parts.OrderBy(p => p.Voice))
		{
			cancellationToken.ThrowIfCancellationRequested();
			stems[part.Voice] = await RenderPartAsync(score, options.SampleRate, length, part, progress, cancellationToken).ConfigureAwait(false);
		}
		var song = Mix(score, options.SampleRate, length, stems);
		if (progress is not null)
		{
			await progress(new(VoiceKind.HiHat, 1)).ConfigureAwait(false);
		}
		return new(song, new SoundFontRenderMetadata
		{
			RendererVersion = typeof(Synthesizer).Assembly.GetName().Version!.ToString(),
			BankName = _name,
			BankSha256 = _hash,
			Options = options
		});
	}

	private void Validate(GeneratedSong score, SoundFontRenderOptions options)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(options.SampleRate, 16000);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(options.SampleRate, 96000);
		if (score.SampleRate <= 0 || score.Duration.TotalSeconds > 3600 || !double.IsFinite(score.Info.DetuneCents)
			|| Math.Abs(score.Info.DetuneCents) > 50)
		{
			throw new ArgumentException("The score needs a valid rate, duration and tuning.", nameof(score));
		}
		ValidateParts(options.Parts);
		foreach (var note in score.Notes)
		{
			ValidateNote(note);
		}
	}

	private void ValidateParts(IReadOnlyList<SoundFontPart> parts)
	{
		foreach (var part in parts)
		{
			ArgumentNullException.ThrowIfNull(part);
		}
		if (parts.Count != 6 || parts.Select(p => p.Voice).Distinct().Count() != 6)
		{
			throw new ArgumentException("Supply exactly one instrument assignment for each of the six roles.", nameof(parts));
		}
		foreach (var part in parts)
		{
			ValidatePart(part);
		}
	}

	private void ValidatePart(SoundFontPart part)
	{
		ArgumentNullException.ThrowIfNull(part);
		ValidatePartValues(part);
		var minimum = IsDrum(part.Voice) ? 128 : 0;
		if (part.Bank < minimum || part.Bank > minimum + 127 || !Presets.Any(p => p.Bank == part.Bank && p.Program == part.Program))
		{
			throw new ArgumentException($"Bank/program {part.Bank}:{part.Program} is unavailable for {part.Voice}.", nameof(part));
		}
	}

	private static void ValidatePartValues(SoundFontPart part)
	{
		if (!Enum.IsDefined(part.Voice) || part.Velocity is < 1 or > 127 || part.Program is < 0 or > 127)
		{
			throw new ArgumentException("Roles, zero-based programs and velocities must be valid.", nameof(part));
		}
	}

	private static void ValidateNote(NoteEvent note)
	{
		if (!Enum.IsDefined(note.Voice) || note.MidiNote is < 0 or > 127 || !double.IsFinite(note.StartSeconds)
			|| !double.IsFinite(note.DurationSeconds) || note.StartSeconds < 0 || note.DurationSeconds <= 0)
		{
			throw new ArgumentException("Notes need valid roles, MIDI keys and finite non-negative timing.", nameof(note));
		}
	}

	private static bool IsDrum(VoiceKind voice) => voice is VoiceKind.Kick or VoiceKind.Snare or VoiceKind.HiHat;

	private static GeneratedSong Mix(GeneratedSong score, int rate, int length, Dictionary<VoiceKind, double[]> stems)
	{
		var mix = new double[length];
		foreach (var stem in stems.Values)
		{
			for (var i = 0; i < length; i++)
			{
				mix[i] += stem[i];
			}
		}
		var peak = mix.Select(Math.Abs).DefaultIfEmpty().Max();
		var gain = peak > 0 ? 0.9 / peak : 1;
		return score with { SampleRate = rate, Samples = [.. mix.Select(s => (float)(s * gain))], Stems = stems, NormalisationGain = gain };
	}
}
