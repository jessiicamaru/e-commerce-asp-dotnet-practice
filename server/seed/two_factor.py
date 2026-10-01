"""
The administrator's second factor, for the seed scripts (specs/110): staff sign in with a code from an authenticator,
and in development Identity seeds the administrator's with ADMIN_TOTP_SECRET - so a script computes the code the app
would show. How it works: docs/features/auth/totp-two-factor.md.

Run on its own, it is how a PERSON signs in as that administrator in development - the seed already turned two-factor
on, so the sign-in page asks for a code nobody enrolled:

    python seed/two_factor.py          # the current code, and the link to add the account to an authenticator app

It reads ADMIN_TOTP_SECRET and ADMIN_EMAIL from the environment, else from server/.env.
"""

import base64
import hashlib
import hmac
import os
import struct
import time
import urllib.parse


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


def _from_env_file(name):
    """NAME from the environment, else from server/.env beside this folder - None when neither has it."""
    if os.environ.get(name):
        return os.environ[name]
    path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".env")
    if not os.path.exists(path):
        return None
    with open(path, encoding="utf-8") as env:
        for line in env:
            key, sep, value = line.strip().partition("=")
            if sep and key.strip() == name:
                return value.strip().strip('"').strip("'") or None
    return None


def key_uri(secret_base32, account, issuer="EcommerceShop"):
    """What the enrolment QR code holds (Identity's Totp.KeyUri), so an authenticator app can add the same account."""
    label = urllib.parse.quote(issuer) + ":" + urllib.parse.quote(account)
    secret = secret_base32.replace(" ", "").upper().rstrip("=")
    return (f"otpauth://totp/{label}?secret={secret}&issuer={urllib.parse.quote(issuer)}"
            "&algorithm=SHA1&digits=6&period=30")


if __name__ == "__main__":
    secret = _from_env_file("ADMIN_TOTP_SECRET")
    if not secret:
        raise SystemExit("ADMIN_TOTP_SECRET is not set (environment or server/.env), so the administrator has no "
                         "seeded second factor - sign in and enrol at /account/two-factor instead.")
    email = _from_env_file("ADMIN_EMAIL") or "admin"
    left = 30 - int(time.time()) % 30
    print(f"Code for {email}: {totp(secret)}  (valid for {left}s more; the next is {totp(secret, 1)})")
    print("A code works once - if it was just used, wait for the next one.")
    print()
    print("To keep codes on your phone, add this account to an authenticator app")
    print(f"(\"enter a setup key\": {secret.replace(' ', '').upper()}, time-based), or open this link on the phone:")
    print(key_uri(secret, email))
