using MeltySynth;

namespace PanoramicData.MusicGeneration.SoundFonts;

public sealed partial class SoundFontRenderer
{
	private readonly record struct RenderEvent(int Frame, int Channel, int Key, bool On);

	private static List<RenderEvent> Events(GeneratedSong score, int rate, int length, VoiceKind voice)
	{
		var events = new List<RenderEvent>();
		var channels = Enumerable.Range(0, 16).Where(c => c != 9).ToArray();
		var available = new int[16];
		foreach (var note in score.Notes.Where(n => n.Voice == voice).OrderBy(n => n.StartSeconds))
		{
			var start = ToFrame(note.StartSeconds, rate, length);
			var end = ToFrame(note.StartSeconds + note.DurationSeconds, rate, length);
			if (start >= end)
			{
				continue;
			}
			var channel = IsDrum(voice) ? 9 : channels.FirstOrDefault(c => available[c] <= start, -1);
			if (channel < 0)
			{
				throw new ArgumentException("A melodic role supports at most 15 overlapping notes.", nameof(score));
			}
			available[channel] = end;
			events.Add(new(start, channel, note.MidiNote, true));
			events.Add(new(end, channel, note.MidiNote, false));
		}
		return [.. events.OrderBy(e => e.Frame).ThenBy(e => e.On)];
	}

	private static int ToFrame(double seconds, int rate, int length) =>
		(int)Math.Clamp(Math.Round(seconds * rate, MidpointRounding.AwayFromZero), 0, length);

	private Synthesizer CreateSynth(int rate, SoundFontPart part, double cents)
	{
		var synth = new Synthesizer(_bank, new SynthesizerSettings(rate)
		{
			BlockSize = 64,
			MaximumPolyphony = 256,
			EnableReverbAndChorus = false
		});
		foreach (var channel in Enumerable.Range(0, 16).Where(c => IsDrum(part.Voice) ? c == 9 : c != 9))
		{
			synth.ProcessMidiMessage(channel, 0xB0, 0, part.Bank % 128);
			synth.ProcessMidiMessage(channel, 0xC0, part.Program, 0);
			var bend = 8192 + (int)Math.Round(cents * 8192 / 200);
			synth.ProcessMidiMessage(channel, 0xE0, bend & 127, bend >> 7);
		}
		return synth;
	}

	private async Task<double[]> RenderPartAsync(GeneratedSong score, int rate, int length, SoundFontPart part,
		Func<SoundFontRenderProgress, Task>? progress, CancellationToken cancellationToken)
	{
		var synth = CreateSynth(rate, part, score.Info.DetuneCents);
		var events = Events(score, rate, length, part.Voice);
		var output = new double[length];
		var left = new float[4096];
		var right = new float[4096];
		var index = 0;
		var cursor = 0;
		var reportAt = 0;
		var held = new int[16, 128];
		while (cursor < length)
		{
			cancellationToken.ThrowIfCancellationRequested();
			while (index < events.Count && events[index].Frame <= cursor)
			{
				Send(synth, events[index++], held, part.Velocity);
			}
			var next = index < events.Count ? events[index].Frame : length;
			var count = Math.Min(left.Length, next - cursor);
			synth.Render(left.AsSpan(0, count), right.AsSpan(0, count));
			for (var i = 0; i < count; i++)
			{
				output[cursor + i] = (left[i] + right[i]) * 0.5;
			}
			cursor += count;
			if (progress is not null && cursor >= reportAt)
			{
				await progress(new(part.Voice, ((int)part.Voice + (double)cursor / length) / 6)).ConfigureAwait(false);
				reportAt = cursor + rate / 2;
			}
		}
		return output;
	}

	private static void Send(Synthesizer synth, RenderEvent message, int[,] held, int velocity)
	{
		if (message.On)
		{
			held[message.Channel, message.Key]++;
			synth.NoteOn(message.Channel, message.Key, velocity);
		}
		else if (--held[message.Channel, message.Key] == 0)
		{
			synth.NoteOff(message.Channel, message.Key);
		}
	}
}
