let audio;
const rate = 22050;

export async function enableKeyboardAudio() {
	audio ??= new AudioContext();
	await audio.resume();
}

export function createKeyboard(root, callbacks) {
	const controller = new Keyboard(root, callbacks);
	return { dispose: () => controller.dispose() };
}

class Keyboard {
	constructor(root, callbacks) {
		this.root = root;
		this.callbacks = callbacks;
		this.voices = new Map();
		this.events = new AbortController();
		root.dataset.keyboardReady = 'true';
		const options = { signal: this.events.signal };
		root.addEventListener('pointerdown', event => this.pointerDown(event), options);
		for (const type of ['pointerup', 'pointercancel', 'lostpointercapture']) {
			root.addEventListener(type, event => this.release(`p:${event.pointerId}`), options);
		}
		root.addEventListener('keydown', event => this.keyDown(event), options);
		window.addEventListener('keyup', event => this.release(`k:${event.code}`), options);
		window.addEventListener('blur', () => this.stopAll(), options);
		document.addEventListener('visibilitychange', () => { if (document.hidden) this.stopAll(); }, options);
	}

	pointerDown(event) {
		const key = event.target.closest('.piano-key');
		if (!key || event.button !== 0) return;
		event.preventDefault();
		key.focus({ preventScroll: true });
		key.setPointerCapture(event.pointerId);
		const bounds = key.getBoundingClientRect();
		this.begin(`p:${event.pointerId}`, key, (event.clientY - bounds.top) / bounds.height);
	}

	keyDown(event) {
		if (event.code !== 'Space' && event.code !== 'Enter') return;
		const key = event.target.closest('.piano-key');
		if (!key) return;
		event.preventDefault();
		if (!event.repeat) this.begin(`k:${event.code}`, key, 0.5);
	}

	async begin(id, key, strength) {
		if (this.voices.has(id) || this.voices.size >= 24) return;
		const voice = { id: `${id}:${crypto.randomUUID()}`, key, released: false, closed: false, sources: new Set() };
		this.voices.set(id, voice);
		key.classList.add('held');
		try {
			await enableKeyboardAudio();
			const position = Math.max(0, Math.min(1, strength));
			if (!await this.callbacks.invokeMethodAsync('StartKeyboardVoice', voice.id, Number(key.dataset.midi), position)) {
				await this.finish(voice); return;
			}
			if (voice.closed) { await this.callbacks.invokeMethodAsync('StopKeyboardVoice', voice.id); return; }
			voice.gain = audio.createGain();
			voice.gain.connect(audio.destination);
			voice.next = audio.currentTime + 0.01;
			voice.gain.gain.setValueAtTime(0, voice.next);
			voice.gain.gain.linearRampToValueAtTime(0.55 * (0.05 + 0.95 * position * position), voice.next + 0.152 - 0.15 * position);
			await this.pump(voice);
		} catch (error) {
			await this.finish(voice);
			if (!this.disposed) await this.callbacks.invokeMethodAsync('KeyboardError', String(error));
		}
	}

	async pump(voice) {
		let quiet = 0;
		while (!voice.closed) {
			if (voice.next < audio.currentTime + 0.06) {
				const bytes = await this.callbacks.invokeMethodAsync('RenderKeyboardVoice', voice.id, voice.released);
				if (voice.closed) return;
				if (!bytes.length) break;
				const samples = new Float32Array(bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength));
				const peak = samples.reduce((max, value) => Math.max(max, Math.abs(value)), 0);
				quiet = voice.released && peak < 0.00001 ? quiet + 1 : 0;
				this.schedule(voice, samples);
				if (quiet >= 4) break;
			} else {
				await new Promise(resolve => setTimeout(resolve, 10));
			}
		}
		await this.finish(voice, true);
	}

	schedule(voice, samples) {
		const buffer = audio.createBuffer(1, samples.length, rate);
		buffer.copyToChannel(samples, 0);
		const source = audio.createBufferSource();
		source.buffer = buffer;
		source.connect(voice.gain);
		voice.next = Math.max(voice.next, audio.currentTime + 0.005);
		source.start(voice.next);
		voice.next += buffer.duration;
		voice.sources.add(source);
		source.onended = () => { voice.sources.delete(source); source.disconnect(); };
	}

	release(id) {
		const voice = this.voices.get(id);
		if (!voice) return;
		voice.released = true;
		voice.gain?.gain.cancelAndHoldAtTime(audio.currentTime);
		this.voices.delete(id);
		this.updateKey(voice.key);
		// Releasing a pointer leaves its own SF2 tail sounding; another strike is independent.
		this.tails ??= new Set();
		this.tails.add(voice);
	}

	updateKey(key) {
		key.classList.toggle('held', [...this.voices.values()].some(voice => voice.key === key));
	}

	async finish(voice, natural = false) {
		if (voice.closed) return;
		voice.closed = true;
		for (const [id, held] of this.voices) { if (held === voice) this.voices.delete(id); }
		this.tails?.delete(voice);
		this.updateKey(voice.key);
		const end = natural ? Math.max(audio.currentTime, voice.next) : audio.currentTime + 0.02;
		if (voice.gain && !natural) voice.gain.gain.setTargetAtTime(0, audio.currentTime, 0.004);
		for (const source of voice.sources) source.stop(end);
		setTimeout(() => voice.gain?.disconnect(), Math.max(0, end - audio.currentTime) * 1000 + 50);
		await this.callbacks.invokeMethodAsync('StopKeyboardVoice', voice.id);
	}

	async stopAll() {
		await Promise.all([...this.voices.values(), ...(this.tails ?? [])].map(voice => this.finish(voice)));
	}

	async dispose() {
		this.disposed = true;
		this.events.abort();
		await this.stopAll();
	}
}
