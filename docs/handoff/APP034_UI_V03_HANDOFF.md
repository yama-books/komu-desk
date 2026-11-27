# APP-034/UI3 実装担当への引継ぎ
2026-11-27 JST。work_id未登録/未確認。推奨チャット名 〖校務デスクUI〗1127・APP-034-UI3・独自バーと統合カレンダー。
目的: 常駐ウィジェットを保ち、独自タイトルバー、管理ナビ、統合カレンダー（月/週/日/一覧）、各画面の共通意匠、必要アイコンを段階実装する。現時点は設計のみ。
正本 yama-books/komu-desk。確認済み v0.1.7-preview tag / feature/app034-retention-icon-quality HEAD 5457961ae84008dfddf5b2f150e053840928eb52 / Draft PR #11 / CI 45953186826 (77 core、Windows描画、ICO、隔離旧版整理成功) / 配布CI 45953333797。mainの直前HEAD 9895634 は文書中心。コード積層PR未統合。最新main・PR #1〜11・releaseタグ・CIを再確認し、最新公開コードを基点に今回文書を取り込む。mainから古いコードを作り直さない。他セッションの変更を上書きしない。
読む順: AGENTS.md, docs/UI_REDESIGN_V03.md, docs/UI_ICON_INVENTORY_V03.md, docs/VISUAL_SYSTEM.md, docs/BENTO_AND_SHARED_AUTH.md, docs/RETENTION_AND_ICON_QUALITY.md, docs/LESSON_ENGINE.md, docs/UPDATE.md。対象コード DeskWindow.cs, WidgetSkin.cs, BentoMenu.cs, GooglePanel.cs, ReportsPanel.cs, TrayIcons.cs, UpdatePanel.cs, SchoolData.cs, LessonEngine.cs, GoogleCalendarClient.cs。
実装順: UI3-01 WindowChromeと共通バー、単一管理ホストとナビ、19操作の到達維持。UI3-02 既存モデルの読み取り投影をコアテストし、月＋選択日詳細、一覧を実装。UI3-03 週/日とホーム、日付導線、同期状態。UI3-04 時間割/授業数/タスク/帳票/設定を共通化。P0アイコンは使用するものから作成/再利用、原画を勝手に変更しない。各段階で小PR、検証、必要時に公開承認。過剰なフレームワーク/依存/常駐プロセスなし。
制約: Google Calendar読取/OAuth・任意GitHub Releases更新以外通信なし。実データ/名列/認証設定JSON/トークンを公開しない。既存データ、LessonEngine、Googleキャッシュ/DPAPI、旧版整理、アイコン選択、更新機構の互換性維持。UI閲覧で授業確定やGoogle通信を起こさない。Google実認証・OAuth配布条件は別の未検収課題。生徒情報の常駐表示なし。
検証: 既存77コアと全Windows smokeを維持。投影境界条件と状態遷移、画面の狭幅/広幅・DPI・操作・フォーカス、独自バー/最大化/復元/ドラッグ/Alt+F4/システムメニュー、既存19操作、保存/取消、更新・復旧を確認。架空データで画面画像、CI、変更差分を記録。Windows実機の外観/操作/通信/資源測定と自動検査を区別。失敗時は公開せず原因を記録。
完了報告: HEAD/PR/CI/公開有無/実機未検収/次の1操作を記録。共通台帳 moyashimisosoup/session-deck/docs/APP_PORTFOLIO.md。次の1操作は最新版コードを読み、UI3-01の共通Chromeと管理ホストの最小差分PRを作る。今回は実装・新リリース・追加原画制作なし。
