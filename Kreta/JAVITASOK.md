# EvolKréta — Javítási jegyzőkönyv

Ez a dokumentum összefoglalja, mi volt hibás az eredeti kódban, és pontosan mit javítottam.

## 1. `.env` fájl — miért "nem működött"

**A hiba:** `DotNetEnv.Env.Load()` (paraméter nélkül) az **aktuális munkakönyvtárból**
(CWD) próbálja beolvasni a `.env`-et. Ez a CWD viszont attól függ, *honnan*
indítod a programot:
- `dotnet run`-nal a projekt gyökeréből → véletlenül működött
- IDE-ből (Rider/VS) más induló könyvtárral → már nem
- a lefordított `.exe` dupla kattintásával a `bin/Debug/net10.0` mappából → biztosan nem,
  mert oda a `.env` sosem másolódott át

**A javítás:**
- Új `Services/PathHelper.cs`: megkeresi a projekt gyökerét (ahol a `.csproj` van),
  a `BaseDirectory`-ból felfelé lépkedve — ez **független** attól, honnan indult a program.
- `MainWindow.LoadEnvironment()` innen tölti be a `.env`-et, és ha nem találja,
  pontosan megmondja, **hol kereste**.
- A `Kreta.csproj`-ba került egy `<None Include=".env" CopyToOutputDirectory="PreserveNewest" />`
  is, biztonsági hálóként.
- **Ugyanez a hiba okozta azt is, hogy két különböző `kreta.db` fájl jött létre**
  (egy a projekt gyökerében, egy a `bin/Debug/net10.0`-ban) — a `KretaDbContext`
  is relatív útvonalat használt. Ezt is a `PathHelper`-rel javítottam, most már
  mindig ugyanaz az egy adatbázisfájl töltődik be.

➡️ **Neked ennyi a teendő:** nyisd meg a `.env` fájlt a projekt gyökerében, és írd
be a saját Gemini API-kulcsodat a `GEMINI_API_KEY=` után.

## 2. "A modell szétesik" — az AI-hívás törékenysége

Több valódi hiba is volt itt egyszerre:

- **API-kulcs kezelés:** a `new Client()` egy dokumentálatlan környezeti változó
  keresésre hagyatkozott. Most explicit módon olvassuk be a `GEMINI_API_KEY`-t,
  és ha hiányzik, egyértelmű magyar hibaüzenetet kapsz — nem egy rejtélyes
  API-kivételt.
- **Nincs kényszerített JSON-válasz:** a Gemini időnként extra szöveget,
  magyarázatot tett a JSON köré, amitől a `JsonSerializer.Deserialize` elszállt.
  Most `ResponseMimeType = "application/json"` van beállítva a kérésben (ez a
  hivatalos Gemini "JSON mód"), plusz egy tartalék JSON-kivonatoló, ami a
  válaszból kimetszi a `{...}` blokkot, ha mégis maradna körülötte szöveg.
- **Nincs önjavítás:** ha a fordítás vagy a biztonsági szűrő elutasította a
  generált kódot, a program csak hibát dobott és leállt — pedig a te
  eredeti terved (PDF, "Self-Healing Loop") pont erre épült. Most a
  `OnAiClick` **3 próbálkozásig** automatikusan visszaküldi a hibát az
  AI-nak, hogy javítsa ki magát.
- **Modellnév konfigurálhatóvá tétele:** ha Google később elavulttá tesz egy
  modellnevet, a `.env`-ben `GEMINI_MODEL=...` sorral bármikor átállíthatod
  újrafordítás nélkül.

## 3. RBAC — a jogosultsági szabály eddig csak "kérés" volt, nem tényleges ellenőrzés

Az eredeti `AstAnalyzer` csak azt nézte, hogy a kód `System.IO`-t vagy
`File`/`Directory`/`Process`-t használ-e. **Semmi nem ellenőrizte ténylegesen**,
hogy pl. egy Diák által kért kód hivatkozik-e `ITeacherContext`-re vagy
`IDirectorContext`-re — ez csak a rendszerpromptban volt "megkérve" az AI-tól,
ami elméletileg megkerülhető.

Most az `AstAnalyzer.IsSafe(code, role, out reason)` ténylegesen megvizsgálja a
generált kód szintaxisfáját, és **elutasítja**, ha a szerepkörhöz nem
engedélyezett Context-re hivatkozik — pontosan úgy, ahogy a PDF-ben leírt
tervben szerepelt.

## 4. Memóriaszivárgás / "második generálás után elszáll"

Az eredeti kód **egyetlen `DynamicLoader` (= egyetlen ALC) példányt** hozott
létre a `MainWindow` konstruktorában, és ezt használta újra minden egyes AI-generáláshoz.
Csakhogy egy `AssemblyLoadContext.Unload()` **egyszer használatos** — utána az
adott példány véglegesen használhatatlan. Ez azt jelentette, hogy ha egyszer
elvetettél egy javaslatot (Discard → Unload), a **következő generálás már
elromlott volna**.

Javítás: minden új generáláskor **friss `DynamicLoader` példány** (= friss ALC)
jön létre, az előzőt előtte leállítjuk. Ez felel meg ténylegesen a PDF-ben
leírt "Collectible ALC" architektúrának.

## 5. Git-verziókezelés

- Ha még nem volt `.git` mappa a projektben, a `Commit()` hívás azonnal hibával
  elszállt volna. Most a `GitService` automatikusan `git init`-el, ha kell.
- Automatikusan létrehoz egy `.gitignore`-t (`bin/`, `obj/`, `.env`, `*.db`
  kizárva) — így a titkos API-kulcsod sosem kerül commitba.
- Az üres commit (ha nincs tényleges változás) most nem dob hibát.

## 6. Hiányzó Context implementációk

A PDF RBAC-modellje Tanár és Igazgató kontextust is előír, de csak a Diák
(`SqliteStudentContext`) volt megvalósítva. Hozzáadtam a
`SqliteTeacherContext`-et és `SqliteDirectorContext`-et is, hogy az
architektúra ténylegesen teljes legyen.

## 7. GUI

- Modernebb, letisztultabb sidebar és kártya-alapú tartalom-megjelenítés
  (lekerekített sarkok, finom árnyékok).
- **"Az AI dolgozik..." overlay** jelenik meg generálás közben (korábban
  semmilyen visszajelzés nem volt, csak a lila szöveg alul — most egy
  animált progress bar is jelzi, hogy történik valami, és a gombok/mező
  ilyenkor le vannak tiltva, hogy ne lehessen véletlenül kétszer elindítani).
- A profil-kártyák (Diák/Tanár/Igazgató nézet) most szép, árnyékolt "kártya"
  dizájnt kapnak sima szöveg helyett.

---

## Hogyan indítsd el

1. Nyisd meg a `.env` fájlt, írd be a saját `GEMINI_API_KEY`-edet.
2. `dotnet restore`
3. `dotnet run`

Ha a `dotnet restore` hálózati/csomag hibát dobna valamelyik verziószámra
(pl. mert időközben újabb NuGet-verzió jött ki), írd meg, és azt is javítom.
Én magam nem tudtam ezen a gépen lefordítani a projektet (nincs `dotnet` SDK
és hálózat a sandboxban), úgyhogy érdemes az első futtatás után visszajelezni,
ha bármi fordítási hibát dob — pontosan meg tudom mondani, mit kell módosítani.
