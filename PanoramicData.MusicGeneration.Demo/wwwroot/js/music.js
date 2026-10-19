// Browser glue for the MusicGeneration demo: playback URLs and the piano-roll drawing.

export function createAudioUrl(bytes) {
	return URL.createObjectURL(new Blob([bytes], { type: 'audio/wav' }));
}

const voiceColours = ['#9b8cff', '#4cc2ff', '#7cf0b2', '#ff8f6b', '#ffd666', '#c9d3ea'];

// notes: flat array of [start, duration, midi, voice] per note. Draws a piano roll with labelled axes.
export function drawPianoRoll(canvasId, notes, seconds, lowMidi, highMidi, names, sections = []) {
	const canvas = document.getElementById(canvasId);
	if (!canvas) { return; }
	const ctx = canvas.getContext('2d');
	const W = canvas.width, H = canvas.height, L = 70, R = 12, T = sections.length ? 28 : 12, B = 40;
	const pw = W - L - R, ph = H - T - B;
	const x = (t) => L + (t / seconds) * pw;
	const y = (m) => T + ph - ((m - lowMidi) / (highMidi - lowMidi + 1)) * ph;
	ctx.fillStyle = '#0a0e18'; ctx.fillRect(0, 0, W, H);
	ctx.strokeStyle = '#2a3552'; ctx.fillStyle = '#8f9bb8'; ctx.font = '12px "Segoe UI", sans-serif';
	ctx.strokeRect(L, T, pw, ph);
	for (let m = lowMidi; m <= highMidi; m++) {
		if (m % 12 === 0) {
			const py = y(m);
			ctx.strokeStyle = 'rgba(255,255,255,0.08)'; ctx.beginPath(); ctx.moveTo(L, py); ctx.lineTo(L + pw, py); ctx.stroke();
			ctx.textAlign = 'right'; ctx.fillStyle = '#8f9bb8'; ctx.fillText(names[m - lowMidi], L - 8, py + 4);
		}
	}
	for (let s = 0; s <= seconds; s += 2) {
		const px = x(s);
		ctx.strokeStyle = '#2a3552'; ctx.beginPath(); ctx.moveTo(px, T + ph); ctx.lineTo(px, T + ph + 5); ctx.stroke();
		ctx.textAlign = 'center'; ctx.fillStyle = '#8f9bb8'; ctx.fillText(String(s), px, T + ph + 18);
	}
	ctx.fillStyle = '#c9d3ea'; ctx.textAlign = 'center';
	ctx.fillText('Time (seconds)', L + pw / 2, H - 6);
	ctx.save(); ctx.translate(14, T + ph / 2); ctx.rotate(-Math.PI / 2); ctx.fillText('Pitch (MIDI note, C marked)', 0, 0); ctx.restore();
	for (const section of sections) {
		const left = x(section.start), right = x(Math.min(seconds, section.end));
		ctx.strokeStyle = '#8f9bb8'; ctx.beginPath(); ctx.moveTo(left, T); ctx.lineTo(left, T + ph); ctx.stroke();
		ctx.fillStyle = '#c9d3ea'; ctx.textAlign = 'left'; ctx.font = '10px "Segoe UI", sans-serif';
		ctx.fillText(section.label, left + 3, T - 7, Math.max(1, right - left - 6));
	}
	const cellH = Math.max(2, ph / (highMidi - lowMidi + 1));
	for (let i = 0; i + 3 < notes.length; i += 4) {
		const m = notes[i + 2];
		if (m < lowMidi || m > highMidi) { continue; }
		ctx.fillStyle = voiceColours[notes[i + 3] % voiceColours.length];
		ctx.fillRect(x(notes[i]), y(m) - cellH, Math.max(2, x(notes[i] + notes[i + 1]) - x(notes[i])), cellH);
	}
}

export function revokeAudioUrl(url) {
	URL.revokeObjectURL(url);
}
