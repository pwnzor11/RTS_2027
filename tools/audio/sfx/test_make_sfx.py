# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
"""Run: python tools/audio/sfx/test_make_sfx.py   (needs numpy; no model, no network)"""
import re
import tempfile
import tomllib
import unittest
from pathlib import Path

import numpy as np

import make_sfx as m

BRANDS = re.compile(r"command ?& ?conquer|red alert|starcraft|warcraft|star wars|tiberium|westwood|\bea\b|marvel|hollywood", re.I)


def tone(rate=44100, secs=1.0, freq=440.0, pad_before=0.5, pad_after=0.7, amp=0.2, ch=2):
    t = np.arange(int(rate * secs)) / rate
    body = (amp * np.sin(2 * np.pi * freq * t) * np.exp(-3 * t)).astype(np.float32)
    x = np.concatenate([np.zeros(int(rate * pad_before), np.float32), body, np.zeros(int(rate * pad_after), np.float32)])
    return np.stack([x] * ch)


class JobTests(unittest.TestCase):
    def setUp(self):
        self.data = m.load_jobs()
        self.jobs = self.data["jobs"]

    def test_count_and_fields(self):
        self.assertTrue(60 <= len(self.jobs) <= 80, len(self.jobs))
        for j in self.jobs:
            self.assertLessEqual(j["seconds"], m.MODEL_MAX_SECONDS)
            self.assertGreater(len(j["prompt"]), 30, j["id"])
            self.assertIn("music", j["negative"])
            self.assertEqual(j["file"], f"sfx/{j['id']}.wav")
            self.assertIsNone(BRANDS.search(j["prompt"]), j["id"])
            self.assertTrue(j["events"] or j["mix_into"] or j["planned"] or j["repoint"], j["id"])

    def test_ids_do_not_clash_with_existing_sfxr_sounds(self):
        existing = {p.stem for p in m.DEFAULT_SFX_DIR.glob("*.wav") if not (m.DEFAULT_OUT / p.stem).exists()}
        sfxr_ids = {j["id"] for j in self.jobs} & {"fire_bullet_1", "select_1", "sold", "unit_ready"}
        self.assertFalse(sfxr_ids)
        self.assertTrue(existing)

    def test_events_exist_in_sounds_toml(self):
        text = m.DEFAULT_SOUNDS.read_text(encoding="utf-8")
        tables = tomllib.loads(text)
        for j in self.jobs:
            for e in j["events"] + j["mix_into"]:
                self.assertIn(e, tables["event"], f"{j['id']} -> {e}")
            if j["repoint"]:
                self.assertIn(j["repoint"], tables["sound"], j["id"])

    def test_loops_are_stereo_and_long(self):
        loops = [j for j in self.jobs if j["loop"]]
        self.assertGreaterEqual(len(loops), 4)
        for j in loops:
            self.assertEqual(j["channels"], 2)
            self.assertGreaterEqual(j["seconds"] - j["loop_xfade"], 20)

    def test_seed_is_stable_and_distinct(self):
        self.assertEqual(m.seed_for("a", 1), m.seed_for("a", 1))
        self.assertNotEqual(m.seed_for("a", 1), m.seed_for("a", 2))
        self.assertNotEqual(m.seed_for("a", 1), m.seed_for("b", 1))

    def test_window_and_estimate(self):
        j = {"seconds": 0.5}
        self.assertEqual(m.window_seconds(j, 1.0), m.MIN_WINDOW_SECONDS)
        self.assertEqual(m.latent_frames(2.0), 44)
        lo, hi = m.estimate_seconds(self.jobs, None, 100, 1.0)
        self.assertTrue(0 < lo < hi)
        lo2, _ = m.estimate_seconds(self.jobs, 1, 30, 1.0)
        self.assertLess(lo2, lo)


