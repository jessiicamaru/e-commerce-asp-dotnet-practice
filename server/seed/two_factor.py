"""
The administrator's second factor, for the seed scripts (specs/110): staff sign in with a code from an authenticator,
and in development Identity seeds the administrator's with ADMIN_TOTP_SECRET - so a script computes the code the app
would show. How it works: docs/features/auth/totp-two-factor.md.
"""

import base64
import hashlib
import hmac
import os
import struct
import time


def totp(secret_base32, offset=0):
    """The current six-digit code for a base32 secret (RFC 6238), OFFSET windows away."""
    secret = secret_base32.replace(" ", "").upper()
    key = base64.b32decode(secret + "=" * (-len(secret) % 8))
    step = int(time.time()) // 30 + offset
    digest = hmac.new(key, struct.pack(">Q", step), hashlib.sha1).digest()
    at = digest[-1] & 15
    return str((struct.unpack(">I", digest[at:at + 4])[0] & 0x7FFFFFFF) % 10**6).zfill(6)


def finish_sign_in(first_answer, exchange):
    """
    The token of a sign-in: the first answer's own for somebody without two-factor sign-in, or - when it asks for a
    code - EXCHANGE(challenge, code)'s. A code works once: when this window's was just used by another script, the
    first exchange is refused and the next window's code is tried after waiting for it. None when it cannot be done.
    """
    answer = first_answer or {}
    if answer.get("twoFactor") != "Required":
        return answer.get("token") or answer.get("accessToken")

    secret = os.environ.get("ADMIN_TOTP_SECRET")
    if not secret:
        return None

    for attempt in range(2):
        token = (exchange(answer["challenge"], totp(secret)) or {}).get("token")
        if token:
            return token
        time.sleep(31 - int(time.time()) % 30)
    return None
