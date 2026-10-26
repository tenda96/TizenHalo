import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("prerelease", ROOT / "tools/prepare-prerelease.py")
prerelease = importlib.util.module_from_spec(spec)
spec.loader.exec_module(prerelease)


class PrereleaseTests(unittest.TestCase):
    def test_package_and_runtime_match(self):
        self.assertEqual(prerelease.release_tag("1.1.0", "tizenhalo-1.1.0-alpha.1"), "v1.1.0-alpha.1")

    def test_stable_or_mismatched_identity_cannot_label_public_test(self):
        for package, runtime in (("1.0.0", "tizenhalo-1.1.0-alpha.1"), ("1.1.0", "tizenhalo-1.1.0"),
                                 ("1.1.0", "tizenhalo-1.1.0-untested")):
            with self.assertRaises(ValueError):
                prerelease.release_tag(package, runtime)


if __name__ == "__main__":
    unittest.main()
