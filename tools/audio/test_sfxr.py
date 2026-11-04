# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
"""Run: python tools/audio/test_sfxr.py"""
import tempfile
import unittest
from pathlib import Path

import sfxr


class SfxrTests(unittest.TestCase):
    def test_same_seed_is_byte_identical(self):
        with tempfile.TemporaryDirectory() as a, tempfile.TemporaryDirectory() as b:
            sfxr.generate(sfxr.DEFAULT_PRESETS, Path(a))
            sfxr.generate(sfxr.DEFAULT_PRESETS, Path(b))
            names = sorted(p.name for p in Path(a).glob("*.wav"))
            self.assertGreaterEqual(len(names), 20)
            for n in names:
                self.assertEqual((Path(a) / n).read_bytes(), (Path(b) / n).read_bytes(), n)

    def test_different_seed_differs_for_noise(self):
        base = {"wave": "noise", "freq": 2000, "sustain": 0.05, "decay": 0.05}
        self.assertNotEqual(sfxr.synth({**base, "seed": 1}), sfxr.synth({**base, "seed": 2}))

    def test_committed_files_match(self):
        with tempfile.TemporaryDirectory() as a:
            sfxr.generate(sfxr.DEFAULT_PRESETS, Path(a))
            for p in Path(a).glob("*.wav"):
                self.assertEqual(p.read_bytes(), (sfxr.DEFAULT_OUT / p.name).read_bytes(), p.name)


if __name__ == "__main__":
    unittest.main()
