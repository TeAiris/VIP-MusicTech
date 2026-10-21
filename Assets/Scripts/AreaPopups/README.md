# Area trigger popups (VR / simulator)

Movement-based studio hints: walk into a **trigger box**, get a **popup UI** for that area. Matches the advisor workflow — trigger volumes + UI — and works in Play Mode without a headset.

## Quick test (StudioHub)

1. Open `Assets/Scenes/StudioHub.unity`.
2. Press **Play**.
3. Fly with **WASD**, **Q/E** up/down, **right-mouse** look.
4. Enter a translucent zone (Mixing Board, Mic Booth, Instruments, Effects Rack) — its world-space popup fades in. Leave and it fades out.

## Pieces

| Component | Role |
| --- | --- |
| `AreaPresenceProbe` | On XR Origin or Main Camera so zones recognize the player |
| `AreaTriggerZone` | Trigger collider for one studio area; gizmo in Scene view |
| `AreaPopupPanel` | World-space popup (title + hint) with fade / billboard |
| `AreaPopupDirector` | Shows the popup for the active zone (stack-safe if overlaps) |
| `AreaPopupBootstrap` | Builds a 4-zone demo layout at runtime |
| `EditorFlyController` | Desktop movement for simulator testing |

## Editor menus

- **Tools → Music Studio → Create Area Popup Demo** — add demo to the current scene  
- **Tools → Music Studio → Add Presence Probe To Selection** — mark XR Origin / camera as the player  
- **Tools → Music Studio → Create StudioHub Area Popup Scene** — regenerate `StudioHub.unity`

## Headset next step

1. Remove or disable `EditorFlyController` on the camera when using a real XR rig.
2. Add `AreaPresenceProbe` to **XR Origin** (menu above).
3. Keep or reposition the `AreaTriggerZone` boxes around your studio layout.
4. Optional: hook `AreaPopupDirector.AreaActivated` to open a fuller station UI later.
