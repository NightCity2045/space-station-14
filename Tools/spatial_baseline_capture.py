#!/usr/bin/env python3
"""Capture Linux G0 process memory and loopback server Prometheus evidence.

Run after startup/warmup, with no build/test jobs competing for CPU. The report
does not certify target hardware, client FPS, P1-P6, or unimplemented spatial work.
"""
import argparse
import json
import math
from pathlib import Path
import re
import time
import urllib.request


def memory(pid):
    proc = Path('/proc') / str(pid)
    status = (proc / 'status').read_text()
    fields = {name: int(value) * 1024 for name, value in
              re.findall(r'^(VmRSS|VmHWM):\s+(\d+) kB$', status, re.MULTILINE)}
    # /proc/PID/stat's comm can contain spaces and parentheses.
    fields['start_ticks'] = int((proc / 'stat').read_text().rsplit(')', 1)[1].split()[19])
    return fields


def values(prometheus):
    result = {}
    for line in prometheus.splitlines():
        if not line or line.startswith('#'):
            continue
        name, raw = line.rsplit(' ', 1)
        result[name] = float(raw)
    return result


def percentile(samples, probability):
    return sorted(samples)[max(0, math.ceil(len(samples) * probability) - 1)]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--server-pid', required=True, type=int)
    parser.add_argument('--client-pid', type=int)
    parser.add_argument('--metrics-port', type=int, default=44880)
    parser.add_argument('--seconds', type=int, default=120)
    parser.add_argument('--pair-manifest', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    if args.seconds < 2 or not 1 <= args.metrics_port <= 65535:
        parser.error('Capture requires at least 2 seconds and a valid metrics port')
    if args.output.exists():
        parser.error('Output directory exists; preserve the previous capture')
    pair = json.loads(args.pair_manifest.read_text())
    args.output.mkdir(parents=True)
    processes = {'server': args.server_pid}
    if args.client_pid is not None:
        processes['client'] = args.client_pid
    identities = {name: memory(pid)['start_ticks'] for name, pid in processes.items()}
    # The endpoint is deliberately loopback-only, not an arbitrary remote URL.
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
    url = f'http://127.0.0.1:{args.metrics_port}/metrics'
    started = time.monotonic()
    samples = []
    first = last = {}
    with (args.output / 'samples.jsonl').open('x', encoding='utf-8') as stream:
        for index in range(args.seconds + 1):
            with opener.open(url, timeout=10) as response:
                text = response.read().decode('utf-8')
            current = values(text)
            required = {'robust_entities_count', 'robust_net_sent_bytes', 'robust_net_recv_bytes',
                        'robust_game_loop_frametime_count'}
            if missing := required - current.keys():
                raise RuntimeError(f'Required metrics are missing: {sorted(missing)}')
            if index == 0:
                first = current
                (args.output / 'metrics-start.prom').write_text(text)
            last = current
            mem = {name: memory(pid) for name, pid in processes.items()}
            if any(mem[name]['start_ticks'] != identity for name, identity in identities.items()):
                raise RuntimeError('A sampled process was replaced; reject this capture')
            sample = {'elapsed_seconds': time.monotonic() - started, 'memory': mem,
                      'entities': current.get('robust_entities_count')}
            samples.append(sample)
            stream.write(json.dumps(sample) + '\n')
            stream.flush()
            if index != args.seconds:
                time.sleep(max(0, started + index + 1 - time.monotonic()))
    (args.output / 'metrics-end.prom').write_text(text)
    elapsed = samples[-1]['elapsed_seconds'] - samples[0]['elapsed_seconds']
    counters = ('robust_net_sent_bytes', 'robust_net_recv_bytes', 'robust_net_dropped',
                'robust_net_resent_delay', 'robust_net_resent_hole', 'robust_game_loop_frametime_count')
    deltas = {name: last[name] - first[name] for name in counters if name in first and name in last}
    if any(value < 0 for value in deltas.values()):
        raise RuntimeError('Metrics reset during capture; reject this capture')
    buckets = []
    for name, value in last.items():
        match = re.fullmatch(r'robust_game_loop_frametime_bucket\{le="([^"]+)"\}', name)
        if match and name in first:
            buckets.append((float(match[1]), value - first[name]))
    buckets.sort()
    if not buckets or any(value < 0 for _, value in buckets):
        raise RuntimeError('Tick histogram is missing or reset; reject this capture')
    quantiles = {}
    count = deltas.get('robust_game_loop_frametime_count', 0)
    if count <= 0:
        raise RuntimeError('No measured ticks; enable game loop metrics and reject this capture')
    if count > 0:
        for probability in (0.95, 0.99):
            bound = next((bound for bound, total in buckets if total >= count * probability), math.inf)
            quantiles[str(probability)] = bound * 1000 if math.isfinite(bound) else None
    report = {
        'purpose': 'G0 baseline evidence, not spatial or target-hardware acceptance',
        'pair': pair,
        'duration_seconds': elapsed,
        'samples': len(samples),
        'memory_bytes': {name: {
            'steady_rss_p95': percentile([sample['memory'][name]['VmRSS'] for sample in samples], 0.95),
            'steady_rss_peak': max(sample['memory'][name]['VmRSS'] for sample in samples),
            'process_lifetime_rss_peak': samples[-1]['memory'][name]['VmHWM']
        } for name in processes},
        'counter_deltas': deltas,
        'sent_bytes_per_second': deltas['robust_net_sent_bytes'] / elapsed,
        'received_bytes_per_second': deltas['robust_net_recv_bytes'] / elapsed,
        'tick_histogram_upper_bounds_ms': quantiles,
        'tick_histogram_note': 'Upper bucket bounds, not exact quantiles. Despite its help text saying ms, GameLoop.NewTimer records seconds. Null means the percentile exceeds the finite buckets.',
        'entities_min': min(sample['entities'] for sample in samples if sample['entities'] is not None),
        'entities_max': max(sample['entities'] for sample in samples if sample['entities'] is not None)
    }
    (args.output / 'report.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
