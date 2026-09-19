"""Offline tests for the structured progress output of `make threejs-asset-upscale`.

They cover the report itself — timestamps, phase tags, streamed child output, the log mirror, the
closing summary — plus the driver's failure and interrupt paths. Nothing here needs the Season-8
client archive or the Real-ESRGAN venv; the one test that drives the real converter is skipped
unless S4_CLIENT_ZIP is set.
"""
import contextlib
import io
import json
import os
import signal
import subprocess
import sys
import tempfile
import time
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SCRIPTS = ROOT / 'Tools/s4l-threejs-converter/scripts'
DRIVER = SCRIPTS / 'upscale-assets.py'
sys.path.insert(0, str(SCRIPTS))

import progress  # noqa: E402
from progress import Step  # noqa: E402

ARCHIVE: str = os.environ.get('S4_CLIENT_ZIP', '')
requires_archive = unittest.skipUnless(ARCHIVE, 'Set S4_CLIENT_ZIP to your unpacked Season-8 client ZIP')


def capture(callable_):
    """Run `callable_` with stdout captured, returning (result, output)."""
    buffer = io.StringIO()
    with contextlib.redirect_stdout(buffer):
        result = callable_()
    return result, buffer.getvalue()


class TorchPlanTests(unittest.TestCase):
    """`--print-plan` says which torch wheel a machine would install. The CUDA cases matter because
    pip's default is the CPU-only wheel on Windows and Linux: without this, a GPU laptop runs the 4x
    pass on one core and looks merely slow rather than misconfigured."""

    def plan(self, platform=None, cuda=None, *extra):
        environment = dict(os.environ)
        environment.pop('OPENS4L_PLATFORM', None)
        environment.pop('OPENS4L_CUDA', None)
        if platform is not None:
            environment['OPENS4L_PLATFORM'] = platform
        if cuda is not None:
            environment['OPENS4L_CUDA'] = cuda
        return subprocess.run([sys.executable, str(DRIVER), '--print-plan', *extra],
                              capture_output=True, text=True, env=environment)

    def test_macos_keeps_the_default_wheel_because_it_carries_mps(self):
        result = self.plan('darwin', '0')
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn('the default PyPI wheel', result.stdout)

    def test_windows_without_an_nvidia_gpu_keeps_the_default_cpu_wheel(self):
        result = self.plan('win32', '0')
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn('the default PyPI wheel', result.stdout)

    def test_windows_with_an_nvidia_gpu_installs_torch_from_the_cuda_index(self):
        result = self.plan('win32', '1')
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn('https://download.pytorch.org/whl/cu124', result.stdout)
        self.assertIn('--index-url', result.stdout)
        self.assertIn('torch torchvision', result.stdout)

    def test_an_older_driver_can_name_its_own_index(self):
        result = self.plan('win32', '1', '--torch-index-url', 'https://download.pytorch.org/whl/cu121')
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn('https://download.pytorch.org/whl/cu121', result.stdout)
        self.assertNotIn('cu124', result.stdout)

    def test_the_plan_needs_no_client_archive(self):
        result = self.plan('linux', '0')
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)


class FormatTests(unittest.TestCase):
    def test_elapsed_clock_and_durations_read_the_way_a_long_log_needs(self):
        self.assertEqual(progress.format_clock(0), '00:00:00')
        self.assertEqual(progress.format_clock(3725), '01:02:05')
        self.assertEqual(progress.format_duration(9), '9s')
        self.assertEqual(progress.format_duration(65), '1m05s')
        self.assertEqual(progress.format_duration(3725), '1h02m05s')
        self.assertEqual(progress.format_size(512), '512 B')
        self.assertEqual(progress.format_size(2048), '2.0 KB')


