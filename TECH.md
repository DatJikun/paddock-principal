# Paddock Principal — TECH

**Status:** DRAFT (2026-09-25)
**Rola:** architektura, niezmienniki techniczne, determinizm, dane, zapis, diagnostyka i testy. Systemy gry opisuje [DESIGN.md](DESIGN.md).

---

## 1. Stack

| Warstwa | Technologia | Dlaczego |
|---|---|---|
| Rdzeń symulacji | **C# / .NET 10 (LTS)**, bez zależności od UI | szybki, typowany, deterministyczny; infrastrukturę przenosimy z Pelotona |
| Zapis | **SQLite** (`Microsoft.Data.Sqlite`), jeden plik `.paddock` na karierę | transakcje, zapytania po historii bez ładowania wszystkiego do RAM |
| UI | **HTML/CSS + TypeScript + Svelte** w oknie desktopowym **Photino.NET (WebView2)** | patrz §1.1 |
| Narzędzia | `SimRunner` (CLI do przebiegów wsadowych), `DataPipeline` (import historii) | balans, regresje, dane |

### 1.1. Dlaczego nie Godot
Godot świetnie nadaje się do gier z grafiką, ale ten projekt to w 90% gęste tabele, formularze i wykresy. Kontrolki UI w Godocie są do tego toporne. Co ważniejsze, **model AI piszący sceny Godota pracuje na ślepo**: nie widzi efektu, więc UI wychodzi słabo. Z HTML/CSS jest inaczej:
- mogę otworzyć UI w przeglądarce, zrobić zrzut ekranu i poprawiać, aż wygląda dobrze;
- Twoje makiety z Pelotona i prototypy z Ping-Ponga już są w HTML;
- CSS daje ładne i gęste tabele najmniejszym kosztem.

Photino.NET to lekkie okno z systemowym WebView2 (jest w Windows 10/11) i rdzeniem C# w tym samym procesie. Gra nadaje się na Steama jak każda aplikacja desktopowa.
Awaryjnie, jeśli Photino sprawi problem: WinForms + WebView2, ten sam frontend.

### 1.2. Most UI ↔ rdzeń
- UI wysyła JSON-y: `{ kind: "query" | "command", name, args, id }`. Rdzeń odpowiada DTO w JSON.
- **UI nie ma logiki gry** i nie mutuje stanu. Wszystko idzie przez komendy (INV-001).
- W trybie deweloperskim ten sam most działa przez lokalny HTTP/WebSocket, więc UI można otworzyć w zwykłej przeglądarce (szybka iteracja, zrzuty ekranu).
- Typy TS generowane z DTO w C#, żeby most się nie rozjeżdżał.

---

## 2. Struktura solucji

```text
src/
  Paddock.Domain        encje i reguły, czysty C#
  Paddock.Simulation    silnik wyścigu, AI, dzienny tick świata, generator ludzi
  Paddock.Application   komendy, zapytania, pętla kariery, skrzynka
  Paddock.Persistence   SQLite, migracje, kompaktowanie
  Paddock.Data          ładowanie bazy świata + walidacja
  Paddock.Desktop       host Photino + most JSON
tools/
  Paddock.SimRunner     przebiegi wsadowe, testy wierności, zrzuty Spy
  Paddock.DataPipeline  import z Jolpica-F1, wyliczanie ocen, budowa bazy świata
tests/
  Paddock.Tests
ui/                     Svelte + TS (Vite)
data/
  world/                zbudowana baza świata (w repo)
  authored/             ręczne pliki: personel, technologie, epoki, propozycje, nadpisania ocen
  cache/                surowy cache API (poza repo)
```

Sześć projektów w `src/`, bez mnożenia warstw na zapas. Nowy projekt powstaje tylko wtedy, gdy obecny realnie przeszkadza.

---

## 3. Niezmienniki

- **INV-001:** UI i narzędzia nie mutują stanu. Robią to tylko komendy.
- **INV-002:** determinizm. Ten sam build + ta sama baza świata + ten sam stan początkowy + ta sama sekwencja komend = ten sam wynik.
- **INV-003:** prawda symulacji jest oddzielona od wiedzy aktorów. AI i UI gracza widzą tylko to, na co pozwala `AccessContext`.
- **INV-004:** strumienie RNG są izolowane (§4).
- **INV-005:** zapytania i prognozy nie zmieniają stanu i nie zużywają RNG.
- **INV-006:** Spy jest pasywny. Włączenie go nie zmienia wyniku.
- **INV-007:** zapis tylko w stabilnych punktach (koniec dnia, przed wyścigiem, po wyścigu). Wyścig jest niepodzielną jednostką.
- **INV-008:** kompaktowanie historii nie zmienia przyszłości.
- **INV-009:** stabilne ID nigdy nie są używane ponownie. Prawdziwe osoby mają ID z bazy świata, generowane dostają nowe.

