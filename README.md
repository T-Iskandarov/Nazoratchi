<div align="center">
  <img src="installer/assets/app.ico" alt="Nazoratchi Logo" width="100"/>
  <h1>🛡️ Nazoratchi - Mukammal Tarmoq va Dastur Nazorati</h1>
  <p>Maktablar, ta'lim markazlari va ota-onalar uchun kompyuterni himoyalash va nazorat qilish tizimi.</p>
</div>

---

## 📌 Loyiha Haqida (About)
**Nazoratchi** — bu Windows operatsion tizimi uchun mo'ljallangan yopiq va qat'iy nazorat tizimi bo'lib, o'quvchilar yoki foydalanuvchilarning chalg'ishini oldini olish, xavfsiz internet muhitini yaratish va ruxsatsiz dasturlarni ishga tushishini taqiqlash uchun ishlab chiqilgan.

Dastur fonda ko'rinmas (`NT AUTHORITY\SYSTEM` ruxsati bilan) ishlaydi va o'zining himoya mexanizmlari orqali istalgan urunishlarni, shu jumladan VPN, DNS-Bypass (QUIC, DoH, IPv6) va xavfsizlik sozlamalarini chetlab o'tishni to'liq bloklaydi.

---

## 🚀 Asosiy Imkoniyatlar (Features)

🌐 **Keng qamrovli DNS Filter**  
Taqiqlangan saytlarga kirishni OS darajasida bloklaydi. Dastur to'g'ridan-to'g'ri `hosts` fayli bilan ishlab, zararli yoki chalg'ituvchi saytlarni (masalan, YouTube, Telegram Web, Instagram) `0.0.0.0` ga yo'naltiradi. 

🛑 **Bypass (Aylanib o'tish) ga qarshi himoya**  
Chrome, Edge va boshqa brauzerlardagi **QUIC** protokoli o'chirilgan, shuningdek, **IPv6** tarmog'i adapter darajasida faol bloklanadi. Bu foydalanuvchilar filtrni aylanib o'tishining oldini oladi.

🚫 **Dasturlarni cheklash (AppGuard)**  
Faqatgina ruxsat etilgan dasturlargagina ishlash imkonini beradi. Kompyuterga ruxsatsiz `.exe`, `.msi` o'rnatish, shuningdek Command Prompt (`cmd`), `PowerShell` kabi tizim dasturlari orqali buzishga urinishlar bloklanadi.

⚙️ **Uchib ketmaslik kafolati (Auto-Recovery)**  
Xizmat `Task Manager` orqali o'chirib qo'yilgan taqdirda ham, Windows tomonidan 5 soniya ichida qayta tiklanadi va ishlashda davom etadi. Xizmatni to'xtatish uchun administrator paroli kiritilishi shart.

---

## 📥 Yuklab Olish (Download & Install)

Tayyor o'rnatuvchi (Setup) faylini yuklab olib, darhol kompyuteringizga o'rnatishingiz mumkin. Boshqa hech qanday qo'shimcha .NET yoki kutubxona o'rnatish talab qilinmaydi (Self-Contained).

🔗 **[Nazoratchi_Setup.exe ni Yuklab Olish](https://github.com/T-Iskandarov/Nazoratchi/raw/main/Installer/Nazoratchi_Setup.exe)**

**O'rnatish jarayoni:**
1. Yuqoridagi linkdan `.exe` faylni yuklab oling.
2. Ishga tushiring (U avtomatik ravishda Windows Defender antivirusiga istisnolar qo'shadi va xavfsiz o'rnatiladi).
3. O'rnatish tugagach, fonda `Nazoratchi.Service` ishga tushadi.
4. Boshqaruv paneli (Panel) orqali parolni o'rnating va oq/qora ro'yxatlarni sozlang.

---

## 🛠️ Dasturchilar uchun (For Developers)

Loyihani o'zingiz o'zgartirib, yig'ish (build) uchun sizga .NET 8.0 SDK va Inno Setup 6 kerak bo'ladi.

### Arxitektura
* **`Nazoratchi.Service`** - Orqa fonda ishlovchi Windows Service (C# Worker Service). Tizim qamrovidagi filtrlash (Hosts, DNS) va AppGuard (WMI) vazifalarini bajaradi.
* **`Nazoratchi.Panel`** - Foydalanuvchi interfeysi (WPF). Parol orqali himoyalangan sozlamalar paneli.
* **`Nazoratchi.Core`** - Xizmat va Panel o'rtasidagi umumiy modellar, sozlamalar (JSON) va xavfsizlik (AES-256) mantiqlari.

### Kompilyatsiya qilish (Build)
Loyihani avtomatik yig'ish va Setup yaratish uchun quyidagi scriptni ishga tushiring:
```bat
Build_Installer.bat
```
*(Eslatma: Skript ishlashi uchun kompyuterda `ISCC` (Inno Setup) o'rnatilgan va Environment Variables (PATH) ga qo'shilgan bo'lishi kerak).*

---

## 👨‍💻 Muallif
**Tursunpo'lat Iskandarov** (CUBO LLC)  
Tizim o'zbekistonlik ta'lim markazlari ehtiyojlari va xavfsizlik talablariga moslashtirib noldan yozildi.
