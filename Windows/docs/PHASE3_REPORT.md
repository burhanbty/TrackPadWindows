# Phase 3 — Advanced Surface Controls

Tarih: 1 Kasım 2026, Europe/Istanbul. Windows uygulaması yerinde genişletildi;
`upstream/` değiştirilmedi. Sürücü/güvenlik/Windows hareket ayarı değiştirilmedi.
Bu kaynak kaydı mevcut ilerlemeyi kapsar; fiziksel eylem kabulü tamamlanmış sayılmaz.

## Uygulanan işlevler

* Sekiz bağımsız fiziksel bölge: sol/sağ/üst/alt kenar ve dört köşe. Her bölgenin
  genişliği/yüksekliği, etkinliği, hareket ekseni, iki yön eylemi ve tekrar davranışı
  düzenlenebilir. Köşeler önceliklidir; kapalı köşe kenara dönüşmez.
* Varsayılanlar: sol parlaklık, sağ ses, üst önceki/sonraki parça, alt zaman
  çizelgesi. Köşeler: oynat/duraklat, sessiz, çark, Normal mod. Bunlar sabit
  sınırlamalar değildir; mevcut uygulama eylemleri bölgelere atanabilir.
* `Surface Mode`: Liquid Glass arayüzü, gerçek temas haritası, kilitlenen çapa,
  bölge/yön/eylem sonucu. Kullanıcı açıkça etkinleştirir. Esc, odak kaybı,
  sayfa/mod değişimi, duraklatma veya kapatma oturumu sonlandırır. Açık oturum
  uygulama yeniden başlatılınca kendiliğinden devam etmez.
* Bölgedeki en dış temas kilitlenir; ek parmaklar merkezde durabilir. Temas
  kimliği değişimi, bölge dışına çıkma, aşırı çapraz hareket, sıçrama ve zaman
  aşımı iptal edilir. Sıralı parmak kaldırma çapa sıçraması yaratmaz. Sonraki
  komut için bütün parmaklar kaldırılmalıdır.
  İsteğe bağlı esnek başlangıç, seçilen asgari sayıdan dört parmağa kadar kabul
  eder; eylem başlamadan eklenen parmaklar için denge süresi yeniden başlar.
  Eylem başladıktan sonraki yeni parmak iptal edilir. Temas sayısı azalınca
  kaldırma/ayrışma kaybı açıkça gösterilir ve kontrol devam ettirilmez.
* Hareket eşiği, başlangıç süresi, hassasiyet, tekrar adımı/aralığı ve güvenlik
  sınırları ayarlanabilir. Windows'un yeniden oluşturduğu ilk down çerçevesinden
  sonraki güncelleme aynı bölge içinde bir kez dengelenir; ilk sıçrama eylem
  patlaması oluşturmaz. Boş fare yakalama iptali sonraki gerçek hareketi tüketmez.
* Sürekli ses, mevcut Windows ses uç noktasına yüzde puanı farkı uygular.
  Parlaklık OEM WMI kullanır. Scrubbing, oynatıcı destekliyorsa Windows medya
  oturumunun aranabilir zaman aralığında saniye farkı uygular. Desteksiz işlem
  açık hata verir. Hızlı seek güncellemeleri kabul edilen konumdan biriktirilir.
* Görsel bölge düzenleyicisi Hareketler sayfasından erişilir. Sürüm-1 ayarlar
  geriye uyumludur; eski dosyaya Surface varsayılanları eklenir, mevcut eşlemeler,
  profiller ve menüler korunur. Atomik kayıt ve `.bak` devam eder.
* Ayrı Bölge Laboratuvarı: sekiz yönlendirmeli fiziksel test, parmak sayısı seçimi,
  gerçek temaslar, bölge/yön, önizleme ve ayrıca açılan gerçek eylem çalıştırma.
  Yüksek frekanslı temaslar kritik tanıma/eylem kayıtlarını silemesin diye ayrı
  sınırlı tanıma günlüğü tutulur. JSON dışa aktarım ve gözlem CLI'si mevcuttur.

## Fiziksel kanıt

Donanım: MSI Katana 17 B13VGK, Windows 26300 x64. Kullanıcı fiziksel olarak iki
parmak kaydırma, üç parmak kaydırma ve üç parmak dokunma denemeleri yaptı.

* İlk başarılı üç parmak oturumunda 449 yerel çerçeve, 444 koordinatlı çerçeve,
  1202 controller callback, 83 WM_POINTER mesajı ve **0 okuma hatası** kaydedildi.
  Üç parmak dokunuşu ayrı `GlobalActionPerformed / ThreeFingerTap` olarak alındı;
  bu olayın temas koordinatı yoktur ve köşe dokunuşu sayılmadı.
* Sonraki kayıtlarda `WM_POINTER` üzerinden gerçek **iki parmak** koordinatları
  da görüldü. Her iki ön plan yolunda fiziksel temas teslimi doğrulanmıştır.
* İlk sağ kenar denemesinde bölge başlangıcı alındı, fakat çapraz hareket
  koruması iptal etti; kullanıcı sesin değişmediğini bildirdi. Başlangıç dengesi
  ve test başlatma davranışı bu bulgu üzerine düzeltildi. Bu başarısız deneme
  başarılı fiziksel ses kontrolü olarak sayılmamıştır.
* Sonraki denemede 1292 çerçeve / 1287 koordinatlı çerçeve ve 0 okuma hatası
  kaydedildi; kullanıcı temasların birbirine yaklaşınca kaybolduğunu ve sesin
  değişmediğini bildirdi. Akışta 2→1 temas azalması görüldü. Sensör birleştirmesi
  ile Windows ayrıştırmasını bu kanıt tek başına ayıramaz. Esnek 2–4 parmak
  başlangıcı ve görünür temas azalması bildirimi bu bulgu üzerine eklendi.
