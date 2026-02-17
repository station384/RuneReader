# runereader-inputd (prototype notes)

This daemon is intended to run as root (or with permissions to read /dev/input/event* and write /dev/uinput).

## Features
- Monitors global key presses via evdev:
    - Default activation keys: 1,2,3,GRAVE,Q,E,W
    - Configurable activation key via command
- Injects key press/release via uinput:
    - Supports digits, F-keys, punctuation, and modifiers.
- Client connection via Unix domain socket:
    - Default: /run/runereader-inputd.sock
    - Simple shared-key authentication

## Commands
- `AUTH <sharedKey>`
- `PING`
- `SETACT <1|2|3|GRAVE|Q|E|W>`
- `INJECT <key> <d/u>`
- `MOD <ctrl|alt|shift> <d/u>`
- `INJECTC <c> <a> <s> <key> <d/u>`
- `QUIT`

## Stuck-key safety
On client disconnect and on daemon termination, daemon emits UP events for:
- all keys it believes are pressed
- all keys the virtual device is allowed to emit (belt-and-suspenders)

## Permissions
- `/dev/uinput` must be writable
- `/dev/input/event*` must be readable

## TODO
- Improve auth (random per-session token, or Unix peer credentials)
- Proper IPC protocol framing (binary, length-prefixed)
- Support notifying the client about activation key events
- systemd socket activation and hardening
