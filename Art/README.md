# Book background provenance

`BookBackground.png` is an image-tool-generated blank parchment, leather and brass inventory panel derived from the user's supplied Oblivion Remastered inventory screenshot. It is a UI texture asset, not a screenshot of the implemented widget.

Reference supplied by the user: `codex-clipboard-5698c52c-c3ca-4a60-a385-f8878893d4ad.png`.

Original generated asset: `C:/Users/micha/.codex/generated_images/01a08112-0dcd-71f2-bcad-bd6541bd294b/exec-c2cbd86e-34a7-4475-9695-890def689b5c.png`.

The original asset contained an opaque checkerboard outside the book. A correction requested through the image tool used this prompt:

> Edit this inventory book background asset. Preserve the blank parchment, black leather frame and brass clip exactly. Remove the fake gray white checkerboard OUTSIDE the book silhouette and replace it with REAL transparent alpha pixels. Output transparent PNG, not painted checkerboard. No words, icons or other changes. Keep the book centered and complete, same portrait proportions.

That correction also retained a checkerboard and was not adopted. The user subsequently authorized local removal. `clean_book_alpha.py` applies an antialiased silhouette mask to the original RGB artwork; `BookBackground-original.png` preserves the source. No game UI words or inventory icons are included in the blank panel; all visible book control text is rendered by Unreal widgets.

Notifications reuse `T_Book` directly through a brush UV region selecting the parchment interior (pixels 72,720 to 1050,1130 in the 1123 x 1400 source). No separate notification raster is generated.

## Torn notification parchment — 0.3.2

`NotificationParchment.png` was generated with the built-in image tool as blank horizontal aged ivory parchment, with deckled tears on all four sides, warm cream fibres, faint creases and a real transparent alpha background. It replaces the cropped book paper in notifications, so all four torn edges remain visible. Original: `call_ysiQSrL7jYFPxZuGtuy98I88.png` in the thread's generated_images directory. Generated RGBA size 2170 x 725; alpha range 0–255. The generated pixels are unchanged.