* Esnek başlangıçlı yayımlanmış sürümde gerçek sağ kenar yukarı/aşağı
  tetiklemeleri alındı. Sesin fiziksel olarak değişmesi kullanıcı tarafından
  doğrulanmadı; sağ kenar tanıması ile ses eyleminin kabulü ayrı tutulmaktadır.
  Son arayüzde önizleme ve gerçek eylem testi ayrı düğmelere ayrıldı; doğrudan
  kullanım düğmesi “Yüzey kontrolünü başlat” olarak açıklandı.

Yerel kanıtlar `artifacts/phase3-physical-acceptance-20261101.json`,
`artifacts/phase3-physical-zones-before-initial-fix.json` ve
`artifacts/phase3-physical-zones-final.json` ve
`artifacts/phase3-published-physical-acceptance.json` içindedir. Bunlar cihaz/temas
bilgileri içerir; kaynak depoya eklenmez. Sentetik testler bu sonuçların yerine
geçmez. Basınç/boyut/açı null bırakıldı; fare imleci koordinatı kullanılmadı.

Tek parmak konumu, konumlu köşe dokunuşu/uzun basış ve sınırsız arka plan ham
temas erişimi mevcut standart yollarda desteklenmez. Dört parmak, diğer kenar ve
köşelerin her biri, canlı parlaklık/oynatıcı etkisi ayrıca fiziksel kabul ister.
Üç/dört parmak controller'ı arka planda Windows tarafından dikkate alınmaz.
[Microsoft controller belgesi](https://learn.microsoft.com/en-us/windows/win32/input-precisiontouchpad/touchpadgesturescontroller)

Doğrudan HID metadata erişimi başarılıdır; 30 bayt input raporu tanımlanmıştır.
Gerçek okuma handle'ı hata 32 (paylaşım ihlali) ile reddedilmiştir. Driver/OEM
alternatifleri ve imzalı filtre fizibilitesi ayrı
[donanım raporunda](PHASE3_HARDWARE.md) açıklanmıştır; sürücü kurulmamıştır.

## Otomatik doğrulama ve dağıtım

`Windows/build.ps1 -Test -Publish`: **105 çekirdek + 10 Windows = 115 test** başarılı;
0 başarısız/atlanan, derleme 0 uyarı/hata. Tüm eski testler korunmuştur. Yeni
testler sekiz bölge/yön, sürekli/tek eylem, eşikler, iptal, parmak kimliği/sıralı
kaldırma, esnek controller parmak grubu, yeniden oluşturulmuş başlangıç, eski ayar
yükleme, kalıcılık/yedek, kanıt kaynağı ayrımı ve iptal edilen donanım eylemlerini
kapsar. Windows pencere işlemleri yalnızca atılabilir test penceresinde denenir.

Çalışan Phase 2 sürümü yayınlamadan önce tam klasör olarak yedeklendi; EXE
SHA-256 eşleşmesi doğrulandı:
`artifacts/backups/TrackpadProMax-phase2-before-phase3-20261101-231854/`.
Yayın betiği ayrıca başarılı staging smoke sonrasında önceki klasörü korur.
Son win-x64 yayın başarıyla tamamlandı; 11 ekran, çark/alt menü, dairesel pencere
bölgesi, kısayol kayıtları ve iki yerel API kaydı smoke testinden geçti.
`artifacts/TrackpadProMax/smoke/ui-smoke.json` içindeki
`PhysicalDeliveryVerified=false`, otomatik smoke'un fiziksel test yapmadığını
belirtir; yukarıdaki kullanıcı eşliğindeki ayrı yerel kanıtları geçersiz kılmaz.

## Fiziksel kabul adımları

1. `artifacts/TrackpadProMax/TrackpadProMax.exe` açın. Klasörü bütün tutun.
2. Yüzey Denetimi'nde bölgeleri/eşikleri düzenleyip kaydedin; yeniden açarak
   atamaların kaldığını doğrulayın. Sol/sağ dikey, üst/alt yataydır; köşelerin
   varsayılan ekseni yataydır. Kenarlar varsayılan olarak yüzeyin %18'idir.
3. Fiziksel bölge testleri ekranında 2 parmak seçin; gerekiyorsa doğrulanan
   controller yolu için 3 seçin. Bölgeyi seçip “Önizlemeyi başlat”a basın, imleci pencere üzerinde
   bırakın. Bir parmak hedef şeritte/köşede, diğerleri merkezde başlayabilir.
4. Eşik kadar kaydırın, bütün parmakları kaldırın. Sekiz testin her birini ayrı
   başlatın. Sol/sağda yukarı/aşağı; üst/altta ve köşelerde sağ/sol deneyin.
   Laboratuvar önizlemesi dış eylem çalıştırmaz.
5. Gerçek etkiyi sınamak için “Gerçek eylem testini başlat”a basın veya Yüzey
   Denetimi'nde “Yüzey kontrolünü başlat”ı seçin. Ses düzeyi,
   parlaklık ve oynatıcı zamanını ayrıca gözleyin. API kabulü tek başına duyulan
   ses veya ekran/oynatıcı etkisi değildir. Sonuçları JSON olarak dışa aktarın.
6. Bölgeden çıkma, ek parmak, iptal, merkezde normal hareket, Esc ve odak kaybını
   sınayın. Sohbete geçmek oturumu kapatır; tekrar test düğmesine basın. Test
   sonunda yerel girişi kapatıp normal Windows davranışını kontrol edin.
