# UI optimisation pass — 0.9.3

The appearance and 30 Hz input/update rate are preserved. This pass removes repeated work in the managed UI script; it does not replace or recook the artwork.

* Book highlights, settled fades/progress and recipe previews update appearance only when their state changes. Hover and keyboard focus are sampled once for both highlighting and preview selection. Static menus poll only relevant shortcuts and skip drawing/drag geometry work.
* HUD progress changes no longer reload parchment or reapply fonts, text, colours and layout. Stable panels issue no setters; an animated progress-only bar normally needs one `SetPercent` call.
* Speaker icons reuse the head socket name and hidden widgets. Local-only speaking skips remote-player processing. Remote on-screen speakers still receive a fresh wall check every active update; sneaking and failed checks hide icons immediately.
* Drawing segments are pooled within a page. Appending, undoing, redoing and recolouring a stroke reuse existing children. Page changes and teardown clear the pool.
* Unreal property/function metadata is cached by class and member name, with invalidation when the game HUD host changes. Native script default objects are cached; asset-object searches remain uncached. A UI update shares one host lookup.
* Native calls pin their managed parameter buffer during synchronous `ProcessEvent`, eliminating a native allocation and two copies per call. Primitive arguments write directly into validated fields. Notification packets are reused while stationary.

## Validation

Release build: no warnings or errors. API/native argument suite: 1,364 checks plus the native metadata check. Harnesses using actual runtime source and a simulated native boundary: 35 book/preview/drawing checks and 20 HUD/speaker scenarios. They include state changes, hiding, host replacement, stale-pointer cleanup, occlusion, sneaking, invalid pawns and trace errors.

The packing benchmark processes 600,000 representative primitive parameters: the previous writer allocates 53,600,040 bytes; the direct writer allocates 40 bytes (the benchmark stopwatch). Elapsed times vary and are not an in-game FPS result.

Harnesses and detailed results are under `Output/Optimization` and the directory named in `Output/optimization-last.txt`. The HUD/speaker harness was run from the local temporary test directory; its output is saved with the release checks.

## Remaining limits

Changing a book definition still performs a full page rebuild, so callers should avoid repeatedly replacing whole pages for live counters. The checks do not measure Unreal render-thread/GPU time or actual game FPS. In-game verification should cover opening/closing menus, changing pages, drawing undo/redo, live progress and travelling between areas.
