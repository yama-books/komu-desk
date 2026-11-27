using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using KomuDesk.Calendar;
using KomuDesk.Core;
using Microsoft.Win32;

namespace KomuDesk.Desktop;

internal sealed partial class DeskWindow
{
    private GoogleVault? googleVault;
    private GoogleState? googleState;
    private string googleStatus="連携OFF";
    private void InitGoogle()
    {
        if(demo)return;
        googleVault=new GoogleVault();
        try
        {
            googleState=googleVault.Load();
            googleStatus=googleState==null?"連携OFF":"手動同期";
        }
        catch{googleStatus="設定を読み込めません。再認証してください。";}
    }
    private void RenderGoogle()
    {
        if(demo){body.Children.Add(Text("Googleカレンダー：デモでは接続しません",11,false,Muted));return;}
        var heading=Horizontal();Add(heading,Text("Googleカレンダー",13,true),Btn("設定・同期",GoogleSettings));body.Children.Add(heading);
        body.Children.Add(Text(googleStatus,11,false,Muted));
        var snapshot=googleState?.Snapshot;
        if(snapshot==null)return;
        body.Children.Add(Text("最終取得 "+snapshot.LastSuccessUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm")+"（保存済み）",11,false,Muted));
        if(string.CompareOrdinal(SchoolData.Iso(day),snapshot.From)<0||string.CompareOrdinal(SchoolData.Iso(day),snapshot.To)>0)
        {body.Children.Add(Text("この日は同期範囲外です。",12,false,Muted));return;}
        var selected=googleState!.Session.SelectedIds;
        var list=snapshot.Events.Where(e=>selected.Contains(e.CalendarId)&&e.OnDate(day)).OrderBy(e=>e.AllDay?0:1).ThenBy(e=>e.DisplayTime(day)).ToList();
        if(list.Count==0)body.Children.Add(Text("保存済み予定なし",12,false,Muted));
        foreach(var e in list)body.Children.Add(Text($"{e.DisplayTime(day)}   {e.Title}",13));
    }
    private void GoogleSettings()
    {
        if(demo||googleVault==null){MessageBox.Show(this,"デモモードでは外部通信を行いません。");return;}
        var w=Dialog("Googleカレンダー・読み取り専用",620,580);
        var p=new StackPanel{Margin=new Thickness(14)};
        var choices=new StackPanel{Margin=new Thickness(0,6,0,6)};
        var status=Text(googleStatus,13,false,Muted);
        var selection=new List<(string Id,CheckBox Box)>();
        var buttons=new List<Button>();
        CancellationTokenSource? operation=null;
        void Build()
        {
            choices.Children.Clear();selection.Clear();
            if(googleState==null){choices.Children.Add(Text("未連携です。",13));return;}
            foreach(var c in googleState.Session.Calendars)
            {
                var check=new CheckBox{Content=Text(c.Name,13),IsChecked=googleState.Session.SelectedIds.Contains(c.Id),Margin=new Thickness(0,4,0,4)};
                choices.Children.Add(check);selection.Add((c.Id,check));
            }
            if(selection.Count==0)choices.Children.Add(Text("読み取り可能なカレンダーがありません。",13,false,Muted));
        }
        async Task Run(Func<CancellationToken,Task> task)
        {
            if(operation!=null)return;
            using var cts=new CancellationTokenSource();operation=cts;
            foreach(var button in buttons)button.IsEnabled=false;
            choices.IsEnabled=false;status.Text="処理中…";
            try{await task(cts.Token);status.Text=googleStatus;Build();}
            catch(OperationCanceledException){googleStatus="処理を中止しました。保存済み予定を維持します。";status.Text=googleStatus;}
            catch(Exception e){googleStatus="連携処理に失敗しました。保存済み予定を維持します。 ("+e.GetType().Name+")";status.Text=googleStatus;}
            finally{operation=null;foreach(var button in buttons)button.IsEnabled=true;choices.IsEnabled=true;Render();}
        }
        Button ActionButton(string title,Func<Task> work)
        {
            var b=WidgetButton(title,title,()=>{});b.Margin=new Thickness(2);b.Padding=new Thickness(10,6,10,6);b.MinHeight=34;
            b.Click+=async (_,_)=>await work();buttons.Add(b);return b;
        }
        var configStatus=Text("",12,false,Muted);
        var detailsPanel=new StackPanel{Margin=new Thickness(0,6,0,0)};
        var details=new Expander{Header="開発者・管理者用の認証設定",Content=detailsPanel,Margin=new Thickness(0,8,0,8)};
        void ConfigDisplay()
        {
            try{configStatus.Text=GoogleClientConfig.Resolve(googleState?.Session)==null?"共有用OAuth設定がまだありません。管理者が一度準備してください。":"OAuth設定済み · ブラウザ認証可能";}
            catch{configStatus.Text="認証設定を読み込めません。詳細設定から再登録してください。";}
        }
        var login=ActionButton("Googleカレンダーと連携する",async ()=>
        {
            DesktopCredential? credential;
            try{credential=GoogleClientConfig.Resolve(googleState?.Session);}
            catch{status.Text="認証設定を読み込めません。詳細設定から再登録してください。";details.IsExpanded=true;return;}
            if(credential==null){status.Text="共有用OAuthクライアントが未設定です。管理者に確認してください。";details.IsExpanded=true;return;}
            if(MessageBox.Show(w,"既定のブラウザでGoogle公式認証ページを開きます。予定の読み取りのみ許可します。既存の連携は成功後に置き換えます。学校の許可を確認して進めてください。","Googleカレンダー連携",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
            await Run(async ct=>
            {
                GoogleClientConfig.Save(credential);
                status.Text="ブラウザでアカウントを選択し、アクセスを許可してください。";
                using var client=new GoogleCalendarClient();
                var session=await client.LoginAsync(credential,url=>Process.Start(new ProcessStartInfo(url){UseShellExecute=true}),ct);
                var nextState=new GoogleState(session,null);
                googleVault.Save(nextState);googleState=nextState;
                googleStatus="認証完了。カレンダーを選択して同期してください。";
            });
            ConfigDisplay();
        });
        var configure=ActionButton("管理者のOAuth設定ファイルを読み込む",async ()=>
        {
            var file=new OpenFileDialog{Filter="Google OAuth Desktop JSON (*.json)|*.json",CheckFileExists=true};
            if(file.ShowDialog(w)!=true)return;
            try
            {
                if(MessageBox.Show(w,"このPCにOAuthクライアント設定を登録します。既存の予定・認証セッションは成功した再認証まで維持します。","認証設定",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
                GoogleClientConfig.Save(DesktopCredential.Read(file.FileName));
                status.Text="設定を登録しました。Googleカレンダーと連携する、を押してください。";
                ConfigDisplay();details.IsExpanded=false;
            }
            catch{status.Text="デスクトップアプリ用OAuth設定を読み込めませんでした。";}
            await Task.CompletedTask;
        });
        Add(detailsPanel,Text("通常は操作不要です。開発者が用意したデスクトップ用JSONを一度登録する場合のみ使用します。ファイルを公開しないでください。",12,false,Muted),configure);
        var reload=ActionButton("カレンダー一覧を更新",async ()=>
        {
            if(googleState==null){status.Text="先に連携してください。";return;}
            await Run(async ct=>{using var client=new GoogleCalendarClient();var session=await client.RefreshListAsync(googleState.Session,ct);var nextState=new GoogleState(session,googleState.Snapshot);googleVault.Save(nextState);googleState=nextState;googleStatus="一覧を更新しました。";});
        });
        var sync=ActionButton("選択したカレンダーを同期",async ()=>
        {
            if(googleState==null){status.Text="先に連携してください。";return;}
            var ids=selection.Where(x=>x.Box.IsChecked==true).Select(x=>x.Id).ToList();
            if(ids.Count==0){status.Text="カレンダーを1件以上選択してください。";return;}
            await Run(async ct=>
            {
                var session=googleState.Session with{SelectedIds=ids};
                var from=SchoolData.ParseDate(data.YearFrom);var to=SchoolData.ParseDate(data.YearTo);
                using var client=new GoogleCalendarClient();
                var snapshot=await client.SyncAsync(session,from,to,ct);
                var nextState=new GoogleState(session,snapshot);googleVault.Save(nextState);googleState=nextState;
                googleStatus="同期成功 · "+snapshot.Events.Count+"件 · 手動同期";
            });
        });
        var disconnect=ActionButton("連携解除・ローカルキャッシュ消去",async ()=>
        {
            if(MessageBox.Show(w,"このPCの認証情報と保存済みGoogle予定を削除します。Google側のアクセス許可の取消はGoogleアカウント設定で行ってください。","確認",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
            await Run(ct=>{googleVault.Clear();googleState=null;googleStatus="連携OFF";return Task.CompletedTask;});
        });
        var stop=Btn("処理を中止",()=>operation?.Cancel());
        Add(p,Text("Google Calendar",20,true),Text("外部通信は下の認証・一覧取得・同期ボタンを押した時のみ。Google側への予定の書き込み、Tasks、校務データの送信はありません。",13,false,Muted),
            Text($"同期対象期間：{data.YearFrom} ～ {data.YearTo}",13),configStatus,login,details,reload,Rule(),Text("表示するカレンダー",14,true),choices,sync,disconnect,stop,Rule(),status,Btn("閉じる",w.Close));
        w.Closing+=(_,e)=>{if(operation!=null){operation.Cancel();e.Cancel=true;status.Text="中止処理中…";}};
        Build();ConfigDisplay();w.Content=new ScrollViewer{Content=p,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};w.ShowDialog();
    }
}
