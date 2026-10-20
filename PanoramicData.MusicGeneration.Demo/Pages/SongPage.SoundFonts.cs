using System.Diagnostics;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using PanoramicData.MusicGeneration.SoundFonts;

namespace PanoramicData.MusicGeneration.Demo.Pages;

public partial class SongPage
{
	/// <summary>Selects the optional SoundFont preview from a direct link.</summary>
	[SupplyParameterFromQuery(Name = "sound")]
	public string? SoundMode { get; set; }

	private string soundMode = "procedural";
	private string lastSoundMode = "procedural";
	private GeneratedSong? proceduralSong;
	private SoundFontRenderer? soundFont;
	private SoundFontRenderResult? soundFontResult;
	private string soundFontName = "GeneralUser GS 2.0.3";
	private int padPreset = 48;
	private int bassPreset = 32;
	private int melodyPreset;
	private int drumPreset = 128 * 128;
	private readonly CancellationTokenSource renderCancellation = new();
	private IEnumerable<SoundFontPreset> MelodicPresets => soundFont?.Presets.Where(p => p.Bank < 128) ?? [];
	private IEnumerable<SoundFontPreset> DrumPresets => soundFont?.Presets.Where(p => p.Bank >= 128) ?? [];
	private string RenderDescription => soundMode == "soundfont"
		? "SoundFont playback uses MeltySynth, with independent stems and effects disabled. Release tails are clipped at the requested duration."
		: "Procedural playback uses the browser's sine function for speed. The written notes match the library's deterministic renderer.";

	private async Task PrepareSoundAsync()
	{
		if (proceduralSong is null)
		{
			return;
		}
		if (soundMode == "procedural")
		{
			sourceSong = proceduralSong;
		}
		else
		{
			await EnsureSoundFontAsync();
			if (soundFontResult is null)
			{
				var clock = Stopwatch.StartNew();
				soundFontResult = await soundFont!.RenderAsync(proceduralSong, RenderOptions(), p => ReportRenderAsync(p, clock), renderCancellation.Token);
			}
			sourceSong = soundFontResult.Song;
		}
		lastSoundMode = soundMode;
	}

	private async Task EnsureSoundFontAsync()
	{
		if (soundFont is not null)
		{
			return;
		}
		await ShowProgressAsync("Loading GeneralUser GS instruments (30.8 MiB, first use only)…");
		var bytes = await Http.GetByteArrayAsync("soundfonts/GeneralUser-GS.sf2");
		await ShowProgressAsync("Preparing SoundFont instruments…");
		soundFont = new SoundFontRenderer(bytes, soundFontName);
	}

	private SoundFontRenderOptions RenderOptions() => new()
	{
		Parts = [.. new SoundFontRenderOptions().Parts.Select(p => p with
		{
			Bank = PresetFor(p.Voice) / 128,
			Program = PresetFor(p.Voice) % 128
		})]
	};

	private int PresetFor(VoiceKind voice) => voice switch
	{
		VoiceKind.Pad => padPreset,
		VoiceKind.Bass => bassPreset,
		VoiceKind.Melody => melodyPreset,
		_ => drumPreset
	};

	private async Task ReportRenderAsync(SoundFontRenderProgress progress, Stopwatch clock)
	{
		if (clock.ElapsedMilliseconds < 100)
		{
			return;
		}
		await ShowProgressAsync($"Rendering SoundFont {progress.Voice}… {progress.Fraction:P0}");
		clock.Restart();
	}

	private async Task ChangeSoundAsync()
	{
		if (busy || proceduralSong is null)
		{
			return;
		}
		busy = true;
		error = null;
		var clock = Stopwatch.StartNew();
		try
		{
			await ShowProgressAsync("Preparing the same score with the selected sound…");
			await PrepareSoundAsync();
			await RemixAsync();
			renderSeconds = clock.Elapsed.TotalSeconds;
		}
		catch (Exception ex) when (IsRenderError(ex))
		{
			error = ex.Message;
			soundMode = lastSoundMode;
		}
		finally
		{
			busy = false;
			busyMessage = null;
		}
	}

	private Task ChangeInstrumentsAsync()
	{
		soundFontResult = null;
		return ChangeSoundAsync();
	}

	private async Task LoadSoundFontAsync(InputFileChangeEventArgs args)
	{
		if (busy)
		{
			return;
		}
		busy = true;
		error = null;
		try
		{
			await ShowProgressAsync("Reading your SF2 bank…");
			await using var stream = args.File.OpenReadStream(64 * 1024 * 1024);
			using var bytes = new MemoryStream();
			await stream.CopyToAsync(bytes);
			var renderer = new SoundFontRenderer(bytes.ToArray(), args.File.Name);
			if (!renderer.Presets.Any(p => p.Bank < 128) || !renderer.Presets.Any(p => p.Bank >= 128))
			{
				throw new ArgumentException("This six-part preview needs a bank with both melodic instruments and a drum kit.");
			}
			soundFont = renderer;
			soundFontName = args.File.Name;
			padPreset = ChoosePreset(48, false);
			bassPreset = ChoosePreset(32, false);
			melodyPreset = ChoosePreset(0, false);
			drumPreset = ChoosePreset(128 * 128, true);
			soundFontResult = null;
		}
		catch (Exception ex) when (IsRenderError(ex))
		{
			error = $"Unable to load the SF2 bank: {ex.Message}";
			return;
		}
		finally
		{
			busy = false;
			busyMessage = null;
		}
		await ChangeSoundAsync();
	}

	private int ChoosePreset(int preferred, bool drum)
	{
		var presets = drum ? DrumPresets : MelodicPresets;
		return presets.Select(p => p.Bank * 128 + p.Program).FirstOrDefault(p => p == preferred, presets.First().Bank * 128 + presets.First().Program);
	}

	private static bool IsRenderError(Exception error) => error is ArgumentException or IOException or InvalidDataException or HttpRequestException or NotSupportedException or OperationCanceledException;
}
