let keyboardAudio;

export function keyboardPosition(keyId, clientY) {
	const bounds = document.getElementById(keyId).getBoundingClientRect();
	return Math.min(1, Math.max(0, (clientY - bounds.top) / bounds.height));
}

export async function enableKeyboardAudio() {
	keyboardAudio ??= new AudioContext();
	await keyboardAudio.resume();
}

export async function playKeyboardSample(wav, attack, strength) {
	await enableKeyboardAudio();
	const buffer = await keyboardAudio.decodeAudioData(wav.slice().buffer);
	const source = keyboardAudio.createBufferSource();
	const gain = keyboardAudio.createGain();
	source.buffer = buffer;
	const now = keyboardAudio.currentTime;
	gain.gain.setValueAtTime(0, now);
	// Samples are peak-normalised by the renderer. Restore a clear dynamic range
	// after rendering: the soft end is 5% of the hard-end amplitude (-26 dB).
	const position = Math.min(1, Math.max(0, strength));
	const level = 0.55 * (0.05 + 0.95 * position * position);
	gain.gain.linearRampToValueAtTime(level, now + Math.max(0.002, attack));
	source.connect(gain).connect(keyboardAudio.destination);
	source.start(now);
	source.onended = () => { source.disconnect(); gain.disconnect(); };
}

function database() {
	return new Promise((resolve, reject) => {
		const request = indexedDB.open('panoramicdata-music-studio', 1);
		request.onupgradeneeded = () => request.result.createObjectStore('songs', { keyPath: 'id' });
		request.onsuccess = () => resolve(request.result);
		request.onerror = () => reject(new Error('Browser song storage is unavailable.'));
	});
}

async function transact(mode, run) {
	const db = await database();
	return new Promise((resolve, reject) => {
		const tx = db.transaction('songs', mode);
		let result;
		const request = run(tx.objectStore('songs'));
		request.onsuccess = () => { result = request.result; };
		tx.oncomplete = () => { db.close(); resolve(result); };
		tx.onerror = () => { db.close(); reject(new Error('Song storage failed; browser storage may be full or disabled.')); };
		tx.onabort = tx.onerror;
	});
}

export async function saveSong(title, data, id) {
	if (data.length > 8 * 1024 * 1024) throw new Error('The saved score exceeds the 8 MiB limit.');
	const song = { id: id || crypto.randomUUID(), title: title.trim().slice(0, 100) || 'Untitled song', savedUtc: new Date().toISOString(), data };
	await transact('readwrite', store => store.put(song));
	return song.id;
}

export async function listSongs() {
	const songs = await transact('readonly', store => store.getAll());
	return JSON.stringify(songs.map(({ id, title, savedUtc }) => ({ id, title, savedUtc })).sort((a, b) => b.savedUtc.localeCompare(a.savedUtc)));
}

export async function loadSong(id) {
	const result = await transact('readonly', store => store.get(id));
	if (!result) throw new Error('That saved song no longer exists in this browser.');
	return result.data;
}

export async function deleteSong(id) {
	await transact('readwrite', store => store.delete(id));
}

export function download(data, name, type) {
	const url = URL.createObjectURL(new Blob([data], { type }));
	const link = document.createElement('a');
	link.href = url;
	link.download = name.replace(/[^a-zA-Z0-9._-]/g, '_');
	link.click();
	setTimeout(() => URL.revokeObjectURL(url), 1000);
}

export async function downloadMp3(audioUrl, name, progress) {
	const wav = await (await fetch(audioUrl)).arrayBuffer();
	return new Promise((resolve, reject) => {
		const worker = new Worker(new URL('./mp3-worker.js', import.meta.url));
		worker.onmessage = async event => {
			if (event.data.error) { worker.terminate(); reject(new Error(event.data.error)); return; }
			if (event.data.progress !== undefined) {
				if (progress) await progress.invokeMethodAsync('Mp3Progress', event.data.progress).catch(() => {});
				return;
			}
			download(event.data.mp3, name, 'audio/mpeg');
			worker.terminate();
			resolve();
		};
		worker.onerror = () => { worker.terminate(); reject(new Error('MP3 encoding failed. WAV download remains available.')); };
		worker.postMessage({ wav }, [wav]);
	});
}
