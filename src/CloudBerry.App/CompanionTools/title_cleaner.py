#!/usr/bin/env python3
import json
import re
import sys
from pathlib import PureWindowsPath

NOISE_WORDS = {
    "gog",
    "steam",
    "steamrip",
    "fitgirl",
    "portable",
    "repack",
    "x64",
    "x86",
    "win64",
    "windows",
    "shipping",
    "launcher",
    "setup",
}

EDITION_WORDS = {
    "deluxe",
    "definitive",
    "enhanced",
    "ultimate",
    "complete",
    "remastered",
    "remaster",
    "goty",
}


def read_payload():
    raw = sys.stdin.read()
    if not raw.strip():
        return []
    payload = json.loads(raw)
    return payload if isinstance(payload, list) else []


def title_from_path(value):
    if not value:
        return ""
    path = PureWindowsPath(value)
    stem = path.stem or path.name
    parent = path.parent.name if path.parent else ""
    if stem.lower() in {"game", "launcher", "shipping", "win64", "win32"} and parent:
        return parent
    return stem


def split_camel_case(value):
    value = re.sub(r"(?<=[a-z0-9])(?=[A-Z])", " ", value)
    return re.sub(r"(?<=[A-Z])(?=[A-Z][a-z])", " ", value)


def apply_known_aliases(value):
    value = re.sub(r"\b(?:yu\s*gi\s*oh|yugioh)\b", "Yu-Gi-Oh!", value, flags=re.I)
    value = re.sub(r"\bpokemon\b", "Pokemon", value, flags=re.I)
    value = re.sub(r"\bkaiju\s*no\.?\s*8\b", "Kaiju No. 8", value, flags=re.I)
    value = re.sub(r"\binzoi\b", "inZOI", value, flags=re.I)
    value = re.sub(r"\bgx\b", "GX", value, flags=re.I)
    value = re.sub(r"\bxd\b", "XD", value, flags=re.I)
    return value


def smart_title_case(value):
    if not value or not (value.islower() or value.isupper()):
        return value
    small_words = {"a", "an", "and", "at", "for", "from", "in", "of", "on", "or", "the", "to"}
    words = []
    for index, word in enumerate(value.lower().split()):
        if index > 0 and word in small_words:
            words.append(word)
        else:
            words.append(word[:1].upper() + word[1:])
    return " ".join(words)


def clean_title(value, install_path=""):
    title = value.strip() if value else ""
    if not title:
        title = title_from_path(install_path)

    title = re.sub(r"\[[^\]]*\]|\([^\)]*\)", " ", title)
    title = re.sub(r"[_\.]+", " ", title)
    title = split_camel_case(title)
    title = re.sub(r"\b(v|ver|build)\s*\d+(\.\d+)*\b", " ", title, flags=re.I)
    title = re.sub(r"\b\d{5,}\b", " ", title)

    tokens = []
    for token in re.split(r"\s+", title):
        folded = token.strip("-_: ").lower()
        if not folded or folded in NOISE_WORDS:
            continue
        tokens.append(token.strip())

    title = " ".join(tokens)
    title = re.sub(r"\s+", " ", title).strip(" -_:")
    title = apply_known_aliases(smart_title_case(title))
    return title or value.strip() or title_from_path(install_path)


def build_queries(title):
    queries = []

    def add(value):
        value = re.sub(r"\s+", " ", value).strip()
        if value and value.lower() not in {query.lower() for query in queries}:
            queries.append(value)

    add(title)
    add(apply_known_aliases(title))
    if "Pokemon" in title:
        add(title.replace("Pokemon", "Pokémon"))
    add(re.sub(r"\s+[-:\u2013\u2014]\s+.*$", " ", title))
    add(" ".join(token for token in title.split() if token.lower() not in EDITION_WORDS))
    add(re.sub(r"\b(episode|disc|cd)\s*\d+\b", " ", title, flags=re.I))
    return queries[:5]


def confidence(original, cleaned):
    if not cleaned or len(cleaned) < 3:
        return 0.0
    if cleaned == original:
        return 0.95
    if original and cleaned.lower() in original.lower():
        return 0.82
    if original and original.lower() in cleaned.lower():
        return 0.78
    if re.search(r"\byugioh\b|\byu\s*gi\s*oh\b", original, flags=re.I) and "yu-gi-oh" in cleaned.lower():
        return 0.86
    original_tokens = set(re.findall(r"[a-z0-9]+", original.lower()))
    cleaned_tokens = set(re.findall(r"[a-z0-9]+", cleaned.lower()))
    if cleaned_tokens and len(original_tokens & cleaned_tokens) / len(cleaned_tokens) >= 0.8:
        return 0.84
    return 0.68


def main():
    records = read_payload()
    results = []
    for record in records:
        original = str(record.get("title") or "")
        install_path = str(record.get("installPath") or "")
        cleaned = clean_title(original, install_path)
        queries = build_queries(cleaned)
        results.append(
            {
                "id": str(record.get("id") or ""),
                "title": cleaned,
                "searchQuery": queries[0] if queries else cleaned,
                "queries": queries,
                "confidence": confidence(original, cleaned),
            }
        )

    json.dump(results, sys.stdout, ensure_ascii=False)


if __name__ == "__main__":
    main()
