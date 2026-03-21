#!/usr/bin/env python3
import argparse
import re
import subprocess
import time
import sys
from concurrent.futures import ThreadPoolExecutor, as_completed

def parse_usi_position_command(command_line):
    if not command_line.startswith("position sfen"):
        if "startpos" in command_line:
            command_line = command_line.replace("startpos", "sfen lnsgkgsnl/1r5b1/ppppppppp/9/9/9/PPPPPPPPP/1B5R1/LNSGKGSNL b - 1")
        else:
            raise ValueError("Not a USI position command: " + command_line)
    tmp = command_line[len("position sfen "):].strip()
    parts = tmp.split(" moves ")
    if len(parts) == 1:
        return parts[0].strip(), []
    return parts[0].strip(), parts[1].strip().split()

def wait_for_line(proc, pattern, timeout, label=""):
    regex = re.compile(pattern)
    start = time.time()
    while True:
        if time.time() - start > timeout:
            raise TimeoutError(f"[{label}] Timeout waiting for {pattern}")
        line = proc.stdout.readline()
        if not line:
            raise RuntimeError(f"[{label}] Engine terminated")
        line = line.rstrip("\n\r")
        if regex.search(line):
            return line

def analyze_one_position(engine_path, engine_cwd, sfen, moves, index,
                         move_nodes, threads_per_worker, hash_per_worker, extra_options):
    label = f"W{index}"
    proc = subprocess.Popen(
        [engine_path], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
        stderr=subprocess.DEVNULL, text=True, bufsize=1, cwd=engine_cwd)
    def send(cmd):
        proc.stdin.write(cmd + "\n")
        proc.stdin.flush()
    send("usi")
    wait_for_line(proc, r"usiok", timeout=30, label=label)

    # 追加オプションを先に設定（FV_SCALE等）
    for opt in extra_options:
        send(opt)

    # Threads/Hashは並列用の値で上書き
    send(f"setoption name Threads value {threads_per_worker}")
    send(f"setoption name USI_Hash value {hash_per_worker}")

    send("isready")
    wait_for_line(proc, r"readyok", timeout=120, label=label)
    send("usinewgame")
    if moves:
        send(f"position sfen {sfen} moves {' '.join(moves)}")
    else:
        send(f"position sfen {sfen}")
    send(f"go nodes {move_nodes}")
    bestmove = ""
    info_line = ""
    start = time.time()
    while True:
        if time.time() - start > 600:
            break
        line = proc.stdout.readline()
        if not line:
            break
        line = line.rstrip("\n\r")
        if line.startswith("info ") and " pv " in line:
            info_line = line
        elif line.startswith("bestmove"):
            parts = line.split()
            bestmove = parts[1] if len(parts) >= 2 else ""
            break
    try:
        proc.kill()
    except:
        pass
    return (index, bestmove, info_line)

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--cmd", required=True)
    parser.add_argument("--engine", required=True)
    parser.add_argument("--engine_cwd", default=None)
    parser.add_argument("--move_nodes", type=int, default=10000000)
    parser.add_argument("--workers", type=int, default=4)
    parser.add_argument("--threads_per_worker", type=int, default=4)
    parser.add_argument("--hash_per_worker", type=int, default=2048)
    parser.add_argument("--setoptions", default="",
                        help="Additional setoption commands separated by semicolons")
    args = parser.parse_args()

    # 追加オプションをパース
    extra_options = []
    if args.setoptions:
        for opt in args.setoptions.split(";"):
            opt = opt.strip()
            if opt:
                extra_options.append(opt)

    sfen_base, move_list = parse_usi_position_command(args.cmd)
    if not move_list:
        print("ERROR: No moves to analyze")
        return

    print(f"Analyzing {len(move_list)} moves with {args.workers} workers, "
          f"{args.threads_per_worker} threads/worker, "
          f"{args.hash_per_worker}MB hash/worker, "
          f"{args.move_nodes} nodes/move", file=sys.stderr, flush=True)
    if extra_options:
        print(f"Extra options: {extra_options}", file=sys.stderr, flush=True)

    tasks = [(i+1, move_list[:i+1]) for i in range(len(move_list))]
    results = []
    total = len(tasks)
    done = 0

    with ThreadPoolExecutor(max_workers=args.workers) as executor:
        fut_map = {}
        for (idx, pm) in tasks:
            fut = executor.submit(analyze_one_position,
                args.engine, args.engine_cwd, sfen_base, pm, idx,
                args.move_nodes, args.threads_per_worker, args.hash_per_worker,
                extra_options)
            fut_map[fut] = idx
        for fut in as_completed(fut_map):
            idx = fut_map[fut]
            try:
                result = fut.result()
                results.append(result)
            except Exception as e:
                results.append((idx, "error", f"info string error: {e}"))
            done += 1
            print(f"PROGRESS {done}/{total}", file=sys.stderr, flush=True)

    results.sort(key=lambda x: x[0])
    for (idx, bestmove, info_line) in results:
        move_usi = move_list[idx-1] if idx <= len(move_list) else "???"
        print(f"RESULT {idx} move={move_usi} bestmove={bestmove}")
        print(f"INFO {idx} {info_line}")

if __name__ == "__main__":
    main()
