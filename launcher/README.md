# RestMC Windows Launcher

Native Windows x64 launcher for RestMC 1.6.7.

## Account flow

The launcher intentionally mirrors the in-game RestMC account flow:

1. Bind a temporary loopback callback on `127.0.0.1`.
2. Generate a random 16-byte state value.
3. Open `https://mc.tiers.rest/login/?port=<port>&state=<state>` (or `/signup/`).
4. Validate the returned `code` and `state`.
5. Exchange the one-time code with `POST /api/v1/exchange`.
6. Store the RestMC token using Windows DPAPI in the same `dpapi:<hex>` format used by the client.
7. Keep `~/.restmc/game-session.txt` compatible with the game-to-launcher session bridge.

The launcher never sends RestMC tokens to the upstream Arcanic service.

## Install location

`%LOCALAPPDATA%\Programs\RestMC\RestMC.exe`

## Client install

The launcher downloads `RestMC-1.6.7.jar`, verifies the published SHA-256 manifest, installs it into `.minecraft/mods`, checks Fabric Loader 1.21.11, and opens the Minecraft Launcher.

## License

GPL-3.0-or-later. RestMC is based on Arcanic Visuals and preserves upstream notices.
