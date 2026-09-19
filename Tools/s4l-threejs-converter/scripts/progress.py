"""Structured console output for the long-running Three.js asset pipeline.

Plain text, deliberately without colour codes: the output of `make threejs-asset-upscale` is read
both on a terminal and in a captured log, and a phase that takes an hour must be legible in both.

Every line carries the elapsed time since the run started, and every line inside a phase carries
that phase's `[n/N]` tag — including the output streamed from the command a phase runs, which is
echoed line by line through the same channel. A phase announces its label, streams whatever its
command prints, then reports its outcome and duration on one aligned line. Everything is mirrored
into a log file so an hours-long run survives a closed terminal or a limited scrollback.
"""

from __future__ import annotations

import os
import signal
import subprocess
import sys
import time
from pathlib import Path

RULE = '─' * 72
START = time.monotonic()
_log = None

# A Windows console is often cp1252/cp437, and a child's piped stdout takes that same locale codec
# there, so the glyphs below raise UnicodeEncodeError and kill an hours-long run on its own banner.
# When the stream at hand cannot encode a line it is transliterated instead: the same run stays
# legible on a wedged console rather than dying on a box-drawing character. The log mirror is written
# as UTF-8 regardless, so a line the console cannot take is still recorded in full.
ASCII_FALLBACK = str.maketrans({'─': '-', '·': '|', '×': 'x', '…': '...', '→': '->', '—': '-', '✗': 'x'})
_stream = None           # the stream the cached encoding was resolved from
_encoding = None

def _stream_encoding() -> str | None:
    """The encoding of the stream currently standing in for stdout, or None when it takes any str."""
    global _stream, _encoding
    stream = sys.stdout
    if stream is not _stream:
        _stream = stream
        _encoding = getattr(stream, 'encoding', None)
    return _encoding


def _safe(text: str) -> str:
    # Whether a glyph survives is a property of the stream, not of `isatty()`: a pipe to a cp1252
    # reader (a child this script spawned on Windows) rejects exactly what a cp1252 console rejects,
    # and a StringIO takes anything. So the line is tried against the stream's own codec.
    encoding = _stream_encoding()
    if not encoding:
        return text
    try:
        text.encode(encoding)
        return text
    except UnicodeEncodeError:
        pass
    except LookupError:
        return text                       # an unknown codec name: nothing to transliterate against
    translated = text.translate(ASCII_FALLBACK)
    try:
        translated.encode(encoding)
        return translated
    except UnicodeEncodeError:
        return translated.encode(encoding, 'replace').decode(encoding, 'replace')


def format_clock(seconds: float) -> str:
    """Elapsed time as `HH:MM:SS` — the prefix on every line."""
    seconds = max(0, int(seconds))
    hours, remainder = divmod(seconds, 3600)
    minutes, seconds = divmod(remainder, 60)
    return f'{hours:02d}:{minutes:02d}:{seconds:02d}'


def format_duration(seconds: float) -> str:
    seconds = max(0, int(seconds))
    hours, remainder = divmod(seconds, 3600)
    minutes, seconds = divmod(remainder, 60)
    if hours:
        return f'{hours}h{minutes:02d}m{seconds:02d}s'
    if minutes:
        return f'{minutes}m{seconds:02d}s'
    return f'{seconds}s'


def format_size(size: int) -> str:
    value = float(size)
    for unit in ('B', 'KB', 'MB', 'GB'):
        if value < 1024 or unit == 'GB':
            return f'{value:.0f} {unit}' if unit == 'B' else f'{value:.1f} {unit}'
        value /= 1024
    raise AssertionError('unreachable')


def elapsed() -> str:
    """The run's elapsed clock, for callers that build their own lines."""
    return format_clock(time.monotonic() - START)


def now() -> str:
    """Wall-clock time of day, for the run banner."""
    return time.strftime('%Y-%m-%d %H:%M:%S')


def set_log(path: Path | None) -> Path | None:
    """Mirror every emitted line into `path` (the previous log, if any, is closed first)."""
    global _log
    if _log is not None:
        _log.close()
        _log = None
    if path is None:
        return None
    path.parent.mkdir(parents=True, exist_ok=True)
    _log = path.open('w', encoding='utf-8')
    return path


def emit(text: str = '') -> None:
    """Print one line, timestamped, and write the same line to the log when one is open.

    A process that is streaming its output into another one (see `Step.stream`) sets
    `OPENS4L_PROGRESS_PLAIN`: the outer process already timestamps and tags every line, so the
    inner one stays plain instead of carrying two elapsed clocks.
    """
    prefix = '' if os.environ.get('OPENS4L_PROGRESS_PLAIN') else f'[{elapsed()}] '
    line = f'{prefix}{text}' if text else ''
    try:
        print(_safe(line), flush=True)
    except UnicodeEncodeError:
        # Last resort: a stream rejected a glyph the check above let through. No character is worth
        # ending an hours-long run over, so the line goes out ASCII-only and the log keeps the rest.
        print(line.translate(ASCII_FALLBACK).encode('ascii', 'replace').decode('ascii'), flush=True)
    if _log is not None:
        _log.write(line + '\n')
        _log.flush()


