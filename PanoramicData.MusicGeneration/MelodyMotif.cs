namespace PanoramicData.MusicGeneration;

/// <summary>A two-bar melodic idea before chord adaptation, octave shifts and performed timing.</summary>
public sealed record MelodyMotif(string Id, string? SourceId, int BeatsPerBar, IReadOnlyList<MotifTone> Notes);

/// <summary>A motif event: tonic-relative diatonic degree (7 is the next octave), beat offset, length and velocity accent.</summary>
public sealed record MotifTone(int Degree, double Beat, double DurationBeats, int Accent);
