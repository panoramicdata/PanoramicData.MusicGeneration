using System.Diagnostics;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PanoramicData.MusicGeneration.SoundFonts;

namespace PanoramicData.MusicGeneration.Demo.Pages;

public sealed partial class StudioPage
{
	private string title = "My song";
	private int seed = 42;
	private int seconds = 60;
	private int tempo = 100;
	private int key;
	private bool minor;
	private MusicGenre genre;
	private MusicBeat beat;
	private string progression = "Four chords";
	private string customChords = "C, G, Am, F";
	private double swing;
	private double variation = 0.25;
	private bool sections = true;
	private List<LayerEditor> layers = [.. ArrangementPresets.ForGenre(MusicGenre.Pop).Layers.Select(l => new LayerEditor(l))];
	private SoundFontRenderer? bank;
	private ArrangedScore? score;
	private LayeredRenderResult? rendered;
	private IJSObjectReference? module;
	private IJSObjectReference? musicModule;
	private string? audioUrl;
	private string visualizer = "piano";
	private bool busy;
	private bool dirty = true;
	private string? error;
	private string? notice;
	private string status = "Loading instruments…";
	private double renderSeconds;
	private readonly CancellationTokenSource cancellation = new();
	private List<SavedSongHeader> saved = [];
	private DotNetObjectReference<StudioPage>? callback;

	private ArrangementSettings Settings() => new()
	{
		Seed = seed,
		Seconds = seconds,
		Tempo = tempo,
		Key = key,
		Minor = minor,
		Genre = genre,
		Beat = beat,
		Progression = progression,
		CustomChords = customChords,
		Swing = swing,
		Variation = variation,
		Sections = sections,
		Layers = [.. layers.Select(l => l.ToLayer())]
	};

	private IReadOnlyDictionary<string, string> LayerNames => score!.Tracks.ToDictionary(t => t.Instrument.Id, t => t.Instrument.Name);
	private string ChordPreview
	{
		get
		{
			try { return string.Join(" → ", ChordProgressions.Resolve(Settings()).Select(c => c.Symbol)); }
			catch (ArgumentException) { return "Enter recognised chord symbols separated by commas."; }
		}
	}

	private static string GenreName<T>(T value) => value?.ToString() == "BossaNova" ? "Bossa nova" : value?.ToString() ?? "";
	private static string ProgressionLabel(string name) => name switch
	{
		"Four chords" => "Four chords · I–V–vi–IV",
		"Doo-wop" => "Doo-wop · I–vi–IV–V",
		"Jazz turnaround" => "Jazz · ii7–V7–Imaj7–VI7",
		"Canon" => "Canon · I–V–vi–iii–IV–I–IV–V",
		"Ragtime circle" => "Ragtime · I–VI7–II7–V7",
		_ => name
	};

	private IEnumerable<SoundFontPreset> PresetsFor(LayerEditor layer)
	{
		var drum = layer.ToLayer().Voice is VoiceKind.Kick or VoiceKind.Snare or VoiceKind.HiHat;
		return bank?.Presets.Where(p => drum ? p.Bank >= 128 : p.Bank < 128) ?? [];
	}

	private void MarkDirty() { dirty = true; notice = null; }

	private void ApplyGenre()
	{
		var preset = ArrangementPresets.ForGenre(genre);
		beat = preset.Beat; tempo = preset.Tempo; progression = preset.Progression; swing = preset.Swing; variation = preset.Variation;
		layers = [.. preset.Layers.Select(l => new LayerEditor(l))];
		MarkDirty();
	}

	private void ChangePattern(LayerEditor layer)
	{
		var drum = layer.ToLayer().Voice is VoiceKind.Kick or VoiceKind.Snare or VoiceKind.HiHat;
		layer.Bank = drum ? 128 : 0;
		layer.Program = 0;
		MarkDirty();
	}

	private void AddLayer()
	{
		if (layers.Count >= 24) { return; }
		layers.Add(new(new() { Id = Guid.NewGuid().ToString("N"), Name = "New instrument", Pattern = LayerPattern.Countermelody }));
		MarkDirty();
	}

	private void Duplicate(LayerEditor layer)
	{
		if (layers.Count >= 24) { return; }
		layers.Add(new(layer.ToLayer() with { Id = Guid.NewGuid().ToString("N"), Name = layer.Name + " copy" }));
		MarkDirty();
	}

	private void Remove(LayerEditor layer) { if (layers.Count > 1) { layers.Remove(layer); MarkDirty(); } }
	private async Task RandomiseAsync() { seed = Random.Shared.Next(1, 99999); await GenerateAsync(); }

	private async Task GenerateAsync()
	{
		if (busy) { return; }
		busy = true;
		error = null;
		notice = null;
		try
		{
			await ProgressAsync("Writing harmony, motifs and instrumental parts…");
			var written = ArrangementComposer.Compose(Settings());
			await RenderAsync(written);
		}
		catch (OperationCanceledException) { }
		catch (Exception ex) when (IsStudioError(ex)) { error = ex.Message; }
		finally { busy = false; StateHasChanged(); }
	}

	private async Task RenderAsync(ArrangedScore written)
	{
		var token = cancellation.Token;
		await ProgressAsync("Loading SoundFont instruments (30.8 MiB on first use)…");
		bank ??= await Banks.GetAsync();
		token.ThrowIfCancellationRequested();
		var watch = Stopwatch.StartNew();
		var report = Stopwatch.StartNew();
		var result = await bank.RenderScoreAsync(written, 22050, async p =>
		{
			if (report.ElapsedMilliseconds < 150) { return; }
			await ProgressAsync($"Rendering {p.Layer}… {p.Fraction:P0}");
			report.Restart();
		}, token);
		await ProgressAsync("Preparing the player…");
		musicModule ??= await JS.InvokeAsync<IJSObjectReference>("import", "./js/music.js");
		var next = await musicModule.InvokeAsync<string>("createAudioUrl", result.Song.ToWav());
		var previous = audioUrl;
		rendered = result; score = written; audioUrl = next;
		renderSeconds = watch.Elapsed.TotalSeconds;
		dirty = false;
		if (previous is not null) { await musicModule.InvokeVoidAsync("revokeAudioUrl", previous); }
	}

	private async Task ProgressAsync(string message)
	{
		status = message;
		StateHasChanged();
		musicModule ??= await JS.InvokeAsync<IJSObjectReference>("import", "./js/music.js");
		await musicModule.InvokeVoidAsync("nextPaint");
	}

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (!firstRender) { return; }
		module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/studio.js");
		await RefreshSavedAsync();
		await GenerateAsync();
	}

	private static bool IsStudioError(Exception ex) => ex is ArgumentException or IOException or InvalidDataException or JSException or HttpRequestException or System.Text.Json.JsonException;

	public async ValueTask DisposeAsync()
	{
		await cancellation.CancelAsync();
		cancellation.Dispose();
		callback?.Dispose();
		if (musicModule is not null)
		{
			if (audioUrl is not null) { await musicModule.InvokeVoidAsync("revokeAudioUrl", audioUrl); }
			await musicModule.DisposeAsync();
		}
		if (module is not null) { await module.DisposeAsync(); }
	}
}