def header(title: str, fields: list[tuple[str, str]]) -> None:
    """The run banner: what is about to happen and with which inputs."""
    emit(title)
    emit(RULE)
    width = max((len(name) for name, _ in fields), default=0)
    for name, value in fields:
        emit(f'  {name:<{width}}  {value}')
    emit(RULE)


def stop(process: subprocess.Popen, signal_number: int, stream) -> None:
    """Stop a streamed command and its children, then close its pipe."""
    try:
        if os.name != 'nt':
            os.killpg(os.getpgid(process.pid), signal_number)
        else:
            process.terminate()
        process.wait(timeout=10)
    except (ProcessLookupError, PermissionError):
        pass
    except subprocess.TimeoutExpired:
        if os.name != 'nt':
            os.killpg(os.getpgid(process.pid), signal.SIGKILL)
        else:
            process.kill()
        process.wait(timeout=10)
    stream.close()


class Step:
    """One numbered phase of the pipeline.

    Lines that belong to the phase (its notes, its outcome and the output streamed from its own
    command) are tagged `[index/total]`, so a long log can be read back per phase.
    """

    def __init__(self, index: int, total: int, label: str):
        self.index = index
        self.total = total
        self.label = label
        self.tag = f'{index}/{total}'
        self.start = time.monotonic()
        self.duration = 0.0
        self.status = 'pending'
        self.lines = 0    # output lines streamed from the phase's own command
        emit('')
        emit(f'[{self.tag}] {label}')

    def note(self, text: str) -> None:
        """A detail line inside a phase: a command, a download, a decision."""
        emit(f'[{self.tag}]   {text}')

    def stream(self, command: list[object], **kwargs) -> int:
        """Run the phase's command, echoing its output line by line under this phase's tag.

        The child's stderr is merged into stdout so ordering is preserved, and every line is
        flushed as it arrives: a phase that runs for hours must not look stuck.
        """
        parts = [str(part) for part in command]
        emit(f'[{self.tag}]   $ {" ".join(parts)}')
        # Both ends of the pipe are pinned to UTF-8. On Windows a Python child would otherwise encode
        # its stdout — a pipe, so there is no console to adapt to — with the locale codec (cp1252),
        # where the report's box glyphs raise UnicodeEncodeError and end the run; and reading that
        # pipe through the parent's locale turned `dotnet`'s UTF-8 output into `Â·` in the log.
        environment = dict(os.environ, OPENS4L_PROGRESS_PLAIN='1', PYTHONIOENCODING='utf-8')
        # Its own session: `dotnet run` and the ESRGAN venv both spawn children, and an interrupt
        # must stop the whole tree rather than leaving an orphan writing into the asset directory.
        isolated = os.name != 'nt'
        try:
            process = subprocess.Popen(parts, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                       text=True, encoding='utf-8', errors='replace', bufsize=1,
                                       env=environment, start_new_session=isolated, **kwargs)
        except OSError as error:
            emit(f'[{self.tag}]   {error}')
            return 127
        stream = process.stdout
        if stream is None:
            return process.wait()
        try:
            for line in stream:
                self.lines += 1
                text = line.rstrip().replace('\t', ' ')
                # A blank line in the child's output stays blank: an empty `[n/N]` line is noise.
                emit(f'[{self.tag}]     {text}' if text else '')
            code = process.wait()
            stream.close()
            return code
        except KeyboardInterrupt:
            stop(process, signal.SIGTERM, stream)
            raise

    def finish(self, status: str, detail: str = '') -> None:
        self.duration = time.monotonic() - self.start
        self.status = status
        parts = [status, *([detail] if detail else []), format_duration(self.duration)]
        emit(f'[{self.tag}]   ' + ' · '.join(parts))

    def ok(self, detail: str = '') -> None:
        self.finish('ok', detail)

    def skipped(self, detail: str = '') -> None:
        self.finish('skipped', detail)

    def failed(self, code: int, hint: str = '') -> None:
        self.finish(f'FAILED (exit {code})')
        if hint:
            emit(f'[{self.tag}]   hint: {hint}')

    def interrupted(self, detail: str = '') -> None:
        self.finish('interrupted', detail)


def summary(title: str, rows: list[tuple[str, str, str]], total: str) -> None:
    """The closing table: one row per phase, then the wall-clock total."""
    emit('')
    emit(RULE)
    emit(title)
    label_width = max((len(label) for label, _, _ in rows), default=0)
    status_width = max((len(status) for _, status, _ in rows), default=0)
    emit(f'  {"phase":<{label_width}}  {"status":<{status_width}}  {"time":>9}')
    for label, status, duration in rows:
        emit(f'  {label:<{label_width}}  {status:<{status_width}}  {duration:>9}')
    emit(f'  {"total":<{label_width}}  {"":<{status_width}}  {total:>9}')
    emit(RULE)


def close() -> None:
    """Flush and close the log, if one is open."""
    if _log is not None:
        _log.flush()
        _log.close()
