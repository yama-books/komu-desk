# APP-034 / Tray icons v0.1.6
Source: user-approved last generated monochrome 2×5 image, original conversation. Order is left to right, top to bottom. Ten transparent 64×64 PNGs from the original raster (cropped to actual glyph, background removed, centered and normalized; four alpha levels). Each ICO has 16/32/48 pixel 32-bit DIB frames, small monochrome white outline for contrast on dark taskbars; 48 is enlarged from the 32 mask. No vector redraw, no external images. Original full sheet not stored in repo.
| ID | Name |
|---|---|
| cat-01 | のぞきねこ暦 |
| cat-02 | お昼寝カレンダー |
| cat-03 | 暦の中のねこ |
| cat-04 | ねこの後ろ姿 |
| cat-05 | 黒い窓とねこ |
| cat-06 | チェックとねこ |
| cat-07 | ひと休み |
| cat-08 | 暦を見つめるねこ |
| cat-09 | 机からひょっこり |
| cat-10 | 肉球 |

Path: src/KomuDesk.Desktop/Assets/Tray/cat-NN.png and .ico. Default 02. Executable embeds default 02; running WPF window and NotifyIcon use selected variant. Windows pinned shortcut icon may remain cached/default. Choice under Settings > アイコン, 2 columns, image/16px light and dark preview, selected outline, save/cancel. Display.TrayIcon persisted in existing local JSON; old data defaults to 02; invalid selection falls back to 02 at runtime. Existing close button still exits and minimizes normally; actual Windows tray menu: show, settings, select icon, update, exit. No separate process, no new networking. Demo/smoke does not normally create shell notification icon; app closes and disposes it on successful exit. Test all resource sizes and WPF picker render, local persistence. Actual Explorer visibility, DPI and interaction need user's PC validation. No change to Google authentication or other widget features.
