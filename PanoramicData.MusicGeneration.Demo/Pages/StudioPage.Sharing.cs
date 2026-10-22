using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PanoramicData.MusicGeneration.Demo.Pages;

public partial class StudioPage
{
	[SupplyParameterFromQuery(Name = "song")]
	public string? SongToken { get; set; }

	private string? lastSongToken;
	private string? shareUrl;
	private string? shareError;
	private bool linkPending;
	private bool sharingReady;

	private void TitleChanged() { linkPending = true; notice = null; }
	private async Task SelectSongLinkAsync() => await module!.InvokeVoidAsync("selectSongLink");

	private async Task InitialiseSharingAsync()
	{
		sharingReady = true;
		await RestoreLinkAsync();
	}

	private async Task RestoreLinkAsync()
	{
		lastSongToken = SongToken;
		linkPending = false;
		shareUrl = null;
		shareError = null;
		if (audioUrl is not null && musicModule is not null) { await musicModule.InvokeVoidAsync("revokeAudioUrl", audioUrl); }
		audioUrl = null;
		score = null;
		rendered = null;
		dirty = true;
		try
		{
			if (SongToken is not null)
			{
				var song = StudioSongLink.Decode(SongToken);
				ApplySettings(song.Settings);
				title = song.Title;
			}
			await GenerateAsync();
		}
		catch (Exception ex) when (IsStudioError(ex))
		{
			linkPending = false;
			shareError = error = ex.Message;
			StateHasChanged();
		}
	}

	private void UpdateSongLink()
	{
		linkPending = false;
		shareUrl = null;
		shareError = null;
		try
		{
			if (!dirty && score?.ComposerVersion != ArrangementComposer.Version)
			{
				throw new ArgumentException("Export this older written score as song JSON to share its exact notes.");
			}
			var token = StudioSongLink.Encode(title, Settings());
			shareUrl = Navigation.GetUriWithQueryParameter("song", token);
			lastSongToken = token;
			Navigation.NavigateTo(shareUrl, replace: true);
		}
		catch (Exception ex) when (IsStudioError(ex))
		{
			shareError = ex.Message;
			lastSongToken = null;
			Navigation.NavigateTo(Navigation.GetUriWithQueryParameter("song", (string?)null), replace: true);
		}
	}

	private async Task CopySongLinkAsync()
	{
		UpdateSongLink();
		if (shareUrl is null) { return; }
		try
		{
			await module!.InvokeVoidAsync("copySongLink", shareUrl);
			notice = dirty ? "Link copied for the current settings. Generate to preview what others will hear." : "Song link copied.";
		}
		catch (JSException) { notice = "Select and copy the song link below."; }
	}
}
