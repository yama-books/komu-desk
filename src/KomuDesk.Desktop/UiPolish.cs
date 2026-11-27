using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using KomuDesk.Core;

namespace KomuDesk.Desktop;
internal sealed partial class DeskWindow
{
    private string agendaSearch="";
    private string taskSearch="";
    private bool taskCompleted;
    private DateTime? countFrom,countTo;
    private static DataGrid Table()
    {
        var grid=new DataGrid{AutoGenerateColumns=false,CanUserAddRows=false,RowHeaderWidth=0,Background=White,Foreground=Ink,BorderBrush=Line,BorderThickness=new Thickness(1),RowBackground=White,AlternatingRowBackground=Paper,GridLinesVisibility=DataGridGridLinesVisibility.Horizontal,HorizontalGridLinesBrush=Line,MinRowHeight=34,EnableRowVirtualization=true,EnableColumnVirtualization=true,HeadersVisibility=DataGridHeadersVisibility.Column};
        var header=new Style(typeof(DataGridColumnHeader));header.Setters.Add(new Setter(Control.BackgroundProperty,Soft));header.Setters.Add(new Setter(Control.ForegroundProperty,Ink));header.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(9,8,9,8)));grid.ColumnHeaderStyle=header;
        return grid;
    }
    private sealed record ListEntry(AgendaRow Row,DateTime Date,string Time,string Source,string Title,string State,Brush Fill,Brush Accent);
    private FrameworkElement AgendaList(Action<Action> modal)
    {
        var root=new DockPanel();
        var top=new WrapPanel();var query=new TextBox{Text=agendaSearch,Width=220,MinHeight=32,Margin=new Thickness(3)};
        System.Windows.Automation.AutomationProperties.SetName(query,"予定検索");
        var count=Text("",12,false,Muted);Add(top,Glyph("filter"),query,Tool("close","検索クリア",()=>query.Clear(),false),count);
        DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);
        var grid=Table();grid.IsReadOnly=true;grid.SelectionMode=DataGridSelectionMode.Single;
        foreach(var (label,key,width) in new[]{("時刻","Time",75),("種類","Source",100),("内容","Title",0),("状態","State",105)})
        {var col=Col(label,key);col.Width=width==0?new DataGridLength(1,DataGridLengthUnitType.Star):new DataGridLength(width);if(key=="Source"){var style=new Style(typeof(DataGridCell));style.Setters.Add(new Setter(Control.BackgroundProperty,new Binding("Fill")));style.Setters.Add(new Setter(Control.ForegroundProperty,new Binding("Accent")));col.CellStyle=style;}grid.Columns.Add(col);}
        VirtualizingPanel.SetIsVirtualizingWhenGrouping(grid,true);VirtualizingPanel.SetVirtualizationMode(grid,VirtualizationMode.Recycling);
        var header=new FrameworkElementFactory(typeof(TextBlock));header.SetBinding(TextBlock.TextProperty,new Binding("Name"){StringFormat="{0:yyyy年M月d日 (ddd)}",ConverterCulture=System.Globalization.CultureInfo.GetCultureInfo("ja-JP")});header.SetValue(TextBlock.FontWeightProperty,FontWeights.SemiBold);header.SetValue(TextBlock.PaddingProperty,new Thickness(9,9,9,5));header.SetValue(TextBlock.BackgroundProperty,Soft);
        var template=new DataTemplate();template.VisualTree=header;grid.GroupStyle.Add(new GroupStyle{HeaderTemplate=template});
        var all=Agenda.Range(agendaFrom,agendaTo).SelectMany(d=>Rows(d)).Select(r=>new ListEntry(r,r.Day,r.Time,SourceNames[Array.IndexOf(SourceIds,r.Source)],r.Title,StateText(r),ToneFor(r).Fill,ToneFor(r).Accent)).ToArray();
        void Fill()
        {
            agendaSearch=query.Text;
            var rows=all.Where(r=>r.Title.Contains(agendaSearch,StringComparison.CurrentCultureIgnoreCase)).ToArray();
            var view=new ListCollectionView(rows);view.GroupDescriptions.Add(new PropertyGroupDescription("Date"));grid.ItemsSource=view;count.Text=rows.Length==0?"表示する予定はありません。":rows.Length+"件";
        }
        void Detail(){if(grid.SelectedItem is ListEntry item)modal(()=>OpenAgendaRow(item.Row));}
        var footer=new WrapPanel();Add(footer,Tool("edit","詳細を開く",Detail),Text("日付ごとに表示 · Google予定は読み取り専用",12,false,Muted));DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        grid.MouseDoubleClick+=(_,_)=>Detail();grid.KeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Enter){e.Handled=true;Detail();}};
        query.TextChanged+=(_,_)=>Fill();Fill();root.Children.Add(grid);return root;
    }
    private FrameworkElement WeekTimeline(Action refresh,Action<Action> modal)
    {
        var start=Agenda.WeekStart(agendaDate);
        var dates=Enumerable.Range(0,7).Select(i=>start.AddDays(i)).ToArray();
        var rows=dates.Select(d=>Rows(d)).ToArray();
        var all=rows.SelectMany(r=>r).Where(r=>r.StartMinute.HasValue).ToArray();
        int lo=Math.Min(8,all.Select(r=>r.StartMinute!.Value/60).DefaultIfEmpty(8).Min());
        int hi=Math.Max(18,all.Select(r=>(Math.Max(r.EndMinute??r.StartMinute!.Value,r.StartMinute!.Value+45)+59)/60).DefaultIfEmpty(18).Max());hi=Math.Min(24,hi);
        const double hour=56;const double dayWidth=128;const double rulerWidth=52;
        var root=new StackPanel();Add(root,Text(DayText(start)+" ～ "+DayText(start.AddDays(6)),17,true),Text("時刻のある予定を時間軸に表示します。終了時刻のない予定は開始マークです。",11,false,Muted));
        var g=new Grid();g.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(rulerWidth)});
        for(int i=0;i<7;i++)g.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(dayWidth)});
        for(int i=0;i<3;i++)g.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        void Put(UIElement element,int row,int col){Grid.SetRow(element,row);Grid.SetColumn(element,col);g.Children.Add(element);}
        Put(Text("終日等",12,true,Muted),1,0);
        var ruler=new Canvas{Height=(hi-lo)*hour,Width=rulerWidth};
        for(int h=lo;h<hi;h++){var label=Text(h.ToString("00")+":00",11,false,Muted);Canvas.SetTop(label,(h-lo)*hour);ruler.Children.Add(label);}Put(ruler,2,0);
        for(int i=0;i<7;i++)
        {
            var d=dates[i];var select=WidgetButton(DayText(d),DayText(d),()=>{agendaDate=d;refresh();});select.Margin=new Thickness(2);if(d==agendaDate){select.BorderBrush=Ink;select.Background=Soft;}Put(select,0,i+1);
            var floating=new StackPanel{Margin=new Thickness(2)};
            foreach(var r in rows[i].Where(r=>!r.StartMinute.HasValue)){var b=WidgetButton(Text(r.Time+" "+r.Title,11),r.Title+" "+StateText(r),()=>modal(()=>OpenAgendaRow(r)));PaintAgenda(b,ToneFor(r));b.Margin=new Thickness(0,1,0,1);floating.Children.Add(b);}
            Put(floating,1,i+1);
            var canvas=new Canvas{Height=(hi-lo)*hour,Width=dayWidth,Background=d==DateTime.Today?Soft:White,ClipToBounds=true};
            for(int h=lo;h<hi;h++){var line=new Border{Width=dayWidth,Height=1,Background=Line};Canvas.SetTop(line,(h-lo)*hour);canvas.Children.Add(line);}
            foreach(var item in Agenda.Place(rows[i]))
            {
                var r=item.Row;double width=(dayWidth-4)/item.Lanes;
                var p=new StackPanel();var time=r.Time+(r.EndMinute.HasValue?"":" 開始");var t=Text(time,10,true);t.TextWrapping=TextWrapping.NoWrap;Add(p,t,Text(r.Title,11));
                var b=WidgetButton(p,r.Title+" "+r.Time+(r.EndMinute.HasValue?" ～ "+TimeSpan.FromMinutes(r.EndMinute.Value).ToString(@"hh\:mm"):"（終了時刻なし）")+" "+StateText(r),()=>modal(()=>OpenAgendaRow(r)));
                PaintAgenda(b,ToneFor(r));b.Margin=new Thickness(0);b.Padding=new Thickness(3,1,3,1);b.MinWidth=0;b.MinHeight=0;b.Width=Math.Max(1,width-2);b.Height=Math.Max(1,(item.End-item.Start)*hour/60-2);b.HorizontalContentAlignment=HorizontalAlignment.Left;b.ClipToBounds=true;
                Canvas.SetLeft(b,2+item.Lane*width);Canvas.SetTop(b,(item.Start-lo*60)*hour/60);canvas.Children.Add(b);
            }
            Put(canvas,2,i+1);
        }
        root.Children.Add(new ScrollViewer{Content=g,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled});
        root.Children.Add(DayDetail(agendaDate,modal));return PageScroll(root);
    }
}