---

## 4. Determinizm i RNG

- **Master seed na karierę.** Każdy strumień ma ziarno wyprowadzone przez `hash(master, nazwaStrumienia, sezon, [runda])`. Dzięki temu dodatkowy rzut w jednym systemie nie przesuwa innych, a wynik sezonu X nie zależy od tego, ile losowań zużył sezon X−1.
- **Strumienie:** `Weather`, `LapNoise`, `Incidents`, `Failures`, `PitStops`, `Market`, `AiDecisions`, `People` (generator i rozwój), `History` (ocena propozycji historycznych), `LifeEvents`, `Regulations` (propozycje i głosowania zmian regulaminu w trybie `VotedEachSeason`).
- **Generator:** Xoshiro256** z jawnym stanem zapisywanym w save.
- **Liczby:** `double`. Gwarancja determinizmu obejmuje ten sam build na x64. Arytmetykę stałoprzecinkową rozważymy tylko wtedy, gdy testy regresji to wymuszą.
- **Test regresji:** SimRunner przelicza N sezonów i porównuje hash stanu z zapisanym wzorcem.

---

## 5. Czas i komendy

- **Tick dnia:** `AdvanceDay` przetwarza kolejkę zaplanowanych zdarzeń danego dnia: kontrakty, rozwój, finanse, harmonogram ludzi, propozycje historyczne, zdarzenia życiowe. Dni bez zdarzeń są prawie darmowe, co pozwala przewijać tygodnie.
- **Pipeline komendy:** walidacja → (dla AI) `DecisionTrace` → wykonanie → zdarzenia domenowe. Odrzucona komenda nie zmienia stanu i zwraca powód, który UI pokazuje graczowi.

### 5.1. Wielu graczy (PP-045)

**Zasada:** jeden świat liczony w jednym miejscu (u hosta). Gra jednoosobowa to po prostu host bez gości.

- **Menedżerowie od pierwszego dnia:** każda komenda niesie `managerId`, a każde zapytanie zwraca widok jednego menedżera (jego skrzynka, jego wiedza o świecie). Rdzeń nigdy nie zakłada, że człowiek jest jeden.
- **Bramka gotowości:** `AdvanceDay` rusza dopiero, gdy wszyscy ludzie zgłoszą gotowość. Sprawa blokująca czas (negocjacje, decyzja w skrzynce) u jednego gracza zatrzymuje czas dla wszystkich i jest widoczna jako „czekamy na …”.
- **Kolejność komend:** komendy graczy trafiają do jednej kolejki u hosta w ustalonym porządku (dzień, numer zgłoszenia), więc przebieg jest deterministyczny i da się go odtworzyć z zapisu.
- **Sieć:** most JSON z §1.2 działa przez WebSocket. Goście łączą się z hostem, wysyłają komendy i dostają DTO swojego widoku. Gość nie ma własnej symulacji, więc nie może się „rozjechać” ze światem.
- **Wyścig na żywo:** silnik wyścigu produkuje strumień zdarzeń (okrążenia, pit-stopy, incydenty). Host odtwarza go w tempie oglądania i rozsyła wszystkim, a polecenia z boksu wracają jako komendy z numerem okrążenia.
- **Łączenie przez internet:** na start bezpośrednie połączenie z hostem (przekierowanie portu albo sieć typu Tailscale/ZeroTier). Gdyby to było za trudne dla znajomych, można dodać serwer pośredniczący. Decyzja w fazie 6.
- **Zapis:** zapis gry istnieje tylko u hosta. Goście zapisują jedynie swoje ustawienia.
- **Nie robimy:** gry korespondencyjnej, wspólnego zespołu z podziałem ról, kont i matchmakingu.

---

## 6. Dane świata i zapis

