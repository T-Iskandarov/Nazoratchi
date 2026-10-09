; =========================================================================
; Nazoratchi - Mukammal Nazorat va Filtrlash Tizimi
; Inno Setup 6 Installer Skripti
; Ishlab chiquvchi: CUBO IT Academy (https://cubo.uz)
; Muallif: Tursunpo'lat Iskandarov
; =========================================================================

#define MyAppName "Nazoratchi"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "CUBO LLC"
#define MyAppURL "https://cubo.uz"
#define MyAppExeName "Nazoratchi.Panel.exe"
#define MyServiceExeName "Nazoratchi.Service.exe"

[Setup]
; Dastur haqida asosiy ma'lumotlar
AppId={{9F8E24C1-4D90-482B-8E49-7C6B2F8D5A10}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; O'rnatish yo'li va guruh
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputBaseFilename=Nazoratchi_Setup_v1.0
OutputDir=..\dist
Compression=lzma2/ultra64
SolidCompression=yes

; Administrator huquqi talab qilinadi
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible

; Dizayn, brending va rasmlar
WizardStyle=modern
SetupIconFile=assets\app.ico
WizardImageFile=assets\wizard_large.bmp
WizardSmallImageFile=assets\wizard_small.bmp
InfoBeforeFile=assets\info_before.txt
UninstallDisplayIcon={app}\{#MyAppExeName}

; Jarayonlarni boshqarish
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
WelcomeLabel1=Nazoratchi tizimiga xush kelibsiz!
WelcomeLabel2=Ushbu usta kompyuteringizga Nazoratchi tizimini o'rnatadi.%n%nIshlab chiquvchi: CUBO LLC%nMuallif: Tursunpo'lat Iskandarov%nVeb-sayt: https://cubo.uz%n%nDavom etishdan oldin boshqa barcha dasturlarni yopish tavsiya etiladi.
FinishedHeadingLabel=Nazoratchi muvaffaqiyatli o'rnatildi!
FinishedLabel=Nazoratchi xavfsizlik va filtrlash tizimi kompyuteringizga o'rnatildi va xizmat avtomatik ishga tushirildi.
ClickFinish=O'rnatishni yakunlash uchun "Finish" tugmasini bosing.

[Tasks]
Name: "desktopicon"; Description: "Ish stolida yorliq (shortcut) yaratish"; GroupDescription: "Qo'shimcha sozlamalar:"; Flags: checkedonce

[Files]
; Boshqaruv paneli va uning barcha kerakli kutubxonalari
Source: "..\publish\panel\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Fon tizim xizmati (Windows Service)
Source: "..\publish\service\Nazoratchi.Service.exe"; DestDir: "{app}"; Flags: ignoreversion
; Ikonka fayli
Source: "assets\app.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; Ish stoli yorlig'i
Name: "{autodesktop}\Nazoratchi Boshqaruv Paneli"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"; Tasks: desktopicon
; Bosh menyu yorliqlari
Name: "{group}\Nazoratchi Boshqaruv Paneli"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"
Name: "{group}\Nazoratchi Xizmatini Ishga Tushirish"; Filename: "{sys}\sc.exe"; Parameters: "start NazoratchiService"; Flags: runminimized
Name: "{group}\Nazoratchi Xizmatini To'xtatish"; Filename: "{sys}\sc.exe"; Parameters: "stop NazoratchiService"; Flags: runminimized
Name: "{group}\Dasturni O'chirish (Uninstall)"; Filename: "{uninstallexe}"

[Run]
; 1. Avval eski xizmat bo'lsa uni xavfsiz to'xtatish va o'chirish
Filename: "{sys}\sc.exe"; Parameters: "stop NazoratchiService"; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "delete NazoratchiService"; Flags: runhidden waituntilterminated

; 2. Yangi servisni avtomatik ro'yxatdan o'tkazish
Filename: "{sys}\sc.exe"; Parameters: "create NazoratchiService binPath= ""{app}\{#MyServiceExeName}"" start= auto DisplayName= ""Nazoratchi Xavfsizlik Xizmati"""; Flags: runhidden waituntilterminated; StatusMsg: "Nazoratchi tizim xizmati ro'yxatdan o'tkazilmoqda..."

; 3. Servisni darhol ishga tushirish
Filename: "{sys}\sc.exe"; Parameters: "start NazoratchiService"; Flags: runhidden waituntilterminated; StatusMsg: "Nazoratchi tizim xizmati ishga tushirilmoqda..."

; 4. Boshqaruv panelini ochish taklifi
Filename: "{app}\{#MyAppExeName}"; Description: "Nazoratchi Boshqaruv Panelini ishga tushirish"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Tizimdan o'chirish paytida servisni to'xtatish va o'chirish
Filename: "{sys}\sc.exe"; Parameters: "stop NazoratchiService"; Flags: runhidden waituntilterminated; RunOnceId: "StopServiceUninstall"
Filename: "{sys}\sc.exe"; Parameters: "delete NazoratchiService"; Flags: runhidden waituntilterminated; RunOnceId: "DeleteServiceUninstall"

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
