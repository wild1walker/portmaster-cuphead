#!/bin/bash
# build_pylibs.sh <out dir> [cache dir]: the Python packages the on-device setup needs on top of
# PortMaster's python_3.11 runtime (CPython 3.11, aarch64): UnityPy and what it imports, unpacked
# into <out dir> (the setup puts it on PYTHONPATH), and portmaster-unity's guided_astc. No symbolic
# links (exFAT cards cannot hold them).
#
# Wheels come from the cache dir (default pm/cache/wheels311/), else from PyPI (pip download,
# manylinux aarch64, cp311). PyPI has no aarch64 wheel of UnityPy 1.25.4, astc_encoder_py 0.1.12,
# etcpak 0.9.15 or tpk_ar 0.2.4: those are built from source in a manylinux_2_28_aarch64 container
# (Docker) when the cache lacks them.
set -euo pipefail
OUT=${1:?usage: build_pylibs.sh <out dir> [cache dir]}
HERE=$(cd "$(dirname "$0")" && pwd)
C=${2:-$HERE/cache/wheels311}; mkdir -p "$C"; C=$(cd "$C" && pwd)
PYPI="lz4==4.4.5 pillow==12.3.0 texture2ddecoder==1.0.6 brotli==1.2.0 archspec==0.2.6 attrs==26.1.0
      fsspec==2026.9.0 pyfmodex==0.7.2"
SRC="UnityPy==1.25.4 astc_encoder_py==0.1.12 etcpak==0.9.15 tpk_ar==0.2.4"
have() { ls "$C" | grep -qi "^$(echo "${1%%==*}" | tr - _)-${1##*==}-"; }
for w in $PYPI; do
  have "$w" || python3 -m pip download -q --no-deps --only-binary=:all: --platform manylinux_2_28_aarch64 \
    --python-version 3.11 --implementation cp -d "$C" "$w"
done
for w in $SRC; do
  have "$w" || docker run --rm --platform linux/arm64 -v "$C":/out quay.io/pypa/manylinux_2_28_aarch64 bash -c "
    /opt/python/cp311-cp311/bin/pip wheel --no-deps -w /tmp/w '$w' && for f in /tmp/w/*.whl; do
      case \$f in *-none-any.whl) cp \$f /out/;; *) auditwheel repair -w /out \$f;; esac; done"
done

# guided_astc (portmaster-unity's convert/native/guided_astc: the textures step's C extension, one
# abi3 wheel for CPython 3.11+), built in the same container when GUIDED_ASTC names its folder
if [ -n "${GUIDED_ASTC:-}" ] && ! ls "$C" | grep -q '^guided_astc-'; then
  docker run --rm --platform linux/arm64 -v "$C":/out -v "$(cd "$GUIDED_ASTC" && pwd)":/src:ro quay.io/pypa/manylinux_2_28_aarch64 bash -c "
    cp -r /src /tmp/g && /opt/python/cp311-cp311/bin/pip wheel --no-deps -w /tmp/w /tmp/g && auditwheel repair -w /out /tmp/w/*.whl"
fi

rm -rf "$OUT"; mkdir -p "$OUT"
for w in "$C"/*.whl; do
  python3 -c "import sys, zipfile; zipfile.ZipFile(sys.argv[1]).extractall(sys.argv[2])" "$w" "$OUT"
done
# UnityPy imports fmod_toolkit for audio export, which needs FMOD (not redistributable): a stand-in
mkdir -p "$OUT/fmod_toolkit"
cat > "$OUT/fmod_toolkit/__init__.py" <<'PY'
# Stand-in for fmod_toolkit (FMOD audio export), which UnityPy imports at start-up. The converter
# never exports audio, and FMOD's library may not be redistributed, so it is left out.
def raw_to_wav(*args, **kwargs):
    raise RuntimeError("audio export is not available in this build")
PY
# trimmed: Pillow's AVIF, WebP, colour-management, font and Tk modules and their libraries (the
# converter reads and writes raw pixels and PNG only), etcpak's module for Python 3.7
rm -f "$OUT"/PIL/_avif.* "$OUT"/PIL/_webp.* "$OUT"/PIL/_imagingcms.* "$OUT"/PIL/_imagingft.* "$OUT"/PIL/_imagingtk.* \
      "$OUT"/pillow.libs/lib{avif,webp,webpdemux,webpmux,sharpyuv,lcms2,freetype,harfbuzz,brotlicommon,brotlidec}-* \
      "$OUT"/etcpak/_etcpak_neon.cpython-37m-*
find "$OUT" \( -name __pycache__ -o -name tests \) -type d -prune -exec rm -rf {} +
find "$OUT" \( -type l -o -name '*.pyi' -o -name RECORD \) -delete
echo "built $OUT ($(du -sh "$OUT" | cut -f1))"
