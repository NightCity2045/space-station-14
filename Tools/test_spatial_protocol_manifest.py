import copy
import hashlib
import unittest
from spatial_protocol_manifest import validate_protocol_manifest


class ProtocolManifestTest(unittest.TestCase):
    def setUp(self):
        protocol = dict(id="ss14-spatial-295", wire_revision=1, map_format=8,
                        transform_schema=1, world_schema=1, geometry_schema=1,
                        physics_backend="BepuPhysics", physics_version="2.4.0")
        fingerprint = hashlib.sha256("".join(f"{k}={protocol[k]}\n" for k in sorted(protocol)).encode()).hexdigest().upper()
        self.manifest = dict(spatial_protocol=protocol, spatial_protocol_fingerprint=fingerprint,
                             physics_backend="BepuPhysics", physics_version="2.4.0")

    def test_valid_descriptor_and_pinned_source_match(self):
        self.assertEqual(validate_protocol_manifest(self.manifest, self.manifest["spatial_protocol"]),
                         self.manifest["spatial_protocol"])

    def test_corrupt_hash_backend_schema_and_legacy_artifacts_are_rejected(self):
        for field, value in (("spatial_protocol_fingerprint", "0" * 64),
                             ("physics_version", "2.5.0"), ("spatial_protocol", None)):
            changed = copy.deepcopy(self.manifest)
            changed[field] = value
            with self.subTest(field=field), self.assertRaises(ValueError):
                validate_protocol_manifest(changed)
        changed = copy.deepcopy(self.manifest)
        changed["spatial_protocol"]["wire_revision"] = True
        with self.assertRaises(ValueError):
            validate_protocol_manifest(changed)
        source = dict(self.manifest["spatial_protocol"], geometry_schema=2)
        with self.assertRaises(ValueError):
            validate_protocol_manifest(self.manifest, source)


if __name__ == "__main__":
    unittest.main()
