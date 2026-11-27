using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace KomuDesk.Desktop;
internal sealed partial class DeskWindow
{
    internal string? PendingUpdate {get;private set;}
    private UpdateOffer? availableUpdate;
    private bool updateBusy, updateNotice;
    private static string UpdateOptIn=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"KOMU DESK","update-enabled");
    private bool AutoUpdate=>!demo&&File.Exists(UpdateOptIn);
    private void InitUpdates()
    {
        if(demo||!AutoUpdate)return;
        Loaded+=async (_,_)=>{await CheckUpdates(false);};
    }
    private async Task CheckUpdates(bool showError)
    {
        if(demo||updateBusy)return;
        updateBusy=true;
        try
        {
            using var client=new UpdateClient();using var ct=new CancellationTokenSource(TimeSpan.FromSeconds(30));
            availableUpdate=await client.Check(UpdateInstall.Current,ct.Token);
            if(availableUpdate!=null&&!updateNotice)
            {
                updateNotice=true;
                if(!closing&&IsLoaded&&MessageBox.Show(this,$"新しい評価版 {availableUpdate.Tag} が利用できます。更新画面を開きますか？","KOMU DESK 更新",MessageBoxButton.YesNo)==MessageBoxResult.Yes)
                    Dispatcher.BeginInvoke(new Action(Updates));
            }
            else if(showError&&availableUpdate==null)MessageBox.Show(this,"現在の評価版が最新です。");
        }
        catch{if(showError)MessageBox.Show(this,"更新を確認できませんでした。現在の版はそのまま利用できます。");}
        finally{updateBusy=false;}
    }
    private void Updates()
    {
        if(demo){MessageBox.Show(this,"デモでは通信しません。");return;}
        var w=Dialog("アプリのアップデート",520,430);
        var panel=new StackPanel{Margin=new Thickness(16)};
        var status=Text($"現在：{UpdateInstall.Current} preview",14,true);
        var detail=Text("GitHub Releasesの公開評価版を確認します。校務データやGoogle認証情報は送信しません。",13,false,Muted);
        var progress=new ProgressBar{Height=7,Minimum=0,Maximum=100,Visibility=Visibility.Collapsed,Margin=new Thickness(0,6,0,6)};
        var opt=new CheckBox{Content="起動時に新しい版を自動確認する",IsChecked=AutoUpdate,Margin=new Thickness(0,12,0,12)};
        opt.Click+=(_,_)=>{try{if(opt.IsChecked==true){Directory.CreateDirectory(Path.GetDirectoryName(UpdateOptIn)!);File.WriteAllText(UpdateOptIn,"enabled");}else if(File.Exists(UpdateOptIn))File.Delete(UpdateOptIn);}catch{opt.IsChecked=AutoUpdate;status.Text="設定を保存できませんでした。";}};
        CancellationTokenSource? operation=null;
        var check=new Button{Content="今すぐ確認",Margin=new Thickness(2),Padding=new Thickness(8,6,8,6)};
        var apply=new Button{Content="ダウンロードして更新",Margin=new Thickness(2),Padding=new Thickness(8,6,8,6)};
        var cancel=Btn("中止",()=>operation?.Cancel());cancel.IsEnabled=false;
        void Display(){apply.IsEnabled=availableUpdate!=null&&operation==null;status.Text=availableUpdate==null?$"現在：{UpdateInstall.Current} preview":$"新しい版：{availableUpdate.Tag}（約 {availableUpdate.Size/1_000_000} MB）";}
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
        check.Click+=async (_,_)=>await Run(async ct=>{status.Text="確認中…";using var client=new UpdateClient();availableUpdate=await client.Check(UpdateInstall.Current,ct);Display();if(availableUpdate==null)status.Text="現在の評価版が最新です。";});
        apply.Click+=async (_,_)=>{
            var offer=availableUpdate;if(offer==null)return;
            if(MessageBox.Show(w,"GitHubから評価版を取得し、検証後に再起動します。未署名の評価版です。学校の許可がある環境で実行してください。旧版は残ります。","更新の確認",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
            string? staged=null;
            await Run(async ct=>{progress.Value=0;progress.Visibility=Visibility.Visible;status.Text="取得・検証中…";using var client=new UpdateClient();staged=await client.Stage(offer,ct,new Progress<long>(n=>progress.Value=Math.Min(100,n*100.0/offer.Size)));status.Text="検証完了。再起動します。";});
            if(staged!=null){PendingUpdate=staged;w.Close();Close();if(!closing)PendingUpdate=null;}
        };
        w.Closing+=(_,e)=>{if(operation!=null){operation.Cancel();e.Cancel=true;}};
        Add(panel,Text("KOMU DESK 更新",20,true),status,detail,opt,check,apply,progress,cancel,Text("更新は本人の確認後のみ適用します。自動確認OFFでも手動確認できます。旧版のフォルダは残ります。",12,false,Muted),Btn("閉じる",w.Close));
        Display();w.Content=new ScrollViewer{Content=panel};w.ShowDialog();
    }
}
