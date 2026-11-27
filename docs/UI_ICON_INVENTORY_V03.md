# APP-034 UIアイコン台帳 v0.3
2026-11-27 JST / 設計、既存素材照合済み。基準 v0.1.7 / 5457961。実在素材: src/KomuDesk.Desktop/Assets/Icons/*.png 16種、Assets/Tray/cat-01〜10 と明暗別ICO・Masters。旧VISUAL_SYSTEMのアイコン候補名をすべて新規制作しない。
## 1. 再利用（16点、実ファイル確認済）
| Asset ID | 用途 |
|---|---|
| calendar | カレンダー/予定 |
| timetable | 時間割/版 |
| bell | 時程 |
| classes | 学年・クラス・担当授業 |
| lesson-count | 授業数 |
| notebook | 進度・科目・日別変更 |
| tasks | タスク |
| printer | 帳票/印刷 |
| spreadsheet | Excel |
| appearance | 外観/年度 |
| backup | バックアップ |
| update | アップデート/同期（初期兼用） |
| settings | 設定 |
| restore | 復元 |
| connection | Google連携 |
| cat | ウィジェット装飾 |
猫10案のID・選択・高品質ICOを維持。新しいロゴを作らない。機能アイコンと猫の装飾を分離。
## 2. 追加P0（まず24点、採用済みではなく制作候補）
| 番号/ID | 図形 | 使う場所 |
|---|---|---|
| 01 home | 家 | 主ナビ |
| 02 calendar-month | 7列格子 | 月表示 |
| 03 calendar-week | 7列＋帯 | 週表示 |
| 04 calendar-day | 1日枠 | 日表示 |
| 05 calendar-list | 日付点と行 | 一覧 |
| 06 calendar-today | 日付枠と点 | 今日 |
| 07 chevron-left | 左山形 | 前へ |
| 08 chevron-right | 右山形 | 次へ |
| 09 filter | 漏斗 | 表示対象 |
| 10 sync | 円形矢印 | Google同期、updateとは意味分離 |
| 11 add | ＋ | 予定/タスク追加 |
| 12 edit | 鉛筆 | 編集 |
| 13 trash | ごみ箱 | 削除 |
| 14 save | 保存図形 | 保存 |
| 15 check | チェック | 完了・確定 |
| 16 close | × | 閉じる/取消 |
| 17 minimize | 横線 | 最小化 |
| 18 maximize | 四角 | 最大化 |
| 19 window-restore | 重なった四角 | ウィンドウ復元（データrestoreと別） |
| 20 pin | 画鋲 | 最前面 |
| 21 pin-off | 斜線画鋲 | 最前面解除 |
| 22 compact | 小窓 | ミニ |
| 23 expand | 拡大角 | 通常に戻す |
| 24 menu | 三本線 | ナビ折畳 |
## 3. 追加P1（必要画面実装時）
search, chevron-down, chevron-up, back, more, import, export, copy, clock, storage, cloud-off, warning, info, lock, external-link, disconnect, checkbox, calendar-event, lesson-marker, task-marker。マーカーは既存 calendar/timetable/tasks の簡略形または小さな点・テキストの組合せで足りる場合は追加しない。Google出所はG/Google文字を添える（公式ブランド利用時は規定遵守）。同期待ち/空状態/休憩猫は既存cat/猫原画再利用を優先、新規キャラは後続承認。出欠/名列アイコンは機能実装まで保留。
## 4. 制作・切出し規格
P0は標準的な機能図形を24×24論理グリッド、均一ストローク1.75〜2、round cap/join、中央寄せ。生成原画を制作する場合は6列×4行、各セル同サイズ、文字/番号/背景タイル/境界線/影なし、均等余白。左上から番号順、原画は保存し切り出し時に実形状を照合。既存PNGの暖かい墨色・丸みを合わせる。UIの機能図形は既存素材またはライセンス確認済み単一OSSセットも可（追加制作不要なら再利用、採用ライセンス/NOTICE記録）。小さいウィンドウボタンは正確な形・識別を優先し手描き風の歪みを入れない。原画から透過マスター128以上、16/20/24/32/48/64で独立縮小、半透明エッジ、強制白縁・二値化禁止。SVGで猫を再描画しない。機能アイコンに限り標準ベクター/Geometryも可、ユーザーの原画指定はラスタ優先。外部フォント/画像の実行時取得なし。
保存: Assets/Icons/<id>.png を既存規則に合わせる。必要ならMasters/とサイズ別・明暗別ファイル、ID→原画セル→ハッシュ→使用画面→ライセンス/生成由来→承認状態の台帳。未作成は pending、生成/切出し/保存/実UI反映を区別。ウィンドウ/トレイICOは既存Assets/Trayと生成スクリプトを維持。ツールチップ・AutomationProperties.Name、アイコン単独操作のヒット領域28以上。小サイズ明暗/高コントラスト/DPIの実機確認を行う。
