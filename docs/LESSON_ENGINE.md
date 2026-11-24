# 授業計上エンジン 詳細仕様 v0.1
設計段階 / 2026-11-24。以下の例はすべて架空。実際の学校規則・原本の値は保存しない。

## 1. データ契約
SQLite候補。日付は YYYY-MM-DD（学校の設定タイムゾーン、既定 Asia/Tokyo）、時刻は日内分、期間は開始/終了日とも含む。瞬間の記録はUTC ISO 8601。IDは不変のランダムIDとし、表示名で結合しない。年度単位で分離する。
| エンティティ | 主要属性・制約 |
|---|---|
| academic_year | id, label, start_date, end_date; 重複年度は確認 |
| period_range | id, year_id, kind(term/exam_interval/custom), label, start/end |
| grade/class/subject | id, year_id, label, color; クラスはgrade_idを持つ |
| teaching_group | id, year_id, subject_id, label; group_memberで対象クラス複数可、名列なし可 |
| timetable_version | id, year_id, valid_from/to, name, cycle; 日付の有効版は一意 |
| timetable_slot | version_id, weekday, cycle_key, period_no, group_id, type; 版内で同一枠重複不可 |
| bell_schedule/slot | id, name, period_no, start/end_min; 時限の重複・逆転禁止 |
| day_override | date, target_scope, weekday_override, bell_id, closed, source_event_id, reason, approved_at |
| slot_override | date, period_no, operation(cancel/replace/add/move), group_id, reason, revision |
| lesson | id, date, period_no, group_id, type, plan_state, actual_state, duration, source_version, source_override, move_id |
| lesson_revision | lesson_id, revision, before/after, reason, changed_at; 確定訂正を追跡 |
| lesson_note | lesson_id, content, next_content, materials, updated_at |
| calendar_event | id, source, source_key, start/end, all_day, title, cancelled, last_seen |
| event_effect | event_id, effect, target, approved, revision |
| local_task | id, title, due_date/time nullable, done_at, priority, optional group/lesson_id |
| roster/student/attendance | 任意後続機能、年度分離、lesson_id/student_idを一意にし未入力と出席を区別 |
| import_batch/settings | 入力の識別、版、件数、設定、操作取消に必要な差分 |
外部キー・NOT NULL・一意制約を適用し、削除は関連確認を経て論理削除を優先。実装時にスキーマmigrationを用意しバックアップ復元テスト。

## 2. 日別展開と優先順位
1. 年度内か確認し、対象日に有効な基本時間割版と周期を選択。未設定は空ではなく未設定表示。
2. 承認済みの日別曜日変更を適用。休業日・考査・時程変更を判定。祝日は表示のみが既定で、学校ごとに授業なし設定を承認する。
3. 対象学年・クラスの閉鎖/休講を適用。合同授業に一部のみ影響する場合は自動確定しない。
4. 時程の時限を対応付ける。時程に存在しない時限は取消候補。開始時刻変更のみで回数を減らさない。
5. 個別枠変更、追加、振替を反映。閉鎖日に追加する場合は明示的確認を要する。時刻衝突は保存前に解決。
6. 既存の確定実績を保全。過去の再展開は差分提案、未来の未確定分のみ再生成。再実行で同一授業の複製を発生させない。
7. ビュー（今日・月間・集計・帳票）を同じ結果から生成。
同じ範囲の相反する行事効果は後勝ちにしない。未解決日は既存確定値を維持し、将来見込みに「要確認」を併記。ルールは文字列完全一致/明示選択から開始、AI分類を導入しない。

## 3. 実績と授業種別
plan_state: scheduled / cancelled / moved_out / unresolved。
actual_state: unknown / confirmed_held / confirmed_not_held。
実施推定は保存された確定状態ではなく、予定・終了時刻・基準日時から算出する派生値。既定は終了時刻を過ぎた有効授業を推定表示、一日一括確定、当日だけ個別変更可能。基準日はUIで任意指定（過去日全体の集計はその日終了時点、今日モードは現在時点）。
種別 normal/exam/self_study/substitute/other と計上条件を分ける。既定は通常授業を1回、考査/代講/自習は年度ごとに本人設定し未設定は要確認。実施回数（整数）と授業時間（分）を別集計し、考査の実時間が未設定なら分数不明。
振替は元枠と先枠を同一move_idで保持。元は計上しない、先の有効実施だけ計上。元が確定済なら明示訂正が必要。振替取消は両方の変更を一つのトランザクションで戻す。

## 4. 集計定義
対象範囲・授業グループ・計上設定を固定し、各授業を一回だけ分類。
- 確定実績 C：計上対象種別かつ confirmed_held。
- 推定実績 E：確定なし、予定有効、終了時刻経過、計上対象。
- 残予定 R：計上対象、未確定、有効、まだ終了していない。
- 見込み C+E+R。休講/振替元/不実施/不明は別数。開始済で終了前はRに含め進行中表示。
- 集計時に未解決数 U を併記。未知の種別・時程を勝手に0と決めない。
同一日に同科目2コマは2回、授業日一覧にも時限付き2行。合同授業は教師の総回数1回、クラス別対象ビューには各1回。クラス別合算を教師総数と混同しない。過去実績訂正は集計・授業番号を再表示するがメモはIDに紐付けたまま保持。
期間内回数と年度累積回数を別選択。考査日当日の境界は区間設定で明示し、隣接区間の重複・欠落を警告。学年/科目/期間ごとにクラス差（最大-最小）、残予定、学期末見込みを表示。

## 5. 欠席可能回数（任意）
総計上数 N、最低必要出席数 m を使い、最大欠席数 N-m、要件未達境界 N-m+1。出席率 p/q 以上なら m=ceil(N*p/q)、超えるなら m=floor(N*p/q)+1。整数演算で端数誤差を回避。N=0、未確定ルール、矛盾入力は「算出不可」。予測総数と確定済数に基づく参考値を区別する。欠席・公欠・遅刻等の扱いは規程に従い別設定。正式認定は教員の判断。個人の欠席値はローカル管理画面のみ。

## 6. 合成受入ケース
架空のA組・B組、月1限A、月2限B、火1限A、火2限B、月火2日間。
| ケース | 期待 |
|---|---|
| 通常4枠 | 教師4、A2、B2 |
| 火全休 | 教師2、A1、B1 |
| 火1限のみ振替（水1限） | 月〜水の総数4、火A0、水A1 |
| 短縮時程 | 回数4、時間のみ変更 |
| 考査1枠を置換 | 含むなら4、除くなら3 |
| 同日にイベント2件 | 両件表示、休講は二重適用なし |
| 確定後の時間割改訂 | 確定実績とメモは保持 |
| 同日同組2枠 | 2回計上、日一覧に2行 |
| 合同1枠 | 教師1、対象各クラス1 |
| 出席要件>1/2、N=20/21 | 最大欠席9/10 |
| N=0/時程未設定 | 判定不能を明示 |
単体テストで純粋エンジンを優先し、UI/DB統合では代表的な一連の流れのみ。実際のExcelとの一致はローカル受入で別途確認。
