using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KomuDesk.Desktop;
internal sealed partial class DeskWindow
{
    private static readonly Dictionary<string,BitmapSource> IconCache=new(StringComparer.Ordinal);
    internal static Image AssetIcon(string id,double size=24)
    {
        if(!IconCache.TryGetValue(id,out var image))
        {
            var bitmap=new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource=new Uri("pack://application:,,,/KOMU-DESK;component/Assets/Icons/"+id+".png");
            bitmap.CacheOption=BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            IconCache[id]=image=bitmap;
        }
        return new Image{Source=image,Width=size,Height=size,Stretch=Stretch.Uniform,IsHitTestVisible=false};
    }
    private static FrameworkElement IconText(string id,string label,double size=17)
    {
        var row=Horizontal();row.VerticalAlignment=VerticalAlignment.Center;
        var icon=AssetIcon(id,size);icon.Margin=new Thickness(0,0,4,0);
        Add(row,icon,Text(label,12));return row;
    }
    private static Button Tile(string id,string label,string hint,Action action)
    {
        var icon=AssetIcon(id,29);icon.Margin=new Thickness(0,0,0,5);
        var name=Text(label,13,true);name.TextAlignment=TextAlignment.Center;
        var sub=Text(hint,10,false,Muted);sub.TextAlignment=TextAlignment.Center;
        var stack=new StackPanel{HorizontalAlignment=HorizontalAlignment.Stretch,VerticalAlignment=VerticalAlignment.Center};
        Add(stack,icon,name,sub);
        var button=WidgetButton(stack,label+"："+hint,action);
        button.Padding=new Thickness(8,10,8,10);
        button.MinHeight=86;button.Margin=new Thickness(4);
        button.HorizontalContentAlignment=HorizontalAlignment.Stretch;
        return button;
    }
    internal FrameworkElement BuildSettingsContent(Action<Action> open,Action close)
    {
        var panel=new StackPanel{Margin=new Thickness(16)};
        var header=new DockPanel{Margin=new Thickness(4,0,4,10)};
        var cat=AssetIcon("cat",48);DockPanel.SetDock(cat,Dock.Right);header.Children.Add(cat);
        var title=new StackPanel();Add(title,Text("KOMU DESK",12,true,Muted),Text("設定・管理",23,true),Text("使いたい機能を選んでください。",12,false,Muted));header.Children.Add(title);panel.Children.Add(header);
        void Group(string name,params (string Icon,string Label,string Hint,Action Action)[] items)
        {
            var heading=Text(name,13,true,Muted);heading.Margin=new Thickness(4,8,4,4);panel.Children.Add(heading);
            var tiles=new UniformGrid{Columns=3};
            foreach(var item in items)tiles.Children.Add(Tile(item.Icon,item.Label,item.Hint,()=>open(item.Action)));
            panel.Children.Add(tiles);
        }
        Group("日常業務",
            ("calendar","予定","ローカル予定",Events),
            ("timetable","時間割","週間編集",Weekly),
            ("tasks","タスク","やること",Tasks),
            ("notebook","日別変更","休講・授業詳細",DayChanges),
            ("lesson-count","授業数","期間・回数",Counts),
            ("printer","帳票","一覧・印刷",Reports));
        Group("授業設定",
            ("classes","学年・色","学年バッジ",Grades),
            ("classes","クラス・色","クラスバッジ",Classes),
            ("notebook","科目","科目名",Subjects),
            ("classes","担当授業","科目と組",Groups),
            ("bell","時程","開始・終了",Bells),
            ("timetable","時間割の版","適用期間",Versions));
        Group("環境・データ",
            ("appearance","年度・表示","考査・外観",Year),
            ("connection","Google連携","読み取り専用",GoogleSettings),
            ("spreadsheet","Excel取込","初期データ",ImportExcel),
            ("backup","バックアップ","ローカル保存",Backup),
            ("restore","復元","バックアップ読込",Restore),
            ("update","アップデート",HasUpdateNotice?"新版あり":"バージョン確認",Updates),
            ("cat","アイコン","10種類から選ぶ",TraySettings));
        var footer=WidgetButton("閉じる","設定を閉じる",close);footer.Margin=new Thickness(4,12,4,4);panel.Children.Add(footer);
        return new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
    }
}
