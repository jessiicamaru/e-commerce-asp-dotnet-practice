#!/usr/bin/env bash
#
# Asserts that a built image carries no credential, in ANY layer.
#
#   .github/scripts/verify-image-has-no-secrets.sh <image[:tag]>
#   ENV_FILE=server/.env .github/scripts/verify-image-has-no-secrets.sh <image>
#
# Why layers and not the running container: `COPY . .` followed by `RUN rm .env`
# leaves the file fully readable in the earlier layer. `docker run --rm <image>
# ls /app` shows nothing and passes. That is the specific mistake this script
# exists to prevent, so it must never be rewritten to inspect a running container.
#
# Why Python and not `docker save | tar -t`: `docker save` emits an OCI layout -
# `blobs/sha256/<digest>`, each layer GZIPPED. Listing the outer tar shows digest
# names and never a file path, and grepping it sees compressed bytes. The first
# version of this script did exactly that and reported a clean pass on an image
# that provably contained a secret. Every blob has to be decompressed and read.
#
# If this fails, the credentials are already in an image. Rotate them - deleting
# the image does not recall a copy somebody pulled.
set -euo pipefail

IMAGE="${1:?usage: verify-image-has-no-secrets.sh <image[:tag]>}"
ENV_FILE="${ENV_FILE:-server/.env}"

PYTHON="${PYTHON:-}"
if [ -z "$PYTHON" ]; then
  for candidate in python3 python; do
    if command -v "$candidate" > /dev/null 2>&1 && "$candidate" -c '' > /dev/null 2>&1; then
      PYTHON="$candidate"
      break
    fi
  done
fi
[ -n "$PYTHON" ] || { echo "::error::python3 or python is required" >&2; exit 1; }

command -v docker > /dev/null 2>&1 || { echo "::error::docker is required" >&2; exit 1; }
docker image inspect "$IMAGE" > /dev/null 2>&1 || { echo "::error::no such image: $IMAGE" >&2; exit 1; }

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

echo "Inspecting every layer of $IMAGE"
echo

docker save "$IMAGE" -o "$WORK/image.tar"
docker history --no-trunc --format '{{.CreatedBy}}' "$IMAGE" > "$WORK/history.txt"

IMAGE="$IMAGE" WORK="$WORK" ENV_FILE="$ENV_FILE" "$PYTHON" - <<'PYEOF'
import gzip, io, os, re, sys, tarfile

work = os.environ["WORK"]
image = os.environ["IMAGE"]
env_file = os.environ["ENV_FILE"]

failures = []


def ok(msg):
    print(f"  ok  {msg}")


# Values worth hunting for: long enough not to collide with ordinary binary
# content, and not a placeholder from .env.example.
secrets = {}
if os.path.isfile(env_file):
    for line in open(env_file, encoding="utf-8", errors="replace"):
        line = line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        name, _, value = line.partition("=")
        value = value.strip()
        if len(value) < 12 or value.startswith("your_") or value.endswith("_here"):
            continue
        secrets[value.encode()] = name.strip()

env_name = re.compile(rb"(^|/)\.env($|\.)")

layers_read = 0

with tarfile.open(f"{work}/image.tar") as outer:
    for member in outer.getmembers():
        if not member.isfile():
            continue
        raw = outer.extractfile(member).read()

        # Layers are gzipped tars; manifests and configs are JSON. Anything that
        # is not a tar after decompression is metadata, not a filesystem.
        if raw[:2] == b"\x1f\x8b":
            try:
                raw = gzip.decompress(raw)
            except OSError:
                continue
        if raw[257:262] != b"ustar":
            continue

        layers_read += 1
        with tarfile.open(fileobj=io.BytesIO(raw)) as layer:
            for entry in layer.getmembers():
                if env_name.search(entry.name.encode()):
                    failures.append(
                        f"{entry.name} is present in a layer of {image} "
                        "- check server/.dockerignore, and ROTATE the credentials")
                if not entry.isfile() or entry.size > 8 * 1024 * 1024:
                    continue
                if not secrets:
                    continue
                extracted = layer.extractfile(entry)
                if extracted is None:
                    continue
                content = extracted.read()
                for value, name in secrets.items():
                    if value in content:
                        failures.append(
                            f"the value of {name} from {env_file} appears in "
                            f"{entry.name} inside a layer of {image} - ROTATE it")

if layers_read == 0:
    failures.append(
        "read 0 filesystem layers - the scan did not actually inspect anything, "
        "which is a broken check rather than a clean image")
else:
    ok(f"read {layers_read} filesystem layer(s)")

# Credentials assigned by an ENV or ARG instruction in the build itself.
history = open(f"{work}/history.txt", encoding="utf-8", errors="replace").read()
for name in ("JWT_SECRET", "DB_PASSWORD", "ADMIN_PASSWORD",
             "PGADMIN_PASSWORD", "RABBITMQ_PASS", "RABBITMQ_PASSWORD"):
    if re.search(rf"{name}=[^\s\"']", history):
        failures.append(f"{name} is assigned a value in the build history of {image}")

if not any("is present in a layer" in f for f in failures):
    ok("no .env file in any layer")
if not any("build history" in f for f in failures):
    ok("no credential assigned in any build step")
if secrets:
    if not any("appears in" in f for f in failures):
        ok(f"no value from {env_file} appears in any layer ({len(secrets)} checked)")
else:
    print(f"  SKIPPED  value check - no usable values in {env_file}")

if failures:
    print()
    for f in dict.fromkeys(failures):
        print(f"::error::{f}", file=sys.stderr)
    sys.exit(1)
PYEOF

echo
echo "$IMAGE carries no credential in any layer."
