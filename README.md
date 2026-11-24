# KOMU DESK（校務デスク）

Local-first Windows desktop planner for teachers: timetable, calendar, lesson counts and tasks.

今日の授業・予定・タスクをデスクトップの片隅に表示し、年間の授業時数管理と連動する教員向けアプリ。モノクロを基本とし、学年・クラスのバッジのみ任意の色を使用します。

## 設計方針
- Windows向け。時間割・年間予定・授業計上・進度・名列・出欠・タスク・帳票はローカル処理。
- 外部通信は任意の Google Calendar 読み取り連携と必要なOAuthのみ。Google Tasks、外部AI、テレメトリ、クラウド保存なし。
- 既存Excelを読み取り専用で取り込み、授業数を再計算。原本・実データ・個人情報は公開しない。
- オフラインで主要機能が完結することを必須とする。

## 設計文書
- [基本設計](docs/DESIGN.md)
- [授業計上エンジン・データモデル](docs/LESSON_ENGINE.md)
- [Excel移行・通信境界](docs/IMPORT_AND_PRIVACY.md)
- [UI・操作設計](docs/UI_SPEC.md)
- [開発ロードマップ](docs/ROADMAP.md)
- [開発・安全規則](AGENTS.md)

## 状態
2026-11-24：App Factory [Issue #23](https://github.com/moyashimisosoup/app-factory/issues/23) からPublicリポジトリを生成し、初期設計を保存。実装、CI、配布、Windows実機確認は未実施。ソース公開とアプリ配布は別工程。

次はWindows上の軽量なオフライン常駐技術の比較試作。公開Repoに実データを保存しない。
