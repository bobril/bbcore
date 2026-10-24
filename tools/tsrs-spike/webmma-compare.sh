#!/bin/zsh
# Compares bbcore baseline (project .bbrc) with tsrs mode on WebMma apps: transpile (validate: "tsrs"), bundles and
# typecheck diagnostics, plus timings. Nothing is written to the WebMma repository except gitignored build files:
# tsrs is enabled through a temporary .bbrc in BBRC_DIR (a directory above WebMma outside any repository), bundles go
# to OUT, the app tsconfig.json is restored and validate repro files are moved to OUT.
#
# usage: WEBMMA=<WebMma dir> BBRC_DIR=<dir above WebMma> ./webmma-compare.sh [runs] [app...]
# needs: dotnet build bb/bb.csproj -c Release, npm i in this directory
set -u
HERE=$(cd "$(dirname "$0")" && pwd)
BB=$HERE/../../bb/bin/Release/net10.0/bb.dll
RUNS=${1:-1}; shift 2>/dev/null
OUT=${OUT:-$HERE/out/webmma}
RBBRC=${BBRC_DIR:?set BBRC_DIR}/.bbrc
export TSRS_BINARY=${TSRS_BINARY:-$HERE/node_modules/@tsrs/darwin-arm64/tsrs}
[ -e $RBBRC ] && { echo "$RBBRC already exists, refusing to overwrite"; exit 1; }
trap 'rm -f $RBBRC' EXIT
apps=("$@"); [ ${#apps} -eq 0 ] && apps=($(cd ${WEBMMA:?set WEBMMA}/apps && for d in */; do [ -f $d/package.json ] && echo ${d%/}; done))
diags() { grep -E "\([0-9]+,[0-9]+\)|\([0-9]+\)$" $1 | sort }
for app in $apps; do
  A=$WEBMMA/apps/$app; O=$OUT/$app; mkdir -p $O
  [ -f $A/tsconfig.json ] && cp $A/tsconfig.json $O/tsconfig.json.orig
  hadBbcore=0; [ -d $A/.bbcore ] && hadBbcore=1
  cd $A
  echo '{ "validate": "tsrs" }' > $RBBRC
  dotnet $BB build -n --nocache -t no -d $O/dist-validate > $O/validate.log 2>&1
  rm -rf $O/repro; mkdir -p $O/repro
  [ -d $A/.bbcore/TsrsInput ] && mv $A/.bbcore/TsrsInput $A/.bbcore/TsrsWrong $O/repro/
  [ $hadBbcore = 0 ] && rmdir $A/.bbcore 2>/dev/null
  echo "$app transpile: $(grep -c 'ValidateTS output differs' $O/validate.log) of $(grep -oE 'emit done in [0-9.]+s for [0-9]+' $O/validate.log | awk '{print $NF}') files differ"
  for mode in baseline tsrs; do
    if [ $mode = tsrs ]; then echo '{ "tsrs": true }' > $RBBRC; else rm -f $RBBRC; fi
    for i in $(seq 1 $RUNS); do
      rm -rf $O/dist-$mode
      /usr/bin/time -l dotnet $BB build -n --nocache -t no -d $O/dist-$mode > $O/build-$mode.$i.log 2> $O/build-$mode.$i.time
      /usr/bin/time -l dotnet $BB build -n --nocache -t only > $O/check-$mode.$i.log 2> $O/check-$mode.$i.time
      echo "$app $mode run$i: build $(grep real $O/build-$mode.$i.time | awk '{print $1}')s rss=$(grep 'maximum resident' $O/build-$mode.$i.time | awk '{printf "%.0fMB",$1/1048576}') | $(grep -oE 'transpile emit done in [0-9.]+s' $O/build-$mode.$i.log) | $(grep -oE 'typecheck found [0-9]+ errors in [0-9.]+s' $O/check-$mode.$i.log)"
    done
  done
  rm -f $RBBRC $A/.bbcore-tsrs-typecheck.json
  [ -f $O/tsconfig.json.orig ] && cp $O/tsconfig.json.orig $A/tsconfig.json
  if [ -d $O/dist-baseline ] && [ -d $O/dist-tsrs ]; then
    echo "$app bundles: $(diff -rq $O/dist-baseline $O/dist-tsrs | wc -l | tr -d ' ') differing files"
  else
    echo "$app bundles: not written (build has errors in both modes)"
  fi
  echo "$app typecheck diagnostics identical: $([ "$(diags $O/check-baseline.1.log)" = "$(diags $O/check-tsrs.1.log)" ] && echo yes || echo NO)"
done
