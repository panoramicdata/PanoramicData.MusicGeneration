using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace PanoramicData.MusicGeneration.Demo.Pages;

public partial class StudioPage
{
	private StudioDocument Document(bool includeScore) => new()
	{
		Title = title,
		Settings = includeScore ? score!.Settings : Settings(),
		Score = includeScore ? score : null,
		BankSha256 = rendered?.Metadata.BankSha256
	};

	private async Task RefreshSavedAsync()
	{
		try
		{
			var json = await module!.InvokeAsync<string>("listSongs");
			saved = JsonSerializer.Deserialize(json, StudioJsonContext.Default.ListSavedSongHeader) ?? [];
		}
		catch (JSException ex) { notice = "Local library unavailable: " + ex.Message; }
	}

	private async Task SaveAsync()
	{
		if (score is null || dirty || busy) { return; }
		try
		{
			await module!.InvokeAsync<string>("saveSong", title, JsonSerializer.Serialize(Document(true), StudioJsonContext.Default.StudioDocument), null);
			await RefreshSavedAsync();
			notice = "Song saved in this browser, including its written notes and settings.";
		}
		catch (JSException ex) { error = ex.Message; }
	}

	private Task ExportSettingsAsync() => ExportAsync(false);
	private Task ExportSongAsync() => ExportAsync(true);
	private async Task ExportAsync(bool includeScore)
	{
		try
		{
			if (!includeScore) { ArrangementComposer.Validate(Settings()); }
			var json = JsonSerializer.Serialize(Document(includeScore), StudioJsonContext.Default.StudioDocument);
			await module!.InvokeVoidAsync("download", json, FileName(includeScore ? ".song.json" : ".settings.json"), "application/json");
		}
		catch (Exception ex) when (IsStudioError(ex)) { error = ex.Message; }
	}

	private async Task ImportAsync(InputFileChangeEventArgs args)
	{
		if (busy) { return; }
		try
		{
			await using var stream = args.File.OpenReadStream(8 * 1024 * 1024);
			using var reader = new StreamReader(stream);
			await ReadDocumentAsync(await reader.ReadToEndAsync());
		}
		catch (Exception ex) when (IsStudioError(ex)) { error = ex.Message; }
	}

	private async Task OpenAsync(string id)
	{
		if (busy) { return; }
		try { await ReadDocumentAsync(await module!.InvokeAsync<string>("loadSong", id)); }
		catch (Exception ex) when (IsStudioError(ex)) { error = ex.Message; }
	}

	private async Task ReadDocumentAsync(string json)
	{
		var document = JsonSerializer.Deserialize(StudioDocumentMigration.ExpandDefaults(json), StudioJsonContext.Default.StudioDocument)
			?? throw new ArgumentException("The file does not contain a song or settings.");
		if (document.SchemaVersion != 1) { throw new ArgumentException("Unsupported settings/song file version."); }
		var settings = document.Score?.Settings ?? document.Settings;
		ArrangementComposer.Validate(settings);
		ApplySettings(settings);
		title = string.IsNullOrWhiteSpace(document.Title) ? "Imported song" : document.Title[..Math.Min(document.Title.Length, 100)];
		error = null;
		if (document.Score is null)
		{
			notice = "Settings imported. Generate to compose a new score with them.";
			return;
		}
		busy = true;
		try
		{
			await RenderAsync(document.Score);
			notice = "Saved written score restored and rendered without composing new notes.";
			if (document.BankSha256 is not null && document.BankSha256 != rendered!.Metadata.BankSha256)
			{
				notice += " The instrument bank differs from the saved version, so its sound may differ.";
			}
		}
		finally { busy = false; }
	}

	private void ApplySettings(ArrangementSettings settings)
	{
		seed = settings.Seed; melodySeed = settings.MelodySeed; seconds = settings.Seconds; tempo = settings.Tempo; key = settings.Key; minor = settings.Minor;
		genre = settings.Genre; beat = settings.Beat; progression = settings.Progression; customChords = settings.CustomChords;
		swing = settings.Swing; variation = settings.Variation; sections = settings.Sections;
		progressionVariation = settings.ProgressionVariation; sectionContrast = settings.SectionContrast; introVariation = settings.IntroVariation;
		fadeWeight = settings.Endings.Fade; cadenceWeight = settings.Endings.Cadence; tagWeight = settings.Endings.Tag; stopWeight = settings.Endings.Stop;
		layers = [.. settings.Layers.Select(l => new LayerEditor(l))];
		MarkDirty();
	}

	private async Task DeleteAsync(string id)
	{
		if (!await JS.InvokeAsync<bool>("confirm", "Delete this song from this browser? Export a song JSON backup first if you want to keep it.")) { return; }
		try { await module!.InvokeVoidAsync("deleteSong", id); await RefreshSavedAsync(); }
		catch (JSException ex) { error = ex.Message; }
	}

	private string FileName(string extension) => (string.IsNullOrWhiteSpace(title) ? "song" : title) + extension;
	private async Task DownloadWavAsync()
	{
		if (rendered is not null) { await module!.InvokeVoidAsync("download", rendered.Song.ToWav(), FileName(".wav"), "audio/wav"); }
	}

	private async Task DownloadMp3Async()
	{
		if (busy || audioUrl is null) { return; }
		busy = true;
		error = null;
		try
		{
			await ProgressAsync("Encoding MP3… 0%");
			callback ??= DotNetObjectReference.Create(this);
			await module!.InvokeVoidAsync("downloadMp3", audioUrl, FileName(".mp3"), callback);
			notice = "MP3 download ready.";
		}
		catch (JSException ex) { error = ex.Message; }
		finally { busy = false; }
	}

	[JSInvokable]
	public void Mp3Progress(double fraction)
	{
		status = $"Encoding MP3… {fraction:P0}";
		StateHasChanged();
	}
}
