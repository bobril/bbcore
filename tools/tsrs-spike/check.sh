#!/bin/bash
# usage: WEBMMA=<dir> check.sh <app> <runs> [config]  — one-shot typecheck with tsrs, tsgo (TS7 pinned oracle), Homebrew tsgo
S=$(cd "$(dirname "$0")" && pwd)
APP=${WEBMMA:?set WEBMMA to the WebMma directory}/apps/$1
OUT=$S/out/$1
RUNS=${2:-3}
mkdir -p $OUT
TSRS=$S/node_modules/@tsrs/darwin-arm64/tsrs
TS7=$S/node_modules/@typescript/typescript-darwin-arm64/lib/tsc
BREW=${TSGO:-$(command -v tsgo)}
cd $APP
for name in tsrs ts7 brew; do
  case $name in tsrs) BIN=$TSRS;; ts7) BIN=$TS7;; brew) BIN=$BREW;; esac
  for i in $(seq 1 $RUNS); do
    /usr/bin/time -l $BIN -p ${3:-tsconfig.json} --noEmit --pretty false > $OUT/$name.$i.txt 2> $OUT/$name.$i.time
    echo "$1 $name run$i exit=$? $(grep -E 'real' $OUT/$name.$i.time | awk '{print $1"s"}') rss=$(grep 'maximum resident' $OUT/$name.$i.time | awk '{printf "%.0fMB", $1/1048576}') diags=$(grep -c ' error TS' $OUT/$name.$i.txt)"
  done
  sort $OUT/$name.1.txt > $OUT/$name.sorted.txt
done
echo "diff tsrs vs ts7 (sorted): $(diff $OUT/tsrs.sorted.txt $OUT/ts7.sorted.txt | grep -c '^[<>]') lines"
echo "diff tsrs vs ts7 (order):  $(diff $OUT/tsrs.1.txt $OUT/ts7.1.txt | grep -c '^[<>]') lines"
echo "diff ts7 vs brew (sorted): $(diff $OUT/ts7.sorted.txt $OUT/brew.sorted.txt | grep -c '^[<>]') lines"
