# Üçüncü Taraf Bileşenler

Relay kaynak kodu MIT lisanslıdır. Aşağıdaki bileşenler kendi lisanslarıyla, ayrı dosyalar halinde dağıtılır; Relay'in MIT lisansı bu bileşenlerin lisansını değiştirmez.

| Bileşen | Paketteki sürüm | Kaynak | Lisans |
| --- | --- | --- | --- |
| GoodbyeDPI | 0.2.3rc3, x64 | [ValdikSS/GoodbyeDPI](https://github.com/ValdikSS/GoodbyeDPI/tree/0.2.3rc3) | [Apache-2.0](licenses/GoodbyeDPI-APACHE-2.0.txt) |
| zapret / winws | v72.9, x64 | [bol-van/zapret](https://github.com/bol-van/zapret/tree/v72.9) | [MIT](licenses/zapret-MIT.txt) |
| WinDivert | 2.2 serisi; GoodbyeDPI ve zapret'in kendi paketlerindeki DLL ve sürücüler | [basil00/WinDivert](https://github.com/basil00/WinDivert) | [LGPLv3 veya GPLv2](licenses/WinDivert-LICENSE.txt), Relay paketinde LGPLv3 seçeneği |
| Cygwin DLL | 3.4.10-1, x64 | [Cygwin](https://cygwin.com/) | LGPLv3 veya sonrası ve Cygwin bağlantı istisnası; tam metinler `licenses/Cygwin-*` dosyalarında |

GoodbyeDPI, zapret, WinDivert ve Cygwin dosyaları değiştirilmedi. EXE, DLL ve sürücüler ilgili resmî dağıtım arşivleriyle SHA-256 üzerinden karşılaştırıldı. Dosya bazında eşleşmeler, kaynak adresleri ve özetler [kilit dosyasında](third-party/dependencies.lock.json).

`bin/zapret/` altındaki BIN dosyaları motorun kullandığı örnek paket verileridir. Kilit dosyasında bunların da SHA-256 değerleri bulunur. `upstreamMatches` alanı boşsa ilk projeden devralınan dosya için bu sürüm arşivlerinde birebir eşleşme bulunamamıştır; resmî kaynakla eşleştiği iddia edilmez.

## Kaynak arşivleri

Paketlenen bileşenlerin kaynakları ayrı [kaynak dalında](https://github.com/AhmetTrk09/relay-discord/tree/third-party-sources) tutulur. [Kaynakları ZIP olarak indir](https://github.com/AhmetTrk09/relay-discord/archive/refs/heads/third-party-sources.zip). Uygulamayı kullanmak için bu arşivlerin indirilmesi gerekmez.

Arşivler GoodbyeDPI 0.2.3rc3, zapret v72.9, WinDivert v2.2.0/v2.2.2 ve Cygwin 3.4.10-1 kaynaklarını içerir. Cygwin arşivinde özgün kaynak ve cygport derleme tarifi birlikte bulunur.

Bileşen güncellendiğinde kaynak dalı, kilit dosyası ve lisanslar birlikte güncellenmeli; önceki sürümlerin kaynaklarına erişim korunmalı. `scripts/fetch-third-party-sources.ps1` arşivleri özgün adreslerinden indirip doğrular.

WinDivert ve Cygwin DLL dosyaları uygulamaya gömülmez; lisanslarının izin verdiği şekilde uyumlu sürümlerle değiştirilebilir. Bileşenlerin kendi telif bildirimleri ve lisansları arşivlerde ve `licenses/` klasöründe korunmuştur.

## Setup içindeki Microsoft bileşeni

Setup EXE, Microsoft .NET Framework 4.8 web kurucusunu da içerir. Yalnızca Framework eksikse çalıştırılır. Bu dosya Microsoft lisansına tabidir ve Relay MIT lisansının kapsamına girmez. [Bildirim](licenses/Microsoft-NET-Framework-NOTICE.txt), kaynak ve SHA-256 bilgisi [kurulum kilit dosyasında](third-party/installer.lock.json). Inno Setup derleyicisi geliştirme aracıdır; derleyicinin kendisi Relay paketine eklenmez.

Oluşturulan Setup dosyası Inno Setup çalıştırıcısını da içerir. [Inno Setup lisansı](licenses/Inno-Setup-LICENSE.txt) pakette korunmuştur.
