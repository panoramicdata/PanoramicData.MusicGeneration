"""Fetch only the declared HTTPS research hosts and verify exact input hashes."""

import hashlib
import http.client
from urllib.parse import urlsplit

HOSTS = frozenset(
	{
		"www.mutopiaproject.org",
		"bitmidi.com",
		"bhs.minor9.com",
		"theremin.music.uiowa.edu",
	}
)
MAX_BYTES = 64 * 1024 * 1024


def download_reference(url):
	parsed = urlsplit(url)
	if parsed.scheme != "https" or parsed.hostname not in HOSTS:
		raise ValueError("Reference URL must use a declared HTTPS host")
	if parsed.username or parsed.password or parsed.port not in (None, 443):
		raise ValueError("Reference URL must not contain credentials or a custom port")
	connection = http.client.HTTPSConnection(parsed.hostname, timeout=90)
	try:
		connection.request(
			"GET",
			parsed.path,
			headers={"User-Agent": "PanoramicData.MusicGeneration research"},
		)
		with connection.getresponse() as response:
			if response.status != 200:
				raise ValueError(f"Reference server returned HTTP {response.status}")
			data = response.read(MAX_BYTES + 1)
		if len(data) > MAX_BYTES:
			raise ValueError("Reference exceeds the 64 MiB research input limit")
		return data
	finally:
		connection.close()


def load_reference(path, source, download=False):
	if download and not path.exists():
		path.write_bytes(download_reference(source["url"]))
	if hashlib.sha256(path.read_bytes()).hexdigest() != source["sha256"]:
		raise ValueError(f"Reference changed: {path}")
	return path
