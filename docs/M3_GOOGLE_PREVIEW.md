# APP-034 M3a Google Calendar 読み取り連携プレビュー
公式仕様に基づく独立モジュール。実アカウント認証・校務端末での通信監査は未実施。
- [デスクトップOAuth](https://developers.google.com/identity/protocols/oauth2/native-app)
- [Calendarの権限](https://developers.google.com/workspace/calendar/api/auth)
- [CalendarList.list](https://developers.google.com/workspace/calendar/api/v3/reference/calendarList/list)
- [Events.list](https://developers.google.com/workspace/calendar/api/v3/reference/events/list)

## 利用条件
Google CloudのプロジェクトでCalendar APIを有効にし、デスクトップ用OAuthクライアントを作成。Google Workspace管理者の許可とOAuth同意画面/テストユーザー/公開状態を確認。OAuth JSONは本人PCでのみ選択し、チャット・Issue・Repoに置かない。デモは常に通信なし。

## 操作
設定→Googleカレンダー→OAuth設定ファイルを選択→外部通信の確認→システムブラウザで認証→カレンダー一覧→表示対象を選択→同期。対象は設定年度（最大1年度相当）。複数カレンダー・同日複数行事・終日の排他的終了日・時刻付き・複数日を表示。同期成功時にのみキャッシュを置換し、失敗時は以前の内容を維持。起動時通信なし、手動同期のみ。Google予定は今日画面に表示し、授業変更を自動反映しない。
連携解除はローカルの認証情報/キャッシュを消去。Google側の許可取消は本人がGoogleアカウント設定で実施。

## 通信境界
KomuDesk.Calendarは校務データのCoreを参照しない。入力はOAuth設定・refresh token・選択カレンダーID・同期期間のみ。APIは固定HTTPSホストのToken POSTとCalendar GETのみ。外部リダイレクトを許可しない。PKCE S256・ランダムstate・ループバックTCP、限定タイムアウト。要求スコープ calendar.calendarlist.readonly + calendar.events.readonly。取得項目はID/概要/日時/状態、参加者・説明・添付なし。Calendar以外のAPIなし。Google状態は別ファイルをWindows DPAPI CurrentUserで保護し、通常の校務バックアップに含めない。Google認証に利用するシステムブラウザやOS自体の通信は別途観測。
差分syncTokenではなく期間限定の全再取得で削除/更新を反映（上限あり）。自動同期/バックオフ、同期範囲の変更、Calendar予定の統合ルール、OAuthの本番運用、実ネットワーク監査は後続。テストは架空HTTP応答で実施し実アカウントを利用しない。