class StepTests(unittest.TestCase):
    def tearDown(self):
        progress.set_log(None)

    def test_every_phase_line_carries_its_tag_and_outcome(self):
        def run():
            Step(2, 6, 'Real-ESRGAN packages').note('downloading')
            Step(3, 6, 'Model weights').ok('weights ready')
        _, output = capture(run)
        self.assertIn('[2/6] Real-ESRGAN packages', output)
        self.assertIn('[2/6]   downloading', output)
        self.assertIn('[3/6]   ok · weights ready · 0s', output)

    def test_a_failure_and_a_skip_both_say_so_and_keep_the_hint(self):
        def run():
            Step(1, 6, 'Source archive').failed(1, 'not found: x.zip')
            Step(5, 6, 'Wardrobe conversion').skipped('index already records 1x,2x,4x,8x')
        _, output = capture(run)
        self.assertIn('FAILED (exit 1)', output)
        self.assertIn('hint: not found: x.zip', output)
        self.assertIn('skipped · index already records', output)

    def test_streamed_child_output_is_tagged_and_stderr_is_kept_in_order(self):
        child = (f'import sys; sys.path.insert(0, {str(SCRIPTS)!r}); import progress; '
                 'progress.emit("from the child"); print("plain stdout"); print("to stderr", file=sys.stderr)')

        def run():
            step = Step(6, 6, 'Real-ESRGAN colour/alpha levels')
            return step.stream([sys.executable, '-c', child])
        code, output = capture(run)
        self.assertEqual(code, 0)
        lines = output.splitlines()
        # The child runs with OPENS4L_PROGRESS_PLAIN, so it carries the outer clock exactly once.
        self.assertRegex(output, r'(?m)^\[\d\d:\d\d:\d\d\] \[6/6\]     from the child$')
        for line in lines:
            if 'from the child' in line:
                self.assertEqual(line.count('['), 2, line)
        self.assertRegex(output, r'(?m)^\[\d\d:\d\d:\d\d\] \[6/6\]     plain stdout$')
        self.assertRegex(output, r'(?m)^\[\d\d:\d\d:\d\d\] \[6/6\]     to stderr$')

    def test_a_failing_command_reports_its_exit_code(self):
        code, _ = capture(lambda: Step(3, 6, 'pip').stream([sys.executable, '-c', 'raise SystemExit(7)']))
        self.assertEqual(code, 7)

    def test_a_missing_command_is_reported_instead_of_raising(self):
        code, output = capture(lambda: Step(4, 6, 'weights').stream(['s4l-command-that-does-not-exist']))
        self.assertEqual(code, 127)
        self.assertIn('[4/6]', output)

    def test_the_summary_aligns_an_interrupted_phase_with_the_rest(self):
        def run():
            steps = [Step(1, 6, 'Source archive'), Step(2, 6, 'Real-ESRGAN python environment')]
            steps[0].ok('2.1 GB')
            steps[1].interrupted('stopped by the user')
            progress.summary('interrupted in phase 2/6 after 3s',
                             [(s.label, s.status, progress.format_duration(s.duration)) for s in steps], '3s')
        _, output = capture(run)
        self.assertIn('interrupted in phase 2/6 after 3s', output)
        self.assertIn('interrupted', output)
        self.assertIn('total', output)

    def test_the_log_mirrors_every_line_including_child_output(self):
        with tempfile.TemporaryDirectory() as directory:
            log = Path(directory) / 'nested' / 'run.log'
            progress.set_log(log)

            def run():
                step = Step(1, 1, 'phase')
                step.stream([sys.executable, '-c', 'print("child line")'])
                step.ok('done')
            capture(run)
            progress.close()
            # The mirror is UTF-8 whatever the parent's locale is, so a locale read would mangle '·'.
            text = log.read_text(encoding='utf-8')
        self.assertIn('child line', text)
        self.assertIn('ok · done', text)
        for line in text.splitlines():
            if line.strip():
                self.assertRegex(line, r'^\[\d\d:\d\d:\d\d\] ')


class ConsoleEncodingTests(unittest.TestCase):
    """The report draws box glyphs, and it has to survive every stream it is pointed at.

    A Windows console is cp1252/cp437, and a Python child's stdout is a pipe that takes the locale
    codec there, so printing a rule could raise UnicodeEncodeError and end an hours-long run on its
    own banner. A glyph the stream cannot encode is transliterated instead, and a stream that speaks
    UTF-8 (which is what `dotnet` writes into the pipe) has its output decoded as UTF-8 rather than
    through the parent's locale, which used to turn `·` into `Â·` in the log.
    """

    def tearDown(self):
        progress.set_log(None)

    def test_a_stream_that_cannot_encode_the_rule_transliterates_it(self):
        stream = io.TextIOWrapper(io.BytesIO(), encoding='cp1252')
        with contextlib.redirect_stdout(stream):
            progress.header('Real-ESRGAN colour/alpha levels', [('bundle', 'wardrobe')])
            progress.emit('levels 1x,4x → colour/alpha · x4')
            stream.flush()
            written = stream.detach().getvalue().decode('cp1252')
        self.assertIn('-' * 72, written)
        self.assertIn('colour/alpha', written)
        self.assertIn('->', written)

    def test_a_python_child_on_a_cp1252_pipe_reports_its_banner_instead_of_dying(self):
        # The exact failure: the Real-ESRGAN venv runs Python 3.12, whose piped stdout is cp1252.
        child = (f'import sys; sys.path.insert(0, {str(SCRIPTS)!r}); import progress; '
                 'progress.header("Real-ESRGAN colour/alpha levels", [("bundle", "wardrobe")])')
        environment = dict(os.environ, PYTHONIOENCODING='cp1252')
        result = subprocess.run([sys.executable, '-c', child], capture_output=True, text=True, env=environment)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn('-' * 72, result.stdout)

    def test_streamed_child_utf8_is_not_read_through_the_parent_locale(self):
        child = 'print("textures 1 decoded · scenes 0 exported")'
        with tempfile.TemporaryDirectory() as directory:
            log = Path(directory) / 'run.log'
            progress.set_log(log)

            def run():
                return Step(5, 6, 'Wardrobe conversion').stream([sys.executable, '-c', child])
            code, _ = capture(run)
            progress.close()
            # The mirror is written as UTF-8 whatever the parent's locale is (`set_log` opens it that
            # way), so it is read back the same way.
            text = log.read_text(encoding='utf-8')
        self.assertEqual(code, 0)
        self.assertIn('textures 1 decoded · scenes 0 exported', text)