### 6.1. Baza świata (`data/world`)
- **Budowana przez `DataPipeline`** z Jolpica-F1 (osoby, zespoły, tory, wyniki od 1950) oraz plików `data/authored/` (personel, technologie, oś czasu epok, propozycje historyczne, nadpisania ocen).
- **Oceny kierowców:** model porównań z partnerem z zespołu z efektem konstruktor × sezon. Wynik to tempo na sezon, krzywa kariery i sufit talentu. Każde ręczne nadpisanie ma komentarz z uzasadnieniem.
- **Walidacja referencji i hash bazy.** Hash trafia do save'a, więc wczytanie zapisu wymaga tej samej wersji bazy (albo jawnej migracji).
- **Paczka prawdziwych danych jest wymienna** na fikcyjną (PP-014).
- **Licencja danych (PP-041):** dane Jolpica-F1 są na **CC BY-NC-SA 4.0**: użytek niekomercyjny, z podaniem źródła, na tej samej licencji. Do komercji potrzebna jest zgoda (admin@jolpi.ca). Ergast miał własne warunki, wykluczające płatne aplikacje.
  - Konsekwencja: **pobranych danych nie commitujemy** (cache jest w `data/cache/`, poza repo), a każdy buduje bazę lokalnie przez `DataPipeline`.
  - Do wydania na Steam potrzebna jest własna, niezależnie zebrana baza faktów albo zgoda właściciela danych (otwarte pytanie w ROADMAP).
- **Dane autorskie w repo** (tworzone przez nas, można commitować):
  - `data/authored/regulations/`: `catalog.json` z 41 wymiarami, `f1_timeline.json` (1950–2026), `other_series_ideas.json`;
  - `data/authored/tracks/`: `circuits.json` z torami i wersjami układów oraz profilami, `race_layout_map.json` z przypisaniem każdego wyścigu do układu.
  - `data/authored/tech/technologies.json`: katalog technologii;
  - `data/authored/teams/engines.json`: dostawcy silników konstruktorów, 1950–2026;
  - `data/authored/teams/lineage.json`: ciągłość zespołów po zmianie nazwy lub sprzedaży;
  - `data/authored/teams/founders.json`: założyciele, rok założenia i baza organizacji;
  - `data/authored/people/staff.json`: personel techniczny i jego funkcje w sezonach.
- **Importer:** `tools/Paddock.DataPipeline` z komendami `fetch --from 1950 --to 2025`, `normalize`, `summary`, `stats`. `stats` zapisuje raport epok do `data/cache/reports/` (poza repo). Pobiera nie więcej niż ~450 zapytań na godzinę (limit Jolpica: 500/h), wznawia pracę z cache i ponawia zapytania przy błędach.

### 6.1a. Inicjalizator świata (T20)
- `Paddock.Data.World.WorldInitializer.Create(CareerConfig, AuthoredData, IPeopleProvider, masterSeed)` buduje `WorldState` na `StartYear`. Czysta funkcja: bez I/O i zegara, losuje tylko ze strumienia `People` wyprowadzonego z master seeda (INV-002, INV-004). Ten sam seed, konfig i dane dają ten sam `StateHash()`.
- **Organizacje:** jedna na konstruktora występującego w `engines.json` w danym sezonie (id = `constructorId`), plus zespoły-poprzednicy z `lineage.json` jako rozwiązane organizacje, żeby łańcuch ciągłości był kompletny. Łańcuch urywa się przy powtórzonym `constructorId` (np. Renault 2002–2011 i 2016–2020), bo `WorldState` trzyma pojedynczy łańcuch. Dostawcy silników to organizacje `supplier:{slug}`, a przypisanie silników zwraca wynik inicjalizacji (świat nie ma jeszcze relacji dostawy).
- **Personel:** tylko wpisy z `staff.json` pokrywające sezon, w organizacjach istniejących w tym sezonie. Role `owner` i `designer` nie mają odpowiednika w `StaffRole`, więc trafiają do raportu luk. Ocen personelu nie ma, więc wszystkie atrybuty to wartość zastępcza (ESTIMATE).
- **Kierowcy wg `PeopleSource`:** `RealTrajectory` i `RealPotential` (na starcie ten sam świat) biorą ocenę z `IPeopleProvider`, a bez niej płaski zastępnik z `RandomizeKnownPerson` i wpis w raporcie. `RealNamesRandomSkills` zachowuje tożsamość i losuje umiejętności. `FullyGenerated` generuje stawkę i pulę. Pula to kierowcy bez kontraktu, którzy jeszcze nie zadebiutowali, a ich wejście do puli już nastąpiło (`PoolEntryYear` z harmonogramu T12); lista jest w wyniku, bo `WorldState` nie ma pojęcia puli.
- Liczby zgadywane (długość pierwszego kontraktu, jakość pokolenia, liczba miejsc, wielkość puli) są w `WorldInitEstimates` i są ESTIMATE. SimRunner: `init-world --preset <nazwa> --year <rok> --seed <n>` drukuje raport (liczby wg rodzaju, luki, hash).

