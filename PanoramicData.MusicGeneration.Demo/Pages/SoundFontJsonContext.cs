using System.Text.Json.Serialization;
using PanoramicData.MusicGeneration.SoundFonts;

namespace PanoramicData.MusicGeneration.Demo.Pages;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SoundFontRenderMetadata))]
internal sealed partial class SoundFontJsonContext : JsonSerializerContext;
