"""Fetch only the declared HTTPS research hosts and verify exact input hashes."""

import hashlib
from urllib.parse import urlsplit

import requests

HOSTS = frozenset(
	{
		"www.mutopiaproject.org",
		"bitmidi.com",
		"bhs.minor9.com",
		"theremin.music.uiowa.edu",
	}
)
MAX_BYTES = 64 * 1024 * 1024


def validated_url(url):
	parsed = urlsplit(url)
	if parsed.scheme != "https" or parsed.hostname not in HOSTS:
		raise ValueError("Reference URL must use a declared HTTPS host")
	if parsed.username or parsed.password or parsed.port not in (None, 443):
		raise ValueError("Reference URL must not contain credentials or a custom port")
	return url


def download_reference(url):
	with requests.get(
		validated_url(url), timeout=90, stream=True, allow_redirects=False
	) as response:
		if response.status_code != 200:
			raise ValueError(f"Reference server returned HTTP {response.status_code}")
		data = bytearray()
		for chunk in response.iter_content(chunk_size=65536):
			data.extend(chunk)
			if len(data) > MAX_BYTES:
				raise ValueError("Reference exceeds the 64 MiB research input limit")
	return bytes(data)


def load_reference(path, source, download=False):
	if download and not path.exists():
		path.write_bytes(download_reference(source["url"]))
	if hashlib.sha256(path.read_bytes()).hexdigest() != source["sha256"]:
		raise ValueError(f"Reference changed: {path}")
	return path
