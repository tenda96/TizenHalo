import importlib.util
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("profiles", ROOT / "tools/build-profile.py")
profiles = importlib.util.module_from_spec(spec)
spec.loader.exec_module(profiles)


class BuildProfileTests(unittest.TestCase):
    def test_manifest_identity_preserved_and_runtime_consistent(self):
        source = (ROOT / "app/tizen-manifest.xml").read_bytes()
        original = ET.fromstring(source)
        ns = {"t": profiles.NS}
        paths = []
        for name in profiles.PROFILES:
            profile = profiles.load_profile(name)
            path, version = profiles.make_manifest(profile)
            paths.append(path)
            manifest = ET.parse(path).getroot()
            self.assertEqual(manifest.get("package"), original.get("package"))
            self.assertEqual(version, original.get("version"))
            for tag in ("ui-application", "service-application"):
                app = manifest.find("t:" + tag, ns)
                old = original.find("t:" + tag, ns)
                self.assertEqual(app.get("api-version"), profile["api_version"])
                self.assertEqual(app.get("appid"), old.get("appid"))
                self.assertEqual(app.get("exec"), old.get("exec"))
            self.assertEqual(manifest.get("api-version"), profile["api_version"])
            self.assertEqual([p.text for p in manifest.findall("t:privileges/t:privilege", ns)],
                             [p.text for p in original.findall("t:privileges/t:privilege", ns)])
        self.assertEqual(len(paths), len(set(paths)), "Profile manifests must not share staging paths")
        self.assertEqual((ROOT / "app/tizen-manifest.xml").read_bytes(), source)

    def test_unknown_profile_rejected_before_generation(self):
        with self.assertRaises(ValueError):
            profiles.load_profile("all-tizen-guessed")


if __name__ == "__main__":
    unittest.main()