class DriverTests(unittest.TestCase):
    def run_driver(self, *arguments, timeout=180):
        return subprocess.run([sys.executable, str(DRIVER), *arguments],
                              capture_output=True, text=True, timeout=timeout)

    def test_help_lists_the_log_option(self):
        result = self.run_driver('--help')
        self.assertEqual(result.returncode, 0)
        self.assertIn('--log', result.stdout)

    def test_missing_source_fails_in_phase_one_with_a_summary_and_a_log(self):
        with tempfile.TemporaryDirectory() as directory:
            result = self.run_driver('--source', str(Path(directory) / 'absent.zip'),
                                     '--assets', directory, '--cache', directory)
            self.assertEqual(result.returncode, 1)
            self.assertIn('[1/6] Source archive', result.stdout)
            self.assertIn('not found:', result.stdout)
            self.assertIn('stopped in phase 1/6', result.stdout)
            self.assertIn('FAILED (exit 1)', result.stdout)
            logs = list(Path(directory, 'logs').glob('upscale-*.log'))
            self.assertEqual(len(logs), 1)
            self.assertIn('FAILED (exit 1)', logs[0].read_text(encoding='utf-8'))
            # Every line the driver prints is timestamped, so a captured log can be read back.
            for line in result.stdout.splitlines():
                if line.strip():
                    self.assertRegex(line, r'^\[\d\d:\d\d:\d\d\] ')

    def test_an_explicit_log_path_is_used(self):
        with tempfile.TemporaryDirectory() as directory:
            log = Path(directory) / 'run.log'
            result = self.run_driver('--source', str(Path(directory) / 'absent.zip'),
                                     '--assets', directory, '--cache', directory, '--log', str(log))
            self.assertEqual(result.returncode, 1)
            self.assertTrue(log.is_file())
            self.assertIn('stopped in phase 1/6', log.read_text(encoding='utf-8'))

    @requires_archive
    def test_an_interrupted_phase_summary_ends_with_the_resume_hint(self):
        with tempfile.TemporaryDirectory() as directory:
            assets = Path(directory) / 'assets'
            assets.mkdir()
            (assets / 'index.json').write_text('{"coverage": {"textureQuality": {"requested": ["1x"]}}, "textures": {}}')
            process = subprocess.Popen([sys.executable, str(DRIVER), '--source', ARCHIVE,
                                        '--assets', str(assets), '--cache', str(Path(directory) / 'cache')],
                                       stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
            time.sleep(5)
            process.send_signal(signal.SIGINT)
            output, _ = process.communicate(timeout=120)
            self.assertEqual(process.returncode, 130)
            self.assertIn('Interrupted after', output)
            # Whichever phase the interrupt landed in, it is named and the table still prints.
            self.assertRegex(output, r'interrupted in phase \d/6 after')
            self.assertIn('interrupted · stopped by the user', output)
            self.assertRegex(output, r'(?m)^\[\d\d:\d\d:\d\d\]   phase +status +time$')
            self.assertIn('rerun `make threejs-asset-upscale` to resume', output)
            logs = list(Path(directory, 'cache', 'logs').glob('upscale-*.log'))
            self.assertEqual(len(logs), 1)
            self.assertRegex(logs[0].read_text(encoding='utf-8'), r'interrupted in phase \d/6 after')


class PruneTests(unittest.TestCase):
    """`prune-texture-levels.py` must take the entries and the files, never one without the other."""

    def build(self, directory):
        root = Path(directory)
        (root / 'textures').mkdir(parents=True)
        for level in ('1x', '4x', '8x'):
            (root / 'textures' / f'a.{level}.png').write_bytes(b'png-' + level.encode())
        (root / 'textures' / 'orphan.8x.png').write_bytes(b'orphan, left by an interrupted run')
        (root / 'index.json').write_text(json.dumps({
            'coverage': {'textureQuality': {'requested': ['1x', '4x', '8x']}},
            'textures': {'hair.dds': {'kind': 'color', 'variants': {
                '1x': {'file': 'textures/a.1x.png', 'width': 4, 'height': 4},
                '4x': {'file': 'textures/a.4x.png', 'width': 16, 'height': 16},
                '8x': {'file': 'textures/a.8x.png', 'width': 32, 'height': 32}}}}},))
        (root / 'coverage.json').write_text(json.dumps({'textureQuality': {'requested': ['1x', '4x', '8x']}}))
        return root

    def run_prune(self, root, *arguments):
        return subprocess.run([sys.executable, str(SCRIPTS / 'prune-texture-levels.py'), str(root), *arguments],
                              capture_output=True, text=True)

    def test_removing_a_level_takes_its_entries_files_and_orphans(self):
        with tempfile.TemporaryDirectory() as directory:
            root = self.build(directory)
            result = self.run_prune(root, '--levels', '8x')
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            index = json.loads((root / 'index.json').read_text())
            self.assertEqual(sorted(index['textures']['hair.dds']['variants']), ['1x', '4x'])
            self.assertEqual(index['coverage']['textureQuality']['requested'], ['1x', '4x'])
            self.assertIn('8x', index['coverage']['textureQuality']['pruned'])
            self.assertEqual(json.loads((root / 'coverage.json').read_text())['textureQuality']['requested'], ['1x', '4x'])
            self.assertFalse((root / 'textures' / 'a.8x.png').exists())
            self.assertFalse((root / 'textures' / 'orphan.8x.png').exists(), 'An unreferenced file of that level goes too')
            self.assertTrue((root / 'textures' / 'a.1x.png').exists())
            self.assertTrue((root / 'textures' / 'a.4x.png').exists())
            self.assertIn('2 level file(s)', result.stdout)

    def test_a_dry_run_changes_nothing(self):
        with tempfile.TemporaryDirectory() as directory:
            root = self.build(directory)
            before = (root / 'index.json').read_text()
            result = self.run_prune(root, '--levels', '8x', '--dry-run')
            self.assertEqual(result.returncode, 0)
            self.assertTrue((root / 'textures' / 'a.8x.png').exists())
            self.assertEqual((root / 'index.json').read_text(), before)
            self.assertIn('dry run', result.stdout)

    def test_the_decoded_original_can_never_be_pruned(self):
        with tempfile.TemporaryDirectory() as directory:
            root = self.build(directory)
            result = self.run_prune(root, '--levels', '1x')
            self.assertNotEqual(result.returncode, 0)
            self.assertIn('Refusing to remove 1x', result.stdout + result.stderr)
            self.assertTrue((root / 'textures' / 'a.1x.png').exists())

    def test_an_unknown_level_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = self.build(directory)
            result = self.run_prune(root, '--levels', '16x')
            self.assertNotEqual(result.returncode, 0)
            self.assertIn('Unknown level', result.stdout + result.stderr)


    def test_unreferenced_files_are_swept_without_touching_referenced_ones(self):
        with tempfile.TemporaryDirectory() as directory:
            root = self.build(directory)
            # The old naming convention: a texture file no variant names any more.
            (root / 'textures' / 'legacy.png').write_bytes(b'old name')
            result = self.run_prune(root, '--unreferenced')
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertFalse((root / 'textures' / 'legacy.png').exists())
            self.assertTrue((root / 'textures' / 'a.1x.png').exists())
            self.assertTrue((root / 'textures' / 'a.4x.png').exists())
            self.assertIn('levels removed   none', result.stdout)
            self.assertIn('2 unreferenced file(s) deleted', result.stdout)

    def test_a_run_with_nothing_to_do_says_so(self):
        with tempfile.TemporaryDirectory() as directory:
            root = self.build(directory)
            result = self.run_prune(root)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn('Nothing to do', result.stdout + result.stderr)


if __name__ == '__main__':
    unittest.main()
