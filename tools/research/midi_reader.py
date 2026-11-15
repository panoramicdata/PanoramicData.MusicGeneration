"""Read reference MIDI files into per-channel note lists and metadata counters."""

from collections import defaultdict, deque

import mido


class TrackReader:
	def __init__(self, ppq, name):
		"""Start one track's clock, channel note queues and metadata counters."""
		self.ppq, self.name, self.tick, self.pedals = ppq, name, 0, 0
		self.active, self.parts = defaultdict(deque), defaultdict(list)
		self.tempos, self.meters = [], []

	def accept(self, message):
		self.tick += message.time
		if message.type in ("note_on", "note_off"):
			self.note(message)
		else:
			self.metadata(message)

	def metadata(self, message):
		if message.type == "track_name":
			self.name = message.name[:80]
		elif message.type == "set_tempo":
			self.tempos.append((self.tick, message.tempo))
		elif message.type == "time_signature":
			self.meters.append((self.tick, message.numerator, message.denominator))
		elif message.type == "control_change" and message.control == 64:
			self.pedals += 1

	def note(self, message):
		if message.channel == 9:
			return
		key = (message.channel, message.note)
		if message.type == "note_on" and message.velocity > 0:
			self.active[key].append((self.tick, message.velocity))
		else:
			self.release(message, key)

	def release(self, message, key):
		if not self.active[key]:
			return
		start, velocity = self.active[key].popleft()
		if self.tick > start:
			self.parts[message.channel].append(
				(
					start / self.ppq,
					(self.tick - start) / self.ppq,
					message.note,
					velocity,
				)
			)


def read_midi(path):
	file = mido.MidiFile(path)
	if file.type == 2:
		raise ValueError("Asynchronous type-2 tracks require separate timelines")
	tracks, tempos, meters, pedals = [], [], [], 0
	for index, track in enumerate(file.tracks):
		reader = TrackReader(file.ticks_per_beat, str(index))
		for message in track:
			reader.accept(message)
		if any(reader.active.values()):
			raise ValueError(f"Unclosed notes in {path}, track {index}")
		tracks.extend(
			(f"{reader.name} / channel {channel + 1}", notes)
			for channel, notes in reader.parts.items()
		)
		tempos.extend(reader.tempos)
		meters.extend(reader.meters)
		pedals += reader.pedals
	return tracks, metadata(file.ticks_per_beat, tempos, sorted(meters), pedals)


def metadata(ppq, tempos, meters, pedals):
	return {
		"tempoValues": len({t for _, t in tempos}) or 1,
		"tempoEvents": len(tempos),
		"meters": sorted({f"{n}/{d}" for _, n, d in meters}) or ["4/4"],
		"pedalEvents": pedals,
		"ppq": ppq,
		"firstBarQuarterBeats": meters[0][1] * 4 / meters[0][2] if meters else 4,
	}
