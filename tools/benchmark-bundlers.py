#!/usr/bin/env python3
"""Compare two Release bb.dll builds against the same installed application sources.

Example (run from the bbcore checkout):
  python3 tools/benchmark-bundlers.py --baseline /tmp/baseline/bb.dll \
    --candidate /tmp/candidate/bb.dll --project /path/to/WebMma/apps/WebOnDemand \
    --output /tmp/bundler-results --runs 3

Dependency installation and type checking are disabled in timed builds. Run the
normal build/tests separately for validation. Each variant gets an untimed warmup;
measured builds alternate order and run sequentially to avoid CPU/cache contention.
Use --nocache to measure source processing as well as bundling, or --fast to compare
the development bundler. results.json includes timings, logs and per-file JS hashes,
raw byte sizes and gzip byte sizes (each file compressed separately, level 9).
"""

import argparse
import gzip
import hashlib
import json
import re
import statistics
import subprocess
import time
from pathlib import Path


def measure(binary, project, output, extra):
    output.mkdir(parents=True)
    command = ["dotnet", str(binary), "b", "-n", "-t", "no", "-d", str(output), *extra]
    started = time.perf_counter()
    with output.with_suffix(".log").open("w") as log:
        process = subprocess.run(command, cwd=project, stdout=log, stderr=subprocess.STDOUT)
    elapsed = time.perf_counter() - started
    text = output.with_suffix(".log").read_text()
    if process.returncode:
        raise RuntimeError(f"Build failed ({process.returncode}): {output.with_suffix('.log')}\n{text[-4000:]}")
    phases = {}
    for name, seconds in re.findall(r"Bundler phase (\w+) took ([\d.]+)s", text):
        phases[name] = round(phases.get(name, 0) + float(seconds), 3)
    files = {}
    for path in sorted(output.rglob("*.js")):
        content = path.read_bytes()
        files[str(path.relative_to(output))] = {
            "bytes": len(content),
            "gzip_bytes": len(gzip.compress(content, compresslevel=9, mtime=0)),
            "sha256": hashlib.sha256(content).hexdigest(),
        }
    if not files:
        raise RuntimeError(f"No JavaScript was generated in {output}")
    return {
        "command": command,
        "wall_seconds": round(elapsed, 3),
        "phases_seconds": phases,
        "build_summary": re.findall(r"Build done[^\n]+", text),
        "js_bytes": sum(f["bytes"] for f in files.values()),
        "gzip_bytes": sum(f["gzip_bytes"] for f in files.values()),
        "files": files,
        "log": str(output.with_suffix(".log")),
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--baseline", required=True, type=Path)
    parser.add_argument("--candidate", required=True, type=Path)
    parser.add_argument("--project", required=True, action="append", type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--runs", type=int, default=3)
    parser.add_argument("--nocache", action="store_true")
    parser.add_argument("--fast", action="store_true")
    parser.add_argument("--sourcemap", action="store_true")
    args = parser.parse_args()
    if args.runs < 1:
        parser.error("--runs must be positive")
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    if (output / "results.json").exists():
        parser.error("Choose a fresh output directory; results.json already exists")
    binaries = {name: getattr(args, name).resolve(strict=True) for name in ("baseline", "candidate")}
    extra = []
    if args.nocache:
        extra.append("--nocache")
    if args.fast:
        extra += ["-f", "1"]
    if args.sourcemap:
        extra += ["-g", "yes"]
    results = {"flags": extra, "projects": []}
    for index, project in enumerate(args.project):
        project = project.resolve(strict=True)
        record = {"project": str(project), "baseline": [], "candidate": []}
        results["projects"].append(record)
        for iteration in range(args.runs + 1):
            for name in (["baseline", "candidate"] if iteration % 2 == 0 else ["candidate", "baseline"]):
                label = "warmup" if iteration == 0 else str(iteration)
                run = measure(binaries[name], project, output / f"{index}-{project.name}-{name}-{label}", extra)
                print(f"{project.name} {name} {label}: {run['wall_seconds']}s; "
                      f"JS {run['js_bytes']} bytes; gzip {run['gzip_bytes']} bytes", flush=True)
                if iteration:
                    record[name].append(run)
                (output / "results.json").write_text(json.dumps(results, indent=2) + "\n")
        record["median_seconds"] = {
            name: statistics.median(r["wall_seconds"] for r in record[name]) for name in binaries
        }
        record["speedup_percent"] = round(100 * (1 - record["median_seconds"]["candidate"] /
                                                   record["median_seconds"]["baseline"]), 1)
        (output / "results.json").write_text(json.dumps(results, indent=2) + "\n")
        print(f"{project.name}: {record['speedup_percent']}% faster (median)", flush=True)


if __name__ == "__main__":
    main()
