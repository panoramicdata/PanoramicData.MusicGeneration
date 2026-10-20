using PanoramicData.MusicGeneration.SoundFonts;

namespace PanoramicData.MusicGeneration.Demo;

public sealed class SoundFontBankService
{
	private readonly HttpClient _http;
	private Task<SoundFontRenderer>? _loading;

	public SoundFontBankService(HttpClient http) => _http = http;

	public Task<SoundFontRenderer> GetAsync() => _loading ??= LoadAsync();

	private async Task<SoundFontRenderer> LoadAsync()
	{
		try
		{
			return new(await _http.GetByteArrayAsync("soundfonts/GeneralUser-GS.sf2"), "GeneralUser GS 2.0.3");
		}
		catch
		{
			_loading = null;
			throw;
		}
	}
}
