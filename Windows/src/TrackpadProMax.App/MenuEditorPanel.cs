using TrackpadProMax.Core;

namespace TrackpadProMax.App;
public sealed partial class MainWindow
{
    private void MenuEditorPanel()
    {
        var draft=ConfigurationStore.Deserialize(ConfigurationStore.Serialize(config)).Menu;
        var panel=new StackPanel();var tree=new TreeView{Height=245,Background=GlassTheme.Brush("FieldBrush"),BorderBrush=GlassTheme.Brush("LineBrush"),Foreground=GlassTheme.Brush("TextBrush"),Margin=new Thickness(0,0,0,12)};
        var label=new TextBox{MinWidth=160};var kind=new ComboBox{ItemsSource=new[]{"wheel","mode","keys","media","window","launch","url","lock","settings","touchpadsettings","taskmanager","brightness"},SelectedItem="wheel",Width=170};var value=new TextBox{MinWidth=220};
        MenuEntry? selected=null;List<MenuEntry>? owner=null;
        void Rebuild()
        {
            tree.Items.Clear();
            void Add(ItemsControl parent,List<MenuEntry> entries)
            {
                foreach(var entry in entries)
                {
                    var item=new TreeViewItem{Header=entry.Label+(entry.Children.Count>0?$"  /  {entry.Children.Count}":""),IsExpanded=true,Foreground=GlassTheme.Brush("TextBrush"),Tag=entry};
                    item.Selected+=(_,e)=>{e.Handled=true;selected=entry;owner=entries;label.Text=entry.Label;kind.SelectedItem=entry.Action.Kind;value.Text=entry.Action.Value;};
                    parent.Items.Add(item);Add(item,entry.Children);
                    if(ReferenceEquals(selected,entry))item.IsSelected=true;
                }
            }
            Add(tree,draft);
        }
        Rebuild();panel.Children.Add(tree);panel.Children.Add(Text("Seçili öğe: ad / eylem türü / değer (alt öğesi olan kategori yalnızca alt menü açar)",12));panel.Children.Add(Row(label,kind,value));
        panel.Children.Add(Row(Button("Öğeyi uygula",()=>{if(selected is null)return;selected.Label=label.Text.Trim();selected.Action=new(){Kind=(string)kind.SelectedItem,Value=value.Text.Trim()};Rebuild();}),Button("+ Ana öğe",()=>{var entry=new MenuEntry();draft.Add(entry);selected=entry;Rebuild();}),Button("+ Alt öğe",()=>{if(selected is null)return;var entry=new MenuEntry();selected.Children.Add(entry);selected=entry;Rebuild();})));
        void Move(int direction){if(selected is null || owner is null)return;int at=owner.IndexOf(selected),next=at+direction;if(next<0 || next>=owner.Count)return;owner.RemoveAt(at);owner.Insert(next,selected);Rebuild();}
        panel.Children.Add(Row(Button("Yukarı",()=>Move(-1)),Button("Aşağı",()=>Move(1)),Button("Seçileni kaldır",()=>{if(selected is null || owner is null)return;owner.Remove(selected);selected=null;Rebuild();}),Button("Menüyü kaydet",()=>
        {
            try {var candidate=ConfigurationStore.Deserialize(ConfigurationStore.Serialize(config));candidate.Menu=draft;candidate.Validate();config=candidate;if(SaveConfiguration())MenuPage();}catch(Exception ex){status.Text="Menü kaydedilemedi: "+ex.Message;}
        }),Button("Çarkı aç",OpenWheel)));
        panel.Children.Add(Text("Alanları değiştirdikten sonra ‘Öğeyi uygula’, ardından ‘Menüyü kaydet’ seçin. Kaydedilmemiş taslak çarkı etkilemez. En fazla 12 öğe / 5 alt katman.",12));page.Children.Add(Card(panel));
    }
}