### 6.2. Save (`.paddock` = SQLite, tryb WAL)
- **Główne tabele:** `meta` (wersja schematu, hash bazy, master seed, stan RNG, data gry), `people`, `person_attributes`, `organizations`, `org_lineage`, `contracts`, `cars`, `seasons`, `race_results`, `standings`, `chronicle` (rozbieżności), `hall_of_fame`, `inbox`, `decision_traces`.
- **Migracje:** wersjonowane skrypty w kodzie, stosowane w jednej transakcji przy wczytaniu.
- **Kompaktowanie (koniec sezonu):**
  - usuwamy dane okrążeń i szczegółowe ślady Spy;
  - zostają wyniki, klasyfikacje, rekordy, kronika i Hall of Fame;
  - generowane osoby bez znaczącej kariery są usuwane;
  - prawdziwe osoby zostają zawsze, bo baza świata i tak je zna.
- **Cel:** save po 76 sezonach poniżej ~50 MB. Weryfikujemy to w SimRunnerze, a nie deklarujemy.

---

### 6.3. Języki (PP-021)
- Każdy tekst dla gracza to klucz w `strings/pl.json` i `strings/en.json`, także w CLI. Rdzeń zwraca klucz i parametry, a nie gotowe zdania.
- Test pilnuje, żeby oba pliki miały te same klucze.

### 6.4. Prototyp UI
- `ui/prototype/` to statyczny, klikalny prototyp wyglądu, bez prawdziwego rdzenia.
  - `css/app.css`: tokeny i komponenty;
  - `css/screens.css`: ekrany;
  - `js/data.js`: atrapa danych;
  - `js/ui.js`: klocki;
  - `js/screens.js`: ekrany;
  - `js/app.js`: router, ustawienia, tło.
- **Podgląd:** `.claude/launch.json`, konfiguracja `ui-prototype` (`python -m http.server 5178 --directory ui/prototype`). Plik `index.html` otwiera się też dwuklikiem.
- Sprawdzamy na 1440×900 i 1620×860.
- Z prototypu przeniesiemy tokeny i komponenty do właściwego UI (Svelte) w fazie 6.

## 7. Paddock Spy (diagnostyka decyzji)

- **Każda decyzja AI** (rynek, R&D, strategia wyścigu, finanse) zapisuje `DecisionTrace`: kto decydował, jaki był jego poziom, co wywołało decyzję, jakie opcje rozważył (wraz z użytecznością i rozbiciem na czynniki), co wybrał i dlaczego. Opcjonalnie trace zawiera kontekst prawdy symulacji, ale tylko dla dewelopera.
- **Dwa poziomy wglądu:**
  - Spy deweloperski (SimRunner, flaga `--spy`) widzi wszystko;
  - „Dlaczego” dla gracza jest filtrowane przez `AccessContext`: raporty inżynierów, słowa agentów, komunikaty radiowe.
- **Warstwy:** typy trace'a (`DecisionTrace`, `TraceOption`, `TraceFactor`, `WeekendKey`) i `ITraceSink` z `NullSink`/`MemorySink` leżą w `Paddock.Domain.Spy`, żeby AI w `Paddock.Simulation` mogło zapisywać trace bez referencji do Application. W Application zostają filtrowanie przez `AccessContext` (`WhyView`) oraz `FileSink`/JSON. `DecisionTrace.Who` to stabilne ID aktora jako `string`.
- **Retencja:** bufor w RAM na bieżący weekend. W save zostają tylko kluczowe decyzje (transfery, tytuły, duże awarie), a resztę można zrzucić do pliku na żądanie.

---

## 8. Testy

- **Jednostkowe:** reguły domeny (punktacja epok, kontrakty, ekonomia).
- **Regresja determinizmu:** hash stanu po N sezonach.
- **Test wierności historii (PP-012):** przebieg okresu bez gracza, potem porównanie z rzeczywistością (rozkład mistrzów, udział ukończonych wyścigów, dominacja). Raport z SimRunnera zamiast asercji 1:1.
- **Stres:** kariera 1950→2100, czas przeliczenia sezonu i rozmiar save'a.
- **UI:** zrzuty ekranu kluczowych widoków w trybie deweloperskim (przeglądarka).
- **Błędy:** najpierw test, który je odtwarza, potem poprawka (za Pelotonem, D-030).
