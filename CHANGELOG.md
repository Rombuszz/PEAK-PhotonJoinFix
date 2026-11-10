# Changelog

## [1.3.1] - 2026.11.09

- Added all project support links to the readme.

## [1.3.0] - 2026.11.02

- Fixed room code joins bypassing map bundle preparation while Photon is still joining the room. (Bug report: *Kiki*)
- Required map bundles are now always requested before loading an expedition scene.
- Added verification to check whether the loaded bundle contains the requested scene.

## [1.2.0] - 2026.10.30

- Fixed a method naming collision preventing the new asset bundle loading fix to work.

## [1.1.0] - 2026.10.28

- Fixed a new, previously unknown issue, which caused players infinite loading screens when they attempted joining lobbies.<br>
(*This was caused by clients being unable to load scenes that are outside of their currently loaded asset bundles. This mod forces the client to load in the requested expedition map's asset bundle first, before Photon attempts to load its scene.*)

## [1.0.0] - 2026.10.26

- Initial release.