class AudioTests(unittest.TestCase):
    JOB = {"channels": 1, "rate": 44100, "seconds": 3.0, "peak_db": -2.0, "trim": True, "trim_db": 45, "fade_in_ms": 2, "fade_out_ms": 60}

    def test_trim_mono_peak(self):
        pcm, rate = m.postprocess(tone(), 44100, self.JOB)
        self.assertEqual(rate, 44100)
        self.assertEqual(pcm.shape[1], 1)
        secs = pcm.shape[0] / rate
        self.assertTrue(0.7 < secs < 1.2, secs)                      # ~1 s of sound, silence cut
        peak = np.abs(pcm).max() / 32767.0
        self.assertAlmostEqual(20 * np.log10(peak), -2.0, delta=0.05)
        self.assertEqual(int(pcm[-1, 0]), 0)                          # faded out

    def test_max_length_and_scale(self):
        job = {**self.JOB, "seconds": 0.4}
        pcm, rate = m.postprocess(tone(), 44100, job)
        self.assertLessEqual(pcm.shape[0], int(0.4 * rate))

    def test_resample_and_override(self):
        pcm, rate = m.postprocess(tone(), 44100, self.JOB, rate_override=22050)
        self.assertEqual(rate, 22050)
        self.assertTrue(0.7 < pcm.shape[0] / rate < 1.2)

    def test_rms_target_caps_by_peak(self):
        job = {**self.JOB, "rms_db": -40.0}
        pcm, _ = m.postprocess(tone(), 44100, job)
        rms = np.sqrt(np.mean((pcm.astype(np.float64) / 32767) ** 2))
        self.assertAlmostEqual(20 * np.log10(rms), -40.0, delta=0.3)

    def test_silence_rejected(self):
        with self.assertRaises(ValueError):
            m.postprocess(np.zeros((2, 44100), np.float32), 44100, self.JOB)

    def test_loop_is_seamless_and_stereo(self):
        rate = 44100
        rng = np.random.default_rng(1)
        noise = rng.standard_normal((2, rate * 6)).astype(np.float32) * 0.1
        job = {"channels": 2, "rate": rate, "seconds": 6.0, "loop": True, "loop_xfade": 1.0, "peak_db": -3.0, "rms_db": -26}
        pcm, r = m.postprocess(noise, rate, job)
        self.assertEqual(pcm.shape, (5 * rate, 2))
        # wrap-around step is no bigger than typical neighbouring-sample steps
        steps = np.abs(np.diff(pcm[:, 0].astype(np.int32)))
        wrap = abs(int(pcm[0, 0]) - int(pcm[-1, 0]))
        self.assertLess(wrap, np.percentile(steps, 99.9) + 1)

    def test_wav_roundtrip_and_determinism(self):
        pcm, rate = m.postprocess(tone(), 44100, self.JOB)
        with tempfile.TemporaryDirectory() as d:
            a, b = Path(d) / "a.wav", Path(d) / "b.wav"
            m.write_wav(a, pcm, rate)
            m.write_wav(b, pcm, rate)
            self.assertEqual(a.read_bytes(), b.read_bytes())
            back, r2 = m.read_wav(a)
            self.assertEqual(r2, rate)
            self.assertTrue(np.array_equal(back, pcm))


SOUNDS = """version = 1
[sound.old_1]
file = "sfx/old_1.wav"
[sound.ui_move]
file = "sfx/select_2.wav"
volume = 0.35
[event.fire_bullet]
sounds = ["old_1"]
volume = 0.8
[event.explosion_large]
sounds = ["old_1"]
[event.building_placed]
sounds = ["old_1"]
"""


def fake_jobs():
    base = {"category": "weapons", "volume": 1.0, "max_voices": 4, "planned": "", "repoint": "", "events": [], "mix_into": []}
    return [
        {**base, "id": "w_a", "file": "sfx/w_a.wav", "events": ["fire_bullet"]},
        {**base, "id": "w_b", "file": "sfx/w_b.wav", "events": ["fire_bullet"], "mix_into": ["building_placed"]},
        {**base, "id": "gui_x", "file": "sfx/gui_x.wav", "repoint": "ui_move"},
        {**base, "id": "w_missing", "file": "sfx/w_missing.wav", "events": ["fire_bullet"]},
    ]


