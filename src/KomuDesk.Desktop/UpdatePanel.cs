using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using KomuDesk.Core;

namespace KomuDesk.Desktop;
internal sealed partial class DeskWindow
{
    internal string? PendingUpdate {get;private set;}
    private UpdateOffer? availableUpdate;
    private bool updateBusy;
    private Button? updateBadge;
    private UpdateCheckState updateState=UpdateCheckState.Empty;
    private bool HasUpdateNotice=>!demo&&(availableUpdate!=null||updateState.HasNewer(UpdateInstall.Current));
    private static string HistoryPath=>Path.Combine(Path.GetDirectoryName(UpdateOptIn)!,"update-check.txt");
    private void RefreshUpdateNotice(){if(updateBadge!=null){updateBadge.Visibility=HasUpdateNotice?Visibility.Visible:Visibility.Collapsed;footerStatus.Visibility=HasUpdateNotice?Visibility.Collapsed:Visibility.Visible;}}
    private void SaveHistory(){try{Directory.CreateDirectory(Path.GetDirectoryName(HistoryPath)!);var tmp=HistoryPath+".tmp";File.WriteAllText(tmp,updateState.ToStorage());File.Move(tmp,HistoryPath,true);}catch(IOException){}catch(UnauthorizedAccessException){}}
    private string HistoryLabel()=>"最終確認成功："+(updateState.Success?.ToLocalTime().ToString("yyyy/MM/dd HH:mm")??"未確認")+"\n最終確認試行："+(updateState.Attempt?.ToLocalTime().ToString("yyyy/MM/dd HH:mm")??"未確認");
    private static string UpdateOptIn=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"KOMU DESK","update-enabled");
    private bool AutoUpdate=>!demo&&File.Exists(UpdateOptIn);
    private void InitUpdates()
    {
        if(demo)return;
        try{if(File.Exists(HistoryPath)&&new FileInfo(HistoryPath).Length<=512)updateState=UpdateCheckState.Read(File.ReadAllText(HistoryPath));}catch(IOException){}catch(UnauthorizedAccessException){}
        RefreshUpdateNotice();
        if(AutoUpdate&&updateState.IsDue(DateTimeOffset.UtcNow))Loaded+=async (_,_)=>await CheckUpdates();
    }
    private async Task CheckUpdates()
    {
        if(demo||updateBusy||!AutoUpdate)return;
        updateBusy=true;
        availableUpdate=null;
        var now=DateTimeOffset.UtcNow;
        try
        {
            using var client=new UpdateClient();using var ct=new CancellationTokenSource(TimeSpan.FromSeconds(30));
            availableUpdate=await client.Check(UpdateInstall.Current,ct.Token);
            updateState=updateState.Succeeded(now,availableUpdate?.Tag??"");
        }
        catch{updateState=updateState.Failed(now);}
        finally{SaveHistory();updateBusy=false;RefreshUpdateNotice();}
    }
    private void Updates()
    {
        if(demo){MessageBox.Show(this,"デモでは通信しません。");return;}
        if(updateBusy){MessageBox.Show(this,"更新確認中です。終了後にもう一度開いてください。");return;}
        var w=Dialog("アプリのアップデート",520,510);
        var panel=new StackPanel{Margin=new Thickness(16)};
        var status=Text($"現在：{UpdateInstall.Current} preview",14,true);
        var detail=Text("GitHub Releasesの公開評価版を確認します。校務データやGoogle認証情報は送信しません。",13,false,Muted);
        var history=Text(HistoryLabel(),12,false,Muted);
        var progress=new ProgressBar{Height=7,Minimum=0,Maximum=100,Visibility=Visibility.Collapsed,Margin=new Thickness(0,6,0,6)};
        var opt=new CheckBox{Content="起動時に新しい版を自動確認する（最大12時間に1回、失敗時は1時間後から）",IsChecked=AutoUpdate,Margin=new Thickness(0,12,0,12)};
        opt.Click+=(_,_)=>{try{if(opt.IsChecked==true){Directory.CreateDirectory(Path.GetDirectoryName(UpdateOptIn)!);File.WriteAllText(UpdateOptIn,"enabled");}else if(File.Exists(UpdateOptIn))File.Delete(UpdateOptIn);}catch{opt.IsChecked=AutoUpdate;status.Text="設定を保存できませんでした。";}};
        CancellationTokenSource? operation=null;
        var check=new Button{Content="今すぐ確認",Margin=new Thickness(2),Padding=new Thickness(8,6,8,6)};
        var apply=new Button{Content="ダウンロードして更新",Margin=new Thickness(2),Padding=new Thickness(8,6,8,6)};
        var cancel=Btn("中止",()=>operation?.Cancel());cancel.IsEnabled=false;
        void Display(){apply.IsEnabled=availableUpdate!=null&&operation==null;history.Text=HistoryLabel();status.Text=availableUpdate==null?(HasUpdateNotice?$"前回確認した新版：{updateState.LatestTag}（再確認してください）":$"現在：{UpdateInstall.Current} preview"):$"新しい版：{availableUpdate.Tag}（約 {availableUpdate.Size/1_000_000} MB）";}
        async Task Run(Func<CancellationToken,Task> task)
        {
            if(operation!=null||updateBusy)return;
            using var ct=new CancellationTokenSource(TimeSpan.FromMinutes(10));operation=ct;updateBusy=true;
            check.IsEnabled=false;apply.IsEnabled=false;cancel.IsEnabled=true;opt.IsEnabled=false;
            try{await task(ct.Token);}
            catch(OperationCanceledException){status.Text="更新を中止しました。旧版は維持されています。";}
            catch(Exception e){status.Text="更新できませんでした。旧版を維持しています。 ("+e.GetType().Name+")";}
            finally{operation=null;updateBusy=false;check.IsEnabled=true;apply.IsEnabled=availableUpdate!=null;cancel.IsEnabled=false;opt.IsEnabled=true;progress.Visibility=Visibility.Collapsed;}
        }
        check.Click+=async (_,_)=>await Run(async ct=>{
            status.Text="確認中…";availableUpdate=null;var now=DateTimeOffset.UtcNow;
            try{using var client=new UpdateClient();availableUpdate=await client.Check(UpdateInstall.Current,ct);updateState=updateState.Succeeded(now,availableUpdate?.Tag??"");Display();if(availableUpdate==null)status.Text="現在の評価版が最新です。";}
            catch{updateState=updateState.Failed(now);throw;}
            finally{SaveHistory();history.Text=HistoryLabel();RefreshUpdateNotice();}
        });
        apply.Click+=async (_,_)=>{
            var offer=availableUpdate;if(offer==null)return;
            if(MessageBox.Show(w,"GitHubから評価版を取得し、検証後に再起動します。未署名の評価版です。学校の許可がある環境で実行してください。旧版は残ります。","更新の確認",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
            string? staged=null;
            await Run(async ct=>{progress.Value=0;progress.Visibility=Visibility.Visible;status.Text="取得・検証中…";using var client=new UpdateClient();staged=await client.Stage(offer,ct,new Progress<long>(n=>{progress.Value=Math.Min(100,n*100.0/offer.Size);status.Text=$"取得・検証中… {progress.Value:0}%";}));status.Text="検証完了。再起動します。";});
            if(staged!=null){PendingUpdate=staged;w.Close();Close();if(!closing)PendingUpdate=null;}
        };
        w.Closing+=(_,e)=>{if(operation!=null){operation.Cancel();e.Cancel=true;}};
        Add(panel,Text("KOMU DESK 更新",20,true),status,detail,history,opt,check,apply,progress,cancel,Text("更新は本人の確認後のみ適用します。自動確認OFFでも手動確認できます。旧版のフォルダは残ります。",12,false,Muted),Btn("閉じる",w.Close));
        Display();w.Content=new ScrollViewer{Content=panel};w.ShowDialog();
    }
}
