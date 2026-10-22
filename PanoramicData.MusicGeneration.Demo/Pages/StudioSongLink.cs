using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PanoramicData.MusicGeneration.Demo.Pages;

internal sealed record SharedSong
{
	public required int LinkVersion { get; init; }
	public required int ComposerVersion { get; init; }
	public required int PlaybackVersion { get; init; }
	public required string BankSha256 { get; init; }
	public required string Title { get; init; }
	public required ArrangementSettings Settings { get; init; }
}

internal static class StudioSongLink
{
	internal const string BankSha256 = "9575028C7A1F589F5770FCCC8CFF2734566AF40CD26ED836944E9A5152688CFE";
	internal const int MaxTokenLength = 24000;
	private const int MaxJsonBytes = 256 * 1024;

	internal static string Encode(string title, ArrangementSettings settings)
	{
		ArrangementComposer.Validate(settings);
		var song = new SharedSong
		{
			LinkVersion = 1,
			ComposerVersion = ArrangementComposer.Version,
			PlaybackVersion = 1,
			BankSha256 = BankSha256,
			Title = title,
			Settings = settings
		};
		Validate(song);
		var json = JsonSerializer.SerializeToUtf8Bytes(song, SongLinkJsonContext.Default.SharedSong);
		if (json.Length > MaxJsonBytes) { throw new ArgumentException("Settings are too large for a song link. Export settings JSON instead."); }
		using var output = new MemoryStream();
		using (var zip = new GZipStream(output, CompressionLevel.SmallestSize, true)) { zip.Write(json); }
		var token = Convert.ToBase64String(output.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
		if (token.Length > MaxTokenLength) { throw new ArgumentException("Settings are too large for a song link. Export settings JSON instead."); }
		return token;
	}

	internal static SharedSong Decode(string token)
	{
		ValidateToken(token);
		var base64 = token.Replace('-', '+').Replace('_', '/');
		base64 = base64.PadRight((base64.Length + 3) / 4 * 4, '=');
		try
		{
			using var input = new MemoryStream(Convert.FromBase64String(base64));
			using var zip = new GZipStream(input, CompressionMode.Decompress);
			var song = JsonSerializer.Deserialize(Inflate(zip), SongLinkJsonContext.Default.SharedSong)
				?? throw new ArgumentException("The song link contains no settings.");
			Validate(song);
			return song;
		}
		catch (Exception ex) when (ex is FormatException or InvalidDataException or InvalidOperationException or JsonException)
		{
			throw new ArgumentException("The song link is damaged or incomplete.", ex);
		}
	}

	private static void ValidateToken(string token)
	{
		if (string.IsNullOrEmpty(token) || token.Length > MaxTokenLength || token.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
		{
			throw new ArgumentException("The song link is invalid or too large.");
		}
	}

	private static byte[] Inflate(GZipStream zip)
	{
		using var output = new MemoryStream();
		var buffer = new byte[4096];
		int read;
		while ((read = zip.Read(buffer)) > 0)
		{
			if (output.Length + read > MaxJsonBytes) { throw new ArgumentException("The song link expands to too much data."); }
			output.Write(buffer, 0, read);
		}
		return output.ToArray();
	}

	private static void Validate(SharedSong song)
	{
		if (song.LinkVersion != 1 || song.ComposerVersion != ArrangementComposer.Version || song.PlaybackVersion != 1)
		{
			throw new ArgumentException("This song link needs a different app/composer version. It cannot be reproduced by this version.");
		}
		if (song.BankSha256 != BankSha256) { throw new ArgumentException("This song link requires a different SoundFont bank."); }
		if (song.Title is null || song.Title.Length > 100) { throw new ArgumentException("The song link has an invalid title."); }
		ArrangementComposer.Validate(song.Settings);
	}
}

[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
[JsonSerializable(typeof(SharedSong))]
internal sealed partial class SongLinkJsonContext : JsonSerializerContext;
