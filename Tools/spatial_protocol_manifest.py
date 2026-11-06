"""Validate the versioned engine protocol before composing or running a pair."""
import hashlib


def validate_protocol_manifest(manifest, expected=None):
    protocol = manifest.get("spatial_protocol")
    if not isinstance(protocol, dict):
        raise ValueError("Engine artifact has no spatial protocol descriptor")
    for key in ("id", "physics_backend", "physics_version"):
        if not isinstance(protocol.get(key), str) or not protocol[key] or "\n" in protocol[key]:
            raise ValueError(f"Invalid protocol field: {key}")
    for key in ("wire_revision", "map_format", "transform_schema", "world_schema", "geometry_schema"):
        if type(protocol.get(key)) is not int or protocol[key] <= 0:
            raise ValueError(f"Invalid protocol field: {key}")
    if expected is not None and protocol != expected:
        raise ValueError("Engine artifact protocol differs from the pinned engine source")
    for key in ("physics_backend", "physics_version"):
        if manifest.get(key) != protocol[key]:
            raise ValueError(f"Engine artifact disagrees with protocol: {key}")
    canonical = "".join(f"{key}={protocol[key]}\n" for key in sorted(protocol))
    fingerprint = hashlib.sha256(canonical.encode("utf-8")).hexdigest().upper()
    if manifest.get("spatial_protocol_fingerprint") != fingerprint:
        raise ValueError("Engine artifact spatial protocol fingerprint mismatch")
    return protocol