class PickTests(unittest.TestCase):
    def test_update_only_present_idempotent(self):
        present = {"w_a", "w_b", "gui_x"}
        new, notes = m.update_sounds_toml(SOUNDS, fake_jobs(), present)
        d = tomllib.loads(new)
        self.assertEqual(d["event"]["fire_bullet"]["sounds"], ["old_1", "w_a", "w_b"])
        self.assertEqual(d["event"]["building_placed"]["sounds"], ["old_1"])
        self.assertEqual(d["sound"]["w_a"]["file"], "sfx/w_a.wav")
        self.assertNotIn("w_missing", d["sound"])
        self.assertEqual(d["sound"]["ui_move"]["file"], "sfx/gui_x.wav")
        self.assertEqual(d["sound"]["ui_move"]["volume"], 0.35)
        self.assertIn("was sfx/select_2.wav", new)
        again, _ = m.update_sounds_toml(new, fake_jobs(), present)
        self.assertEqual(new, again)

    def test_mix_and_replace(self):
        present = {"w_a", "w_b"}
        new, _ = m.update_sounds_toml(SOUNDS, fake_jobs(), present, mix=True, replace_sfxr=True)
        d = tomllib.loads(new)
        self.assertEqual(d["event"]["fire_bullet"]["sounds"], ["w_a", "w_b"])
        self.assertEqual(d["event"]["building_placed"]["sounds"], ["w_b"])
        self.assertEqual(d["event"]["explosion_large"]["sounds"], ["old_1"])   # untouched

    def test_removing_files_removes_references(self):
        new, _ = m.update_sounds_toml(SOUNDS, fake_jobs(), {"w_a", "w_b"})
        back, _ = m.update_sounds_toml(new, fake_jobs(), set())
        self.assertEqual(tomllib.loads(back)["event"]["fire_bullet"]["sounds"], ["old_1"])
        self.assertNotIn("w_a", tomllib.loads(back)["sound"])

    def test_run_pick_end_to_end(self):
        with tempfile.TemporaryDirectory() as d:
            d = Path(d)
            out, sfx = d / "out", d / "sfx"
            snd = d / "sounds.toml"
            snd.write_text(SOUNDS, encoding="utf-8")
            jobs_toml = d / "jobs.toml"
            jobs_toml.write_text(
                '[defaults]\nchannels=1\nseconds=2.0\nnegative="music"\ntakes=2\n'
                '[job.w_a]\nprompt="a long enough prompt for the test job a"\nevents=["fire_bullet"]\n'
                '[job.w_b]\nprompt="a long enough prompt for the test job b"\nevents=["fire_bullet"]\n', encoding="utf-8")
            pcm, rate = m.postprocess(tone(), 44100, AudioTests.JOB)
            m.write_wav(out / "w_a" / "take1.wav", pcm, rate)
            m.write_wav(out / "w_a" / "take2.wav", pcm[::-1], rate)
            m.write_wav(out / "w_b" / "draft1.wav", pcm, rate)
            (d / "picks.toml").write_text('[picks]\nw_a = "2"\nw_b = "d1"\n', encoding="utf-8")
            rc = m.main(["--jobs", str(jobs_toml), "--out", str(out), "--sfx-dir", str(sfx), "--sounds", str(snd),
                         "--pick", "--picks-file", str(d / "picks.toml")])
            self.assertEqual(rc, 0)
            self.assertEqual((sfx / "w_a.wav").read_bytes(), (out / "w_a" / "take2.wav").read_bytes())
            self.assertEqual((sfx / "w_b.wav").read_bytes(), (out / "w_b" / "draft1.wav").read_bytes())
            self.assertEqual(tomllib.loads(snd.read_text())["event"]["fire_bullet"]["sounds"], ["old_1", "w_a", "w_b"])
            # audition page
            self.assertEqual(m.main(["--jobs", str(jobs_toml), "--out", str(out), "--sfx-dir", str(sfx), "--audition"]), 0)
            page = (out / "index.html").read_text(encoding="utf-8")
            self.assertIn('src="w_a/take2.wav"', page)
            self.assertIn('value="d1"', page)
            self.assertIn("in game now", page)

    def test_bad_pick_rejected(self):
        with self.assertRaises(ValueError):
            m.parse_picks(["x=abc"], None)
        with self.assertRaises(ValueError):
            m.parse_picks(["nonsense"], None)


class RealFileTests(unittest.TestCase):
    def test_real_sounds_toml_survives_all_files_present(self):
        jobs = m.load_jobs()["jobs"]
        text = m.DEFAULT_SOUNDS.read_text(encoding="utf-8")
        new, notes = m.update_sounds_toml(text, jobs, {j["id"] for j in jobs}, mix=True)
        d = tomllib.loads(new)
        self.assertEqual(notes, [])
        for name, ev in d["event"].items():
            for s in ev["sounds"]:
                self.assertIn(s, d["sound"], f"{name} -> {s}")
        again, _ = m.update_sounds_toml(new, jobs, {j["id"] for j in jobs}, mix=True)
        self.assertEqual(new, again)


if __name__ == "__main__":
    unittest.main()
