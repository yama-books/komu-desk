using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace KomuDesk.Desktop;
internal sealed partial class DeskWindow
{
    // Original, conventional 24-unit geometric UI symbols. Not a redraw of the cat art.
    private static readonly Dictionary<string,string> GlyphPaths=new()
    {
        ["home"]="M3,11 L12,3 21,11 M5,10 V21 H10 V15 H14 V21 H19 V10",
        ["calendar-month"]="M3,5 H21 V21 H3 Z M7,3 V7 M17,3 V7 M3,10 H21 M9,10 V21 M15,10 V21 M3,15 H21",
        ["calendar-week"]="M3,5 H21 V21 H3 Z M7,3 V7 M17,3 V7 M3,10 H21 M8,10 V21 M12,10 V21 M16,10 V21",
        ["calendar-day"]="M3,5 H21 V21 H3 Z M7,3 V7 M17,3 V7 M3,10 H21 M8,14 H16 M8,18 H16",
        ["calendar-list"]="M3,5 H21 V21 H3 Z M7,3 V7 M17,3 V7 M3,10 H21 M7,14 H8 M11,14 H18 M7,18 H8 M11,18 H18",
        ["calendar-today"]="M3,5 H21 V21 H3 Z M7,3 V7 M17,3 V7 M3,10 H21 M12,14 L12,17 M10.5,15.5 H13.5",
        ["chevron-left"]="M15,5 L8,12 15,19",["chevron-right"]="M9,5 L16,12 9,19",
        ["filter"]="M3,4 H21 L14,12 V20 L10,18 V12 Z",
        ["sync"]="M20,9 A8,8 0 0 0 6,5 L3,9 M3,3 V9 H9 M4,15 A8,8 0 0 0 18,19 L21,15 M15,15 H21 V21",
        ["add"]="M12,4 V20 M4,12 H20",["edit"]="M4,16 L16,4 20,8 8,20 3,21 Z M14,6 L18,10",
        ["trash"]="M4,6 H20 M9,6 V3 H15 V6 M6,6 L7,21 H17 L18,6 M10,10 V17 M14,10 V17",
        ["save"]="M4,3 H17 L21,7 V21 H3 V3 Z M7,3 V10 H16 V3 M7,21 V15 H17 V21",
        ["check"]="M4,12 L10,18 20,6",["close"]="M6,6 L18,18 M18,6 L6,18",
        ["minimize"]="M5,12 H19",["maximize"]="M5,5 H19 V19 H5 Z",
        ["window-restore"]="M8,8 H20 V20 H8 Z M4,16 V4 H16",
        ["pin"]="M9,3 H15 L14,10 18,14 H6 L10,10 Z M12,14 V22",
        ["pin-off"]="M9,3 H15 L14,10 18,14 H6 L10,10 Z M12,14 V22 M3,3 L21,21",
        ["compact"]="M3,5 H21 V19 H3 Z M3,10 H21",
        ["expand"]="M4,10 V4 H10 M14,4 H20 V10 M20,14 V20 H14 M10,20 H4 V14",
        ["menu"]="M4,6 H20 M4,12 H20 M4,18 H20"
    };
    private static FrameworkElement Glyph(string id,double size=18)
    {
        if(!GlyphPaths.TryGetValue(id,out var path))return AssetIcon(id,size);
        var shape=new System.Windows.Shapes.Path{Data=Geometry.Parse(path),Stroke=Ink,StrokeThickness=1.8,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round};
        var canvas=new Canvas{Width=24,Height=24};canvas.Children.Add(shape);
        return new Viewbox{Width=size,Height=size,Child=canvas,IsHitTestVisible=false};
    }
    private static FrameworkElement GlyphText(string id,string label,double size=17)
    {
        var p=Horizontal();var icon=Glyph(id,size);icon.Margin=new Thickness(0,0,5,0);Add(p,icon,Text(label,12));return p;
    }
    private static Button Tool(string id,string label,Action action,bool text=true)
    {
        var b=WidgetButton(text?GlyphText(id,label):Glyph(id),label,action);
        b.MinHeight=32;b.MinWidth=30;b.Margin=new Thickness(2);b.Padding=new Thickness(7,4,7,4);return b;
    }
}
