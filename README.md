# 🛸 The Colony Protocol

**The Colony Protocol**, Mars benzeri sert bir gezegende hayatta kalmaya ve gelişmeye odaklanan, derinlemesine simülasyon mekaniklerine sahip bir koloni yönetimi oyunudur. Oyuncular, kaynakları yönetmeli, karmaşık yapılar inşa etmeli ve astronotlarının hayatta kalmasını sağlamak için hayati sistemleri optimize etmelidir.

---

## 🌟 Öne Çıkan Özellikler

### 🏗️ Gelişmiş Bina ve Şebeke Simülasyonu
- **Ağ Tabanlı Sistem (BFS):** Binalar sadece yan yana durmaz; enerji, su ve oksijen paylaşımı için dinamik ağlar oluşturur. Sistem, ızgara üzerindeki bağlantıları **Breadth-First Search (BFS)** algoritmasıyla tarayarak koloniyi bağımsız "şebeke adalarına" ayırır.
- **Oksijen Desteği:** Yaşam destek üniteleri, ağa bağlı toplam astronot kapasitesini belirler. Oksijen yetersizliği durumunda astronotların hayatta kalma şansı kritik seviyelere düşer.
- **Bina Aşınması:** Zorlu dış koşullar binaların sağlığını (HP) zamanla azaltır. Koloninin sürekliliği için düzenli bakım ve onarım şarttır.

### 🧑‍🚀 Dinamik NPC Hayatı ve Uzmanlıklar
- **Karakter İhtiyaçları:** Astronotların; Sağlık, Oksijen, Açlık, Susuzluk ve Mutluluk gibi dinamik ihtiyaçları vardır.
- **Uzmanlık Rolleri:** 
  - **Engineer (Mühendis):** Yapı onarımı ve teknik bakım.
  - **Biologist (Biyolog):** Gıda üretimi ve yaşam destek optimizasyonu.
  - **Medical (Doktor):** Sağlık müdahaleleri.
  - **Worker (İşçi):** Genel taşıma ve inşaat görevleri.
- **Akıllı Navigasyon:** A* algoritması ile güçlendirilmiş 2D yol bulma sistemi.
- **İş Yönetimi:** Görevler, `ConstructionManager` tarafından boşta olan en uygun NPC'ye dinamik olarak atanır.

### ⚡ Enerji ve Çevre Ekosistemi
- **Çok Kanallı Üretim:** 
  - **Güneş Panelleri:** Günün saatine göre değişen verimlilikle enerji üretir.
  - **Rüzgar Türbinleri:** Gezegen sınıfına (D, F, M, S) ve anlık rüzgar hızına göre enerji sağlar.
- **Depolama:** "Power Collector" üniteleri, gündüz üretilen fazla enerjiyi gece kullanımı için depolar.
- **Gece/Gündüz Döngüsü:** Görsel atmosferi, ışıklandırmayı (URP 2D Light) ve üretim parametrelerini etkileyen gerçek zamanlı döngü.

### 📦 Kaynak Ekonomisi
- **Stratejik Kaynaklar:** Metal, Biyoplastik, Yedek Parçalar, Yemek, Tıbbi Malzemeler ve Enerji.
- **Geri Dönüşüm (Salvage):** Çevredeki enkazları toplayarak nadir materyaller elde edin.

---

## 📂 Proje Yapısı

```text
Assets/
├── Scrpits/          # Oyun mantığı ve sistemleri
│   ├── Core/         # Simülasyon, Enerji, Kaynak ve Kayıt sistemleri
│   ├── NPC/          # AI, Rol tanımları ve görev yönetimi
│   ├── Building/     # İnşaat mekanikleri ve ScriptableObject tanımları
│   └── World/        # Çevre etkileşimleri ve Salvage sistemi
├── Prefabs/          # Binalar, Karakterler ve UI şablonları
├── Sprites/          # Karakter animasyonları ve yapı grafikleri
├── Animations/       # Dinamik görsel efektler ve hareketler
├── Musics/           # Tematik atmosfer müzikleri
└── Tilemaps/         # Dünya katmanları ve ızgara verileri
```

---

## 🛠️ Teknik Detaylar

- **Motor:** Unity 2022.3 (LTS)
- **Render:** Universal Render Pipeline (URP) ile 2D Işıklandırma.
- **Veri:** JSON tabanlı merkezi `SaveManager` mimarisi.
- **UI:** Dinamik Info-Card ve kaynak takip sistemi.

---

## 🚀 Başlarken

1. **Repoyu Klonlayın:**
   ```bash
   git clone https://github.com/kullanici/the-colony-protocol.git
   ```
2. **Unity ile Açın:** Proje klasörünü Unity Hub üzerinden ekleyin.
3. **Başlatın:** `Assets/Scenes/StartScene` sahnesinden oyunu çalıştırın.

---

> *"İnsanlığın yeni evi sizin ellerinizde. Protokolü takip edin."*
