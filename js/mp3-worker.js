// LAME runs separately from the UI. See ../vendor/lamejs/LICENSE and docs/STUDIO.md.
importScripts('../vendor/lamejs/lame.all.js');

onmessage = event => {
	try {
		const wav = event.data.wav;
		const view = new DataView(wav);
		if (view.getUint16(22, true) !== 1 || view.getUint16(34, true) !== 16) throw new Error('Expected 16-bit mono PCM WAV.');
		const inputRate = view.getUint32(24, true);
		const rate = Math.max(22050, inputRate);
		const samples = new Int16Array(wav, 44);
		const count = Math.floor(samples.length * rate / inputRate);
		const encoder = new lamejs.Mp3Encoder(1, rate, 128);
		const chunks = [];
		for (let at = 0; at < count; at += 1152) {
			const block = new Int16Array(Math.min(1152, count - at));
			for (let i = 0; i < block.length; i++) {
				const position = (at + i) * inputRate / rate;
				const index = Math.floor(position);
				const fraction = position - index;
				block[i] = Math.round(samples[index] * (1 - fraction) + (samples[Math.min(index + 1, samples.length - 1)] || 0) * fraction);
			}
			const encoded = encoder.encodeBuffer(block);
			if (encoded.length) chunks.push(new Uint8Array(encoded));
			if (at % (1152 * 64) === 0) postMessage({ progress: at / count });
		}
		const tail = encoder.flush();
		if (tail.length) chunks.push(new Uint8Array(tail));
		const length = chunks.reduce((sum, c) => sum + c.length, 0);
		const mp3 = new Uint8Array(length);
		let offset = 0;
		for (const chunk of chunks) { mp3.set(chunk, offset); offset += chunk.length; }
		postMessage({ mp3 }, [mp3.buffer]);
	} catch (error) { postMessage({ error: error.message }); }
};
