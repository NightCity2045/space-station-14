#!/usr/bin/env python3
"""Launch an extracted G0 pair on loopback and verify sandboxed client join.

Uses no source resources. Run on the artifact's own OS/architecture with the
server's .NET 10 runtime installed. Preserves startup and shutdown logs.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import signal
import subprocess
import time
import zipfile
from spatial_protocol_manifest import validate_protocol_manifest


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def extract(archive, destination):
    with zipfile.ZipFile(archive) as source:
        for info in source.infolist():
            path = PurePosixPath(info.filename)
            if path.is_absolute() or '..' in path.parts or '\\' in info.filename or ':' in info.filename:
                raise ValueError(f'Invalid archive path: {info.filename}')
            target = Path(source.extract(info, destination))
            if os.name != 'nt' and not info.is_dir():
                target.chmod((info.external_attr >> 16) & 0o777 or 0o644)


def await_log(process, log, pattern, timeout):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if process.poll() is not None:
            raise RuntimeError(f'Process exited with {process.returncode}; inspect {log}')
        if pattern in log.read_text(encoding='utf-8', errors='replace'):
            return
        time.sleep(0.25)
    raise TimeoutError(f'No {pattern!r} within {timeout}s; inspect {log}')


def stop(process):
    if process is None or process.poll() is not None:
        return
    if os.name == 'nt':
        process.terminate()
    else:
        process.send_signal(signal.SIGINT)
    try:
        process.wait(timeout=15)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait(timeout=10)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--pair', required=True, type=Path, help='Directory produced by package_spatial_pair.py')
    parser.add_argument('--output', required=True, type=Path, help='New extraction/log directory')
    parser.add_argument('--port', type=int, default=1213)
    parser.add_argument('--timeout', type=int, default=180)
    args = parser.parse_args()
    if args.output.exists() or not 1 <= args.port <= 65535 or args.timeout < 1:
        parser.error('Use a new output directory, a valid port and positive timeout')
    pair = args.pair.resolve()
    output = args.output.resolve()
    manifest = json.loads((pair / 'pair-manifest.json').read_text())
    runtime = manifest['runtime']
    if runtime != ('win-x64' if os.name == 'nt' else 'linux-x64'):
        parser.error('Run the artifact on its matching host platform')
    server_zip = pair / f'SS14.Server_{runtime}.zip'
    client_zip = pair / f'SS14.Client.Standalone_{runtime}.zip'
    for path, expected in ((server_zip, manifest['server_archive_sha256']),
                           (client_zip, manifest['client_archive_sha256'])):
        if digest(path) != expected:
            raise RuntimeError(f'Artifact hash mismatch: {path}')
    with zipfile.ZipFile(client_zip) as archive:
        engine = json.loads(archive.read('engine-manifest.json'))
        embedded = json.loads(archive.read('pair-manifest.json'))
    validate_protocol_manifest(engine, manifest['spatial_protocol'])
    validate_protocol_manifest(manifest)
    if embedded != {key: value for key, value in manifest.items() if key != 'client_archive_sha256'}:
        raise RuntimeError('Embedded and external pair manifests disagree')
    if engine['engine_commit'] != manifest['engine_commit'] or engine['runtime'] != runtime or engine['source_dirty']:
        raise RuntimeError('Engine artifact does not match the clean fixed pair')
    output.mkdir(parents=True)
    extract(server_zip, output / 'server')
    extract(client_zip, output / 'client')
    server_log = output / 'server.log'
    client_log = output / 'client.log'
    server = client = None
    outcome = {'pair': manifest, 'scenario': 'Packed, lobby disabled',
               'joined': False, 'sandbox_verified': False}
    try:
        with server_log.open('w', encoding='utf-8') as server_stream, client_log.open('w', encoding='utf-8') as client_stream:
            server = subprocess.Popen([
                'dotnet', 'Robust.Server.dll', '--data-dir', str(output / 'server-data'),
                '--cvar', 'net.bindto=127.0.0.1', '--cvar', f'net.port={args.port}',
                '--cvar', f'status.bind=127.0.0.1:{args.port}', '--cvar', 'hub.advertise=false',
                '--cvar', 'game.lobbyenabled=false', '--cvar', 'game.map=Packed'
            ], cwd=output / 'server', stdout=server_stream, stderr=subprocess.STDOUT)
            await_log(server, server_log, '-> Ready', args.timeout)
            executable = 'Robust.Client.exe' if os.name == 'nt' else 'Robust.Client'
            client = subprocess.Popen([
                str(output / 'client' / executable), '--headless', '--self-contained',
                '--mount-zip', 'Content.Client.zip', '--username', 'SpatialQualification',
                '--connect', '--connect-address', f'127.0.0.1:{args.port}',
                '--cvar', 'display.vsync=false', '--cvar', 'display.max_fps=60',
                '--loglevel', 'res.mod=debug', '--loglevel', 'client=debug'
            ], cwd=output / 'client', stdout=client_stream, stderr=subprocess.STDOUT)
            await_log(client, client_log, 'Runlevel changed to: InGame', args.timeout)
            await_log(client, client_log, 'Attaching local player', args.timeout)
            text = client_log.read_text(encoding='utf-8', errors='replace')
            if 'ENABLING sandboxing' not in text or 'Verified assemblies' not in text:
                raise RuntimeError('Client joined without sandbox verification evidence')
            if server.poll() is not None:
                raise RuntimeError('Server exited during join')
            if '[ERRO]' in server_log.read_text(encoding='utf-8', errors='replace'):
                raise RuntimeError('Server logged an error before player attachment; inspect server.log')
            outcome.update(joined=True, sandbox_verified=True)
    finally:
        stop(client)
        stop(server)
        outcome['server_exit_code'] = server.returncode if server else None
        outcome['client_exit_code'] = client.returncode if client else None
        (output / 'result.json').write_text(json.dumps(outcome, indent=2) + '\n', encoding='utf-8')
    print('Extracted pair joined with content sandbox verification enabled')


if __name__ == '__main__':
    main()
