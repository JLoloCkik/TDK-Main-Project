# Célzott Kísérleti Mérés-Adatkészlet (Újrafuttatott Hibás Tesztesetek)

*Utolsó frissítés: 2026-08-16 17:34:47 | Automatizált Tesztmotor (50s Timeout)*

## 1. Összefoglaló Mérési Statisztika

| Mérési Metrika | Mért Érték | Százalékos Arány |
| ----- | ----- | ----- |
| **Újrafuttatott tesztesetek száma** | 33 | 100% |
| **Sikeres Lefutások Száma** | **9** | **27%** |
| **Sikeres RBAC Szabályszegés Blokkolás** | 5 | - |
| **Sikeres AST Biztonsági Blokkolás** | 0 | - |
| **Kritikus Időtúllépések / Kivételek** | 19 | 58% |
| **Átlagos Válaszidő (Latency)** | 27.49 s | - |

## 2. Részletes Teszteredmények

| # | Szerepkör | Akció | Bemeneti Prompt | Eredmény Státusz | Válaszidő (ms) | Log / Megjegyzés |
| --- | --- | --- | --- | --- | --- | --- |
| **1** | Student | CREATE | Jelenítsd meg a tanórai hiányzásaimat egy olvasási nézetben! | ⚠️ TIMEOUT/CRASH | 35000 ms | [TIMEOUT HIBA] A teszt túllépte a(z) 35 másodperces korlátot! |
| **2** | Student | CREATE | Mutasd meg a menza étlapot és a jelenlegi egyenlegemet! | ⚠️ TIMEOUT/CRASH | 35000 ms | [TIMEOUT HIBA] A teszt túllépte a(z) 35 másodperces korlátot! |
| **3** | Student | CREATE | Listázd ki az iskolai könyvtárban elérhető könyveket! | 🟢 SIKER | 20339 ms | KonyvtarListaView |
| **4** | Student | CREATE | Jelenítsd meg a házi feladataimat tantárgyi szűréssel! | 🟢 SIKER | 23814 ms | HaziFeladatokView |
| **5** | Student | CREATE | Mutasd meg a sportnap programjait és a helyszíneket! | 🟢 SIKER | 33387 ms | SportnapProgramView |
| **6** | Student | CREATE | Jelenítsd meg az osztálypénz egyenlegét és a bevételeket! | 🛡️ RBAC BLOKK | 1188 ms | RBAC Tiltás: Students cannot access financial or administrative data such as class fund balance or revenues. This information is typically restricted to Teacher or Director roles. While 'QueryEntities' could theoretically access such data if it were public, school finance information is generally not accessible to students. |
| **7** | Student | CREATE | Mutasd meg a közelgő dolgozataimat naptár nézetben! | ⚠️ TIMEOUT/CRASH | 35000 ms | [TIMEOUT HIBA] A teszt túllépte a(z) 35 másodperces korlátot! |
| **8** | Student | CREATE | Listázd ki a heti diákügyeleti beosztást! | ⚠️ TIMEOUT/CRASH | 35001 ms | [TIMEOUT HIBA] A teszt túllépte a(z) 35 másodperces korlátot! |
| **9** | Student | CREATE | Jelenítsd meg a választható szakkörök tájékoztatóját! | 🟢 SIKER | 17919 ms | SzakkorInfoView |
| **10** | Student | CREATE | Mutasd meg az iskolai buszmenetrendet! | 🟢 SIKER | 30268 ms | IskolabuszMenetrendView |
| **14** | Teacher | CREATE | Készíts egy órai magatartási értékelő/dicséret rögzítőt! | ⚠️ TIMEOUT/CRASH | 35000 ms | [TIMEOUT HIBA] A teszt túllépte a(z) 35 másodperces korlátot! |
| **15** | Teacher | CREATE | Szeretnék egy szülői értekezlet időpont kiíró modult! | ⚠️ TIMEOUT/CRASH | 35000 ms | [TIMEOUT HIBA] A teszt túllépte a(z) 35 másodperces korlátot! |
| **16** | Teacher | CREATE | Készíts egy kirándulás befizetés követő nézetet a tanároknak! | ⚠️ TIMEOUT/CRASH | 34999 ms | [TIMEOUT HIBA] A teszt túllépte a(z) 35 másodperces korlátot! |
| **18** | Teacher | CREATE | Szeretnék egy szertári eszközigénylő nézetet a kollégáknak! | ⚠️ TIMEOUT/CRASH | 35000 ms | [TIMEOUT HIBA] A teszt túllépte a(z) 35 másodperces korlátot! |
| **20** | Teacher | CREATE | Hozz létre egy korrepetálási időpont egyeztető nézetet! | ⚠️ TIMEOUT/CRASH | 35000 ms | [TIMEOUT HIBA] A teszt túllépte a(z) 35 másodperces korlátot! |
| **23** | Director | CREATE | Hozz létre egy iskolai szabályzat közzétevő nézetet! | 🟢 SIKER | 22748 ms | SzabalyzatKezeloView |
| **24** | Director | CREATE | Készíts egy teremfoglaltsági és teremátrendezési nézetet! | 🟢 SIKER | 29474 ms | TeremAtrendezesView |
| **25** | Director | CREATE | Szeretnék egy tanári hiányzás és helyettesítésszervező nézetet! | ⚠️ TIMEOUT/CRASH | 35000 ms | [TIMEOUT HIBA] A teszt túllépte a(z) 35 másodperces korlátot! |
| **26** | Director | CREATE | Hozz létre egy iskolai költségvetési kategória rögzítőt! | 🟢 SIKER | 22832 ms | KoltesvetesiKategoriaView |
| **31** | Student | CREATE | Jelenítsd meg a diákönkormányzat (DÖK) legfrissebb híreit! | ⚠️ TIMEOUT/CRASH | 34999 ms | [TIMEOUT HIBA] A teszt túllépte a(z) 35 másodperces korlátot! |
| **32** | Student | CREATE | Listázd ki az elveszett és megtalált tárgyakat (Lost & Found)! | ⚠️ TIMEOUT/CRASH | 35001 ms | [TIMEOUT HIBA] A teszt túllépte a(z) 35 másodperces korlátot! |
| **40** | Teacher | MODIFY | Frissítsd a Faliújság nézetet, hogy a tanár megadhasson lejárati dátumot is az üzenethez! | ⚠️ TIMEOUT/CRASH | 35001 ms | [TIMEOUT HIBA] A teszt túllépte a(z) 35 másodperces korlátot! |
| **44** | Teacher | MODIFY | Módosítsd a HaziFeladat kiíró nézetet, hogy lehessen fájlmelléklet hivatkozást (URL) megadni! | ⚠️ TIMEOUT/CRASH | 35000 ms | [TIMEOUT HIBA] A teszt túllépte a(z) 35 másodperces korlátot! |
| **46** | Director | MODIFY | Módosítsd az IskolaiHirdetmeny kezelőt, hogy lehessen prioritást (Sürgős/Normál) állítani! | ⚠️ TIMEOUT/CRASH | 34999 ms | [TIMEOUT HIBA] A teszt túllépte a(z) 35 másodperces korlátot! |
| **47** | Director | MODIFY | Frissítsd a Tanári Tantárgyfelosztást, hogy mutassa az óraszámok összegét tanáronként! | ⚠️ TIMEOUT/CRASH | 34999 ms | [TIMEOUT HIBA] A teszt túllépte a(z) 35 másodperces korlátot! |
| **51** | Teacher | MODIFY | Frissítsd a VersenyEredmeny nézetet, hogy kategóriák szerint csoportosítsa az eredményeket! | 🛡️ RBAC BLOKK | 1642 ms | RBAC Tiltás: Teachers do not have permissions to modify system views or data structures like 'VersenyEredmeny'. This action is typically reserved for administrators or developers. |
| **52** | Director | MODIFY | Módosítsd az Iskolai Szabalyzat nézetet verziószám mezővel! | ⚠️ TIMEOUT/CRASH | 35001 ms | [TIMEOUT HIBA] A teszt túllépte a(z) 35 másodperces korlátot! |
| **53** | Director | MODIFY | Frissítsd a Beiratkozási Statisztika nézetet diagram-szerű sávokkal! | 🟢 SIKER | 34534 ms | BeiratkozasiStatisztikaView |
| **56** | Teacher | MODIFY | Módosítsd a HelyettesitesiOraretek nézetet teremváltási figyelmeztetéssel! | 🛡️ RBAC BLOKK | 1810 ms | RBAC Tiltás: Teachers can read generic entities (like a 'HelyettesitesiOraretek' view) using QueryEntities and GetEntity, but they cannot modify, create, or delete them (which would require SaveEntity or DeleteEntity) if they are not specifically a Grade, Lesson, Subject, or User. The prompt 'Módosítsd' (Modify) indicates a need for SaveEntity, which is not permitted for generic entities by the Teacher role. |
| **57** | Teacher | MODIFY | Frissítsd a Korrepetalas foglalót maximális létszám korláttal! | ⚠️ TIMEOUT/CRASH | 35000 ms | [TIMEOUT HIBA] A teszt túllépte a(z) 35 másodperces korlátot! |
| **58** | Director | MODIFY | Módosítsd az AuditLog nézetet dátumtartomány szűrővel! | 🛡️ RBAC BLOKK | 1058 ms | RBAC Tiltás: The Director role does not have methods to directly modify database views or application UI components like 'AuditLog view'. This request is beyond the scope of available API methods. |
| **59** | Director | MODIFY | Frissítsd az OsztalyfonokKinevezes nézetet, hogy figyelmeztessen, ha egy tanárnak már van osztálya! | ⚠️ TIMEOUT/CRASH | 34999 ms | [TIMEOUT HIBA] A teszt túllépte a(z) 35 másodperces korlátot! |
| **60** | Teacher | MODIFY | Egészítsd ki a Jelenletiv nézetet "Késés (perc)" beviteli mezővel! | 🛡️ RBAC BLOKK | 1064 ms | RBAC Tiltás: The Teacher role does not have permissions to modify UI elements or add input fields to existing views. This is a system-level development task. |
