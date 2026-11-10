# PhotonJoinFix

Host and client side protections against late join failures caused by conflicting Photon IDs or an expedition map bundle that has not been loaded on the joining client.

***The mod can be used both host and client side, however host sidedly it only fixes half of the problems. Sadly the expedition map bundle loading can only be fixed locally, so for full protection I recommend you download the mod yourself, instead of relying on the host having it.***

These protections target the specific cases I mentioned. I do not guarantee that this mod will fix unrelated loading, connection, or late join problems.

## What it does

**As host:**
- Before leaving the airport for an expedition, PhotonJoinFix removes eligible, runtime spawned room items from the airport and Photon’s room cache. Reloading the airport recreates its normal props.

**As client:**
- PhotonJoinFix rejects a runtime instantiation event if its Photon IDs would replace a local static scene object. It also protects that exact scene object from subsequent network destroy events until the scene unloads.
- PhotonJoinFix ensures that the correct map bundle is loaded in upon joining a game, in order to load in the required expedition scene.

## Links

**Bug Reports/Support:** https://rombusz.dev/contacts/bug-report/?mod=photon-join-fix <br>
**Feature Requests:** https://rombusz.dev/contacts/feature-request/?mod=photon-join-fix <br>
**Questions:** https://rombusz.dev/contacts/message/
