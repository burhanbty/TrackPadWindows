using Microsoft.Win32;
using TrackpadProMax.Core;

namespace TrackpadProMax.App;
public sealed partial class MainWindow
{
    private bool CommitGrid(DataGrid grid)
    {
        if(grid.CommitEdit(DataGridEditingUnit.Cell,true) && grid.CommitEdit(DataGridEditingUnit.Row,true))return true;
        status.Text="Hücrede geçersiz değer var; düzeltmeden kaydedilemez.";return false;
    }
    private void BindingComposer()
    {
        var body=new StackPanel();body.Children.Add(Text("Kolay eşleme oluştur",20));
        var name=new TextBox{Text="Yeni hareket",MinWidth=180};
        var shortcut=new TextBox{Text="Ctrl+Alt+F9",MinWidth=150};
        var input=new ComboBox{ItemsSource=Enum.GetValues<InputRoute>(),SelectedItem=InputRoute.Shortcut,Width=145};
        var gesture=new ComboBox{ItemsSource=Enum.GetValues<GestureKind>(),SelectedItem=GestureKind.Tap,Width=140};
        var fingers=new ComboBox{ItemsSource=new[]{1,2,3,4},SelectedItem=3,Width=65};
        var scope=new ComboBox{ItemsSource=new[]{"Genel"}.Concat(config.Profiles.Select(p=>p.Name)).ToArray(),SelectedIndex=0,MinWidth=180};
        var mode=new TextBox{Text="*",Width=130};
        var kind=new ComboBox{ItemsSource=new[]{"wheel","launch","url","media","window","keys","mode","settings","touchpadsettings","taskmanager","lock","brightness"},SelectedItem="wheel",Width=170};
        var value=new TextBox{MinWidth=250};
        var presets=new ComboBox{MinWidth=200};
        void Presets()
        {
            presets.ItemsSource=(string)kind.SelectedItem switch{"media"=>new[]{"play","next","previous","up","down","mute"},"window"=>new[]{"left","right","maximize","restore","minimize","tile2","tile3","tile4"},"mode"=>new[]{"Normal","Surface","Media","Control","Windows","Applications","Quick Settings"},"brightness"=>new[]{"10","-10"},_=>Array.Empty<string>()};
            presets.SelectedIndex=presets.Items.Count>0?0:-1;value.Text=presets.SelectedItem as string??"";
        }
        kind.SelectionChanged+=(_,_)=>Presets();presets.SelectionChanged+=(_,_)=>{if(presets.SelectedItem is string text)value.Text=text;};Presets();
        body.Children.Add(Text("Ad / kısayol / giriş yolu",12));body.Children.Add(Row(name,shortcut,input));
        body.Children.Add(Text("Hareket / parmak / kapsam / etkin mod (* = tümü)",12));body.Children.Add(Row(gesture,fingers,scope,mode));
        body.Children.Add(Text("Eylem / hazır değer / değer veya .exe yolu",12));body.Children.Add(Row(kind,presets,value));
        var feedback=Text("Yerel tap: yalnızca 3/4 parmak. Yerel swipe: ön plan API teslimine bağlı. DoubleTap/LongPress: kısayol yolunu kullanın.",12);body.Children.Add(feedback);
        body.Children.Add(Row(Button(".exe seç",()=>{var dialog=new OpenFileDialog{Filter="Uygulama|*.exe"};if(dialog.ShowDialog()==true){kind.SelectedItem="launch";value.Text=dialog.FileName;}}),Button("Eşlemeyi kaydet",()=>
        {
            try
            {
                var binding=new Binding{Name=name.Text.Trim(),Shortcut=shortcut.Text.Trim(),Input=(InputRoute)input.SelectedItem,Gesture=(GestureKind)gesture.SelectedItem,Fingers=(int)fingers.SelectedItem,Mode=mode.Text.Trim(),Action=new(){Kind=(string)kind.SelectedItem,Value=value.Text.Trim()}};
                if(binding.UsesNative && (binding.Fingers<2 || binding.Gesture is GestureKind.DoubleTap or GestureKind.LongPress || (binding.Gesture==GestureKind.Tap && binding.Fingers<3)))
                    throw new ArgumentException("Bu yerel hareket sağlanmıyor. Shortcut yolunu seçip Windows'ta eşleyin.");
                var candidate=ConfigurationStore.Deserialize(ConfigurationStore.Serialize(config));
                if(scope.SelectedIndex<=0)candidate.Bindings.Add(binding);else candidate.Profiles[scope.SelectedIndex-1].Bindings.Add(binding);
                candidate.Validate();config=candidate;if(SaveConfiguration()){GesturesPage();status.Text="Eşleme kaydedildi. "+registrationStatus;}
            }catch(Exception ex){feedback.Text="Kaydedilemedi: "+ex.Message;}
        })));
        page.Children.Add(Card(body));
    }
}
