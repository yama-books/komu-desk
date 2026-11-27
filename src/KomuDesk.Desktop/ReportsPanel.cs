using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using KomuDesk.Core;
using Microsoft.Win32;

namespace KomuDesk.Desktop;
internal sealed partial class DeskWindow
{
    private void Reports()
    {
        var w=Dialog("授業日一覧・帳票",930,580);
        w.Content=BuildReportsContent(w);w.ShowDialog();
    }
    private FrameworkElement BuildReportsContent(Window w)
    {
        var p=new DockPanel{Margin=new Thickness(8)};
        var top=new StackPanel();DockPanel.SetDock(top,Dock.Top);
        var bar=new WrapPanel();var start=new DatePicker{SelectedDate=SchoolData.ParseDate(data.YearFrom)};var end=new DatePicker{SelectedDate=SchoolData.ParseDate(data.YearTo)};
        var groups=new ComboBox{ItemsSource=new[]{new Choice("","すべて")}.Concat(GroupChoices()).ToArray(),SelectedValuePath="Id",DisplayMemberPath="Label",SelectedIndex=0,MinWidth=160};
        var grid=Table();grid.IsReadOnly=true;
        foreach(var (label,key) in new[]{("日付","Date"),("時限","Period"),("授業","Group"),("種別","Kind"),("状態","State"),("回次","Number"),("メモ・理由","Note")})grid.Columns.Add(Col(label,key));
        string? Group()=>groups.SelectedValue is string id&&id!=""?id:null;
        (DateTime,DateTime) Range()=>(start.SelectedDate??throw new InvalidOperationException("開始日を選択してください。"),end.SelectedDate??throw new InvalidOperationException("終了日を選択してください。"));
        void Refresh(){try{var(a,b)=Range();grid.ItemsSource=LessonReports.Lines(data,a,b,DateTime.Now,Group());}catch(Exception ex){Error(ex);}}
        void Export()
        {
            try
            {
                var(a,b)=Range();var tables=LessonReports.Tables(data,a,b,DateTime.Now,Group());
                var dlg=new SaveFileDialog{Filter="Excel (*.xlsx)|*.xlsx",FileName="komu-report-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".xlsx",OverwritePrompt=false};
                if(dlg.ShowDialog(w)!=true)return;
                LessonReports.WriteXlsx(dlg.FileName,tables);MessageBox.Show(w,"ローカルにExcel帳票を保存しました。");
            }catch(Exception ex){Error(ex);}
        }
        void Print()
        {
            try
            {
                var(a,b)=Range();var tables=LessonReports.Tables(data,a,b,DateTime.Now,Group());
                var dlg=new PrintDialog();if(dlg.ShowDialog()!=true)return;
                var doc=new FlowDocument{FontFamily=FontFamily,FontSize=10,PageWidth=dlg.PrintableAreaWidth,PageHeight=dlg.PrintableAreaHeight,PagePadding=new Thickness(28),ColumnWidth=Math.Max(100,dlg.PrintableAreaWidth-56)};
                doc.Blocks.Add(new Paragraph(new Run("KOMU DESK 授業帳票")){FontSize=17,FontWeight=FontWeights.Bold});
                doc.Blocks.Add(new Paragraph(new Run($"{a:yyyy-MM-dd} ～ {b:yyyy-MM-dd} / 推定は未確定")));
                foreach(var t in tables)
                {
                    doc.Blocks.Add(new Paragraph(new Run(t.Name)){FontWeight=FontWeights.Bold});
                    var table=new Table{CellSpacing=0};foreach(var h in t.Headers)table.Columns.Add(new TableColumn());
                    var group=new TableRowGroup();table.RowGroups.Add(group);
                    void Row(IEnumerable<object?> vals,bool head)
                    {
                        var row=new TableRow();foreach(var val in vals)row.Cells.Add(new TableCell(new Paragraph(new Run(val?.ToString()??"")){Margin=new Thickness(2)}){BorderBrush=Brushes.Gray,BorderThickness=new Thickness(.5),FontWeight=head?FontWeights.Bold:FontWeights.Normal});
                        group.Rows.Add(row);
                    }
                    Row(t.Headers,true);foreach(var row in t.Rows)Row(row,false);doc.Blocks.Add(table);
                }
                dlg.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator,"KOMU DESK 授業帳票");
            }catch(Exception ex){Error(ex);}
        }
        Add(bar,start,Text(" ～ "),end,groups,Btn("表示",Refresh));Add(top,Text("授業日一覧・帳票",22,true),bar,Text("回次は選択期間内。考査計上設定に従います。印刷は選択したWindowsプリンターを使用します。",12,false,Muted));
        var footer=new WrapPanel();DockPanel.SetDock(footer,Dock.Bottom);Add(footer,Tool("spreadsheet","Excel出力",Export),Tool("printer","印刷・PDFプリンター",Print));Add(p,top,footer,grid);Refresh();return p;
    }
    private void Restore()
    {
        if(demo){MessageBox.Show(this,"デモでは復元しません。");return;}
        var dlg=new OpenFileDialog{Filter="KOMU DESK JSON (*.json)|*.json",CheckFileExists=true};
        if(dlg.ShowDialog(this)!=true)return;
        try
        {
            if(new FileInfo(dlg.FileName).Length>20_000_000)throw new InvalidDataException("復元ファイルが大きすぎます。");
            var candidate=new LocalStore(dlg.FileName).Load(DateTime.Today);
            var msg=$"年度 {candidate.YearFrom} ～ {candidate.YearTo}\n授業グループ {candidate.Groups.Count} / 実績 {candidate.Actuals.Count} / 予定 {candidate.Events.Count} / タスク {candidate.Tasks.Count}\n現在の校務データを置き換えます。直前データは保存します。Google認証・キャッシュは別管理です。";
            if(MessageBox.Show(this,msg,"ローカル復元の確認",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
            if(Commit(candidate)){Topmost=candidate.Display.Topmost;day=DateTime.Today;Render();}
        }catch(Exception ex){Error(ex);}
    }
}
