// Browser glue for the MusicGeneration demo: playback URLs and yielding before audio processing.

// Let the browser paint the busy state before synchronous audio work occupies its main thread.
export function nextPaint() {
	return new Promise(resolve => {
		if (document.hidden) {
			setTimeout(resolve, 0);
		} else {
			requestAnimationFrame(() => setTimeout(resolve, 0));
		}
	});
}

export function createAudioUrl(bytes) {
	return URL.createObjectURL(new Blob([bytes], { type: 'audio/wav' }));
}

export function revokeAudioUrl(url) {
	URL.revokeObjectURL(url);
}
