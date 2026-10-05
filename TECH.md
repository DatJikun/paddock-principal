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
- **Strumienie:** `Weather`, `LapNoise`, `Incidents`, `Failures`, `PitStops`, `Market`, `AiDecisions`, `People` (generator i rozwój), `History` (ocena propozycji historycznych), `LifeEvents`, `Regulations` (propozycje i głosowania zmian regulaminu w trybie `VotedEachSeason`), `Scouting` (szum obserwacji skautów, T40; osobno od `People`, żeby dodatkowy scouting nie przesuwał rozwoju ani fillerów), `Development` (sufit koncepcji auta, T41; osobno od `People` i `Scouting`, dziecko strumienia ze znacznikiem organizacji, więc zatwierdzenie jednego auta nie przesuwa innych).
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
- `Paddock.Data.World.WorldInitializer.Create(CareerConfig, AuthoredData, IPeopleProvider, masterSeed)` buduje `WorldState` na `StartYear`. Czysta funkcja: bez I/O i zegara. Ludzi losuje ze strumienia `People`, a sufity aut świata generowanego ze strumienia `Development` (`InitialCarFactory`), oba wyprowadzone z master seeda (INV-002, INV-004). Ten sam seed, konfig i dane dają ten sam `StateHash()`.
- **Organizacje:** jedna na konstruktora występującego w `engines.json` w danym sezonie (id = `constructorId`), poza konstruktorami, których wszystkie wpisy tego sezonu dotyczą wyłącznie Indianapolis 500 (1950–1960; luka `world.init.gap.indianapolis_only`). Do tego zespoły-poprzednicy z `lineage.json` jako rozwiązane organizacje, żeby łańcuch ciągłości był kompletny. Łańcuch urywa się przy powtórzonym `constructorId` (np. Renault 2002–2011 i 2016–2020), bo `WorldState` trzyma pojedynczy łańcuch. Dostawcy silników to organizacje `supplier:{slug}`, a przypisanie silników zwraca wynik inicjalizacji (świat nie ma jeszcze relacji dostawy).
- **Personel:** tylko wpisy z `staff.json` pokrywające sezon, w organizacjach istniejących w tym sezonie. Role `owner` i `designer` nie mają odpowiednika w `StaffRole`, więc trafiają do raportu luk. Ocen personelu nie ma, więc wszystkie atrybuty to wartość zastępcza (ESTIMATE).
- **Kierowcy wg `PeopleSource`:** `RealTrajectory` i `RealPotential` (na starcie ten sam świat) biorą ocenę z `IPeopleProvider`, a bez niej płaski zastępnik z `RandomizeKnownPerson` i wpis w raporcie. `RealNamesRandomSkills` zachowuje tożsamość i losuje umiejętności. `FullyGenerated` generuje stawkę i pulę. Pula to kierowcy bez kontraktu, którzy jeszcze nie zadebiutowali, a ich wejście do puli już nastąpiło (`PoolEntryYear` z harmonogramu T12); lista jest w wyniku, bo `WorldState` nie ma pojęcia puli.
- Liczby zgadywane (długość pierwszego kontraktu, jakość pokolenia, liczba miejsc, wielkość puli) są w `WorldInitEstimates` i są ESTIMATE. SimRunner: `init-world --preset <nazwa> --year <rok> --seed <n>` drukuje raport (liczby wg rodzaju, luki, hash).

### 6.2. Save (`.paddock` = SQLite, tryb WAL)
- **Główne tabele:** `meta` (wersja schematu, hash bazy, master seed, stan RNG, data gry), `people`, `person_attributes`, `organizations`, `org_lineage`, `contracts`, `cars`, `seasons`, `race_results`, `standings`, `chronicle` (rozbieżności), `hall_of_fame`, `inbox`, `decision_traces`.
- **Świat w zapisie (V003, T19):** `persons`, `person_roles`, `person_attributes` (prawda i sufit w jednym wierszu), `organizations`, `org_names`, `org_lineage` (jedna krawędź na wiersz), `contracts`, `knowledge` i `knowledge_bands` (przekonania organizacji, tylko przedziały), `id_counters` i `retired_ids` (ID nigdy nie wracają, INV-009), `scheduled_events`, `managers`, `command_log`. Warstwa zapisu nie interpretuje ładunku zdarzeń ani komend: trzyma typ i tekst, a właściciel (Simulation, Application) go koduje. `WorldRepository` zapisuje i wczytuje świat w jednej transakcji, a zapis jest dozwolony tylko na granicy dnia (INV-007): świat z inną datą niż podana granica jest odrzucany przed zapisem. Limity rozmiaru i czasu w teście `WorldSizeTests` to ESTYMATY, nie skalibrowane cele.
- **Sekcje świata (V004–V005, T36):** systemy fazy 4 trzymają swój stan jako `IWorldSection` w `WorldState` (nazwa, `SchemaVersion`, kanoniczny tekst; wartość niezmienna). Hash: świat bez sekcji ma format `paddock-world/1` (bez zmian względem T15), świat z sekcjami `paddock-world/2` (sekcje po zwykłej treści, w kolejności nazw, ciało sekcji z prefiksem długości). Zapis: tabela `world_sections` (rejestr) i `ISectionStore` na sekcję (`Replace`/`Load` w transakcji `WorldRepository`); nowa sekcja = własna migracja z tabelami, własny store, wpis w `SectionStores.Production`. Sekcja bez store'a jest odrzucana przy zapisie, a zapis z sekcją bez store'a przy wczytaniu, żeby dane nigdy nie ginęły po cichu. Pierwsza sekcja to `inbox` (V005); sekcja `objectives` dostała store razem z zarządem (T45, V010).
- **Inbox v0 (T36):** per menedżer, ID `inb:{n}` z licznika sekcji. Decyzja z otwartą opcją zajmuje jedyny slot `BlockingItem` menedżera (rodzaj `inbox.decision`) i zwalnia go po odpowiedzi; slot zajęty przez inny system zostaje nietknięty. Oferta z datą ważności musi mieć domyślną opcję i wygasa komendą `ExpireInboxItem` (wydawaną przez hosta, `InboxExpiry.EnqueueDue`), więc wygaśnięcie trafia do logu i powtarza się deterministycznie. Opcję wykonuje `IInboxResolver` właściciela rodzaju, wewnątrz komendy (INV-001).
- **Kontrakty i negocjacje (T39):** sekcja `contracts` (`ContractsSection`, własna migracja i store) trzyma `ContractTerms` (premie, kto ma opcję, klauzula wyjścia) per `ContractId` oraz `Negotiation` (`neg:{n}`, status, rundy, zainteresowanie, oferta, kontrpropozycja, historia, uzasadnienia jako klucze). Sam kontrakt zostaje w `WorldState` (T15). Osoba decyduje jako aktor: `U = w1·prestiż + w2·samochód + w3·pensja + w4·status − w5·ryzyko` z wagami z osobowości (`CounterpartyEvaluator`), a każda decyzja zostawia `DecisionTrace` (czynniki widoczne dla gracza to tylko te nazwane w uzasadnieniu). Pensja odniesienia to benchmark epoki (`driver_pay_*`) dla gwiazdek wynikających z pasm, które zna **oferujący** zespół, więc ukryty atrybut nie zmienia ani ofert, ani widocznych powodów (INV-003); to nie jest „wartość rynkowa” (PP-035). Osobowość nie jest jeszcze w świecie: port `IPersonalitySource` (domyślnie wyprowadzana z master seeda i ID osoby). Odpowiedź osoby jest zaplanowana w kolejce zegara (`negotiation.respond`, opóźnienie z losowaniem ze strumienia `Market`; każde losowanie to dziecko strumienia z kluczem ID negocjacji, więc cudza negocjacja go nie przesuwa). Człowiek dostaje decyzję w skrzynce (blokuje czas), AI czyta negocjację sam. Osoba z kilkoma akceptowalnymi ofertami czeka do najwcześniejszego terminu, potem: najlepsza użyteczność, zaufanie (`ITrustSource`, domyślnie neutralne), losowanie `Market`. Komendy (`OpenNegotiation`, `SubmitOffer`, `AcceptCounterOffer`, `WalkAway`, `RenewContract`, `TerminateContract`) działają tylko dla menedżera, który kieruje organizacją (`IOrganizationControl`). Finanse (T37) to port `IPayrollLedger` (domyślnie bez limitów). Uchwyt do świata dla komend i handlerów dnia to `ContractBook`; w karierze jest związany ze światem sesji (`ContractBook.Bind`), więc kontrakt podpisany komendą nie ginie (#160). Wszystkie liczby (`NegotiationEstimates`) to ESTYMATY.
- **Emerytura (V006, #118):** `Person.RetiredOn` to fakt świata, nie osobny rejestr. Emeryt zostaje w świecie z całym rekordem (prawdziwi zostają zawsze, usuwanie generowanych to kompaktowanie na koniec sezonu, INV-008), jego kontrakty się kończą, nie podpisze nowego i nie liczy się jako aktywny. Data wchodzi do hasha tylko przy emerycie (wiersz `retired`), więc hash świata bez emerytów się nie zmienia, a w zapisie jest kolumna `persons.retired_on`. Przebieg bez wyścigów: prawdziwa osoba ze znanym ostatnim sezonem (T12, `LastSeasons.From` z miejsc harmonogramu; kierowcy z ostatniego sezonu danych są pominięci, bo dane się tam po prostu kończą) odchodzi 31 grudnia tego sezonu, a pozostali (generowani i prawdziwi bez danych, np. personel) idą wg krzywej wieku `CareerDayEstimates` (ESTIMATE).
- **Wznowienie zapisu (V007, #123):** zapis na granicy dnia wystarcza do tej samej przyszłości (INV-002): bieg 1950→1970 w jednym przebiegu równa się biegowi 1950→1960, zapis, wczytanie, 1960→1970 (hash świata, pula, liczniki i podsumowania sezonów; test `CareerResumeTests`). Kodeki są wersjonowane i oznaczone typem (znacznik `nazwa/wersja`, treść to płaski JSON), z jawnym rejestrem zamiast szukania po nazwie typu CLR: ładunki zdarzeń (`EventPayloadCodec`, Simulation), komendy (`CommandCodec.Production`, Application) i menedżerowie (`ManagerCodec`). Nieznany znacznik albo wersja przerywa wczytanie (`UnknownTagException`), więc zdarzenie ani komenda nie są zgadywane. Nowa komenda wymaga wpisu w `CommandCodec.Production`, a nowy ładunek planowanego zdarzenia wpisu w `EventPayloadCodec`; testy pilnują kompletności. `EventQueue.Restore` odtwarza kolejkę z dokładnym `NextSequence`. Stan RNG to `meta.rng_states`: każdy strumień z `RngStreamName.All` dla sezonu daty zapisu (strumień jeszcze nietknięty w sezonie jest równy wyprowadzonemu z master seeda, więc przy wczytaniu jest pomijany, tak jak go pomija zegar). Poza hashem świata zapis trzyma stan biegu T18: `career_run` (rok otwarcia, od którego zależy generowany napływ, oraz liczniki wygaśnięć kontraktów i napływu), `career_years` (podsumowania sezonów). Pula zastępcza T18 (`talent_pool`) zniknęła w V008: pulę trzyma teraz sekcja świata `talent-pool` (T40), jedyne źródło prawdy, więc jest w hashu i w zapisie świata. `WorldRepository.SaveAll` zapisuje to w jednej transakcji ze światem i zastępuje poprzedni stan (brak stanu biegu czyści tabele); `SaveWorld` ich nie rusza. Zapis sprzed V007 otwiera się i wczytuje, ale nie da się go wznowić dokładnie, więc `run --resume` odmawia. SimRunner: `run --resume <zapis> --to <rok> [--save ścieżka]` bierze ziarno, preset i konfigurację z zapisu, wypisuje tylko sezony przeżyte teraz i odmawia, gdy zmieniła się baza (`meta.world_data_hash` obejmuje pliki autorskie oraz, jeśli użyto, harmonogram ludzi i kierowców z cache). Wiązanie kodeków z wierszami `Stored*` jest w SimRunnerze (korzeń kompozycji, `CareerSaveWriter` i `CareerSaveReader`), bo Persistence nie widzi Simulation ani Application.
- **Reputacja, zarząd i zwolnienia (T45, PP-050):** sekcja `board` (V013; store `objectives` jest z T38, V012) trzyma zarząd każdego zespołu (cierpliwość 0-100 z wieku organizacji, zaufanie, seria słabych ocen, oczekiwana pozycja, archetyp szefa AI, kto kieruje: człowiek albo osoba AI, ochrona do daty, założyciel), reputację każdego szefa z historią zmian oraz menedżerów bez zespołu. Reputacja i zaufanie to całkowite dziesiąte części punktu (0-1000), więc hash nie zależy od zmiennoprzecinkowych. Zarząd to byt, nie osoba, i nie ma atrybutów, tylko skalar cierpliwości. Oczekiwania są zwykłymi celami T36 (`board.objective.season`, `board.objective.multiYear`) z publicznych faktów (ostatnia pozycja, ranga budżetu); skutek celu to zmiana zaufania, którą host stosuje po dniu (`BoardOutcomes.Apply`). Po każdym wyścigu zaufanie zbliża się o ćwierć do wartości wynikającej z pozycji względem oczekiwań, a gotówka je obniża. Szef bez ochrony, którego zaufanie jest poniżej progu przez N ocen z rzędu, jest zwalniany: AI-szefa zastępuje kandydat z losowania `Market` (dziecko strumienia z kluczem celu, organizacji i daty, więc losowanie nie zależy od innych) z nowym archetypem, a gracz dostaje odprawę i stan `Unemployed`. Ochrona nowego szefa to pierwszy pełny sezon plus 3 dni za punkt reputacji. Zwolniony gracz jest neutralnym obserwatorem (zapytania nie zwracają widoku zespołu), jego otwarte decyzje i negocjacje są zamykane, a oferty pracy (decyzje w skrzynce, rodzaj `board.jobOffer`) przychodzą od zespołów, które chcą szefa i nie żądają więcej reputacji, niż gracz ma. Po oknie gwarancji (45 dni) przychodzi oferta minimalnej jakości niezależnie od reputacji, więc gracz nigdy nie utyka. Zakładający zespół nie jest zwalniany i nie może odejść. Komendy: `AcceptJobOffer`, `DeclineJobOffer`, `ResignFromTeam`. Hak T39: `IReputationSource` przesuwa prestiż zespołu w ocenie oferty o co najwyżej 0,15. Wszystkie liczby (`BoardEstimates`) to ESTYMATY; zbiór archetypów jest zastępczy do czasu T44.
- **Pula talentów (T40):** sekcja `talent-pool` (V008) trzyma członków puli z nieprzejrzystym uchwytem `talent-{n}` (licznik sekcji, kolejność z hasha id, więc uchwyt nie zdradza, kto jest prawdziwy), opłacone sezony juniorskie, fokus skautów każdej organizacji, punkty obserwacji i wygasłe kariery. Pasma wiedzy to zwykłe `PersonKnowledge` świata. `TalentPoolDayHandler` zastępuje zastępczy dopływ z T18: rozwój i fillery losuje z `People`, błąd odczytu skauta ze `Scouting`, zawsze jako dzieci strumienia ze znacznikiem sezonu i osoby, więc dodatkowy scouting nie przesuwa rozwoju. Komendy `AssignScoutFocus`, `FundJunior` i `SignPoolDriver` idą przez porty (`IJuniorFunding` do finansów T37, `IPoolNegotiations` do kontraktów T39). Zapytanie `PoolQuery` i widok AI `PoolKnowledgeView` nie mają pola prawdy ani `IsReal`. Liczby w `PoolEstimates` to SZACUNKI.
- **Finanse (T37, V010):** sekcja `finance` trzyma księgę każdej organizacji (wpisy tylko dopisywane, saldo to ich suma, kwoty w centach nominalnego USD), globalny indeks popularności w tysięcznych oraz zegarek niewypłacalności. Księgowanie idzie wyłącznie przez `FinanceSection.Post`, wołane z komend (`OpenBooks`, `ApplyRaceResults`, `ApplySeasonEnded`) i z `FinanceDayHandler` (pensje 1. dnia miesiąca, szkice `ILedgerSource` dla budowy auta, rozwoju i dostaw). Model `promoter_individual_deals` płaci startowe i nagrodę za pozycję; inne wartości `revenue_model` spadają na szacunek `pool_by_position` z ostrzeżeniem. Popularność (PP-025, PP-050: tylko globalna) rusza się na koniec sezonu od przewagi w tytule i liczby różnych zwycięzców. Saldo może być ujemne; `finance.organization_insolvent` pada w rocznicę zejścia pod próg, czyli po całym sezonie (PP-050), raz. Kredyty to pusty port `ILoanFacility` (PP-048). Widok własnej organizacji daje stan i prognozę do końca sezonu; cudza organizacja jest nieznana (INV-003). Zapytanie i prognoza nie zmieniają stanu i nie losują (INV-005). Finanse nie mają strumienia RNG. Liczby w `FinanceEstimates` to SZACUNKI. Porty `FinancePayroll`, `FinanceJuniorFunding` i `FinanceSeverance` podłączają księgę do kontraktów, puli i zarządu; w karierze moduł `finance` wymienia nimi zaślepki, o ile host ma dane epok (#160). Kapitał otwarcia to budżet epoki dla poziomu zespołu z `ITeamTierSource`; wyniki Jolpica są tylko lokalne (PP-041), więc bez nich źródłem jest autorski SZACUNEK poprzedniego sezonu (`data/authored/commercial/team_tiers_estimates.json`, dziś tylko 1954), a brak wpisu znaczy zespół typowy.
- **Auta (T41, PP-050):** sekcja `cars` (V011) trzyma dwa auta na zespół i stałego kierowcę każdego z nich (bez przesiadek i bez sprzedaży podwozi w MVP). Koncepcja to sześć osi; mapa na wektor osiągów jest PROPOZYCJĄ, a liczby w `CarEstimates` to ESTYMATY. Sufit koncepcji losuje strumień `Development` przy zatwierdzeniu. Siła startowa bierze `ICarStrengthSource` (efekt auta T22, gdy jest), inaczej próg; świat w pełni generowany losuje progi. Preferencje i liczniki doświadczenia kierowcy są w tej samej sekcji, losowane ze strumienia `People` przy starcie świata, bez zmiany typów generatora. Prawdę wektora czyta tylko symulacja (`CarPerformanceFor`, z limitem docisku epoki). Menedżer i AI dostają pasma, rywal nie dostaje sufitu ani wektora. Proporcja rozrzutu auta do kierowcy (`DriverCarBalance`) jest stałą per epoka ze źródłem w modelu ocen; dopóki nie ma przeliczenia na danych, to ESTYMATA do kalibracji (#122). Komenda `AcquireCustomerCar` zawsze odmawia (PP-050).
- **Sponsorzy (T38, V012):** sekcja `sponsors` trzyma rozmowy (`spt:n`), umowy (`spd:n`), oferty przedłużenia (`spo:n`), zaufanie per (sponsor, organizacja) i sponsorów zabranych przez rywala; jeden licznik, ID nie wracają. Sponsorzy są FIKCYJNI i leżą w `data/authored/commercial/sponsors_estimates.json` (loader i walidator w `Paddock.Data`, wszystkie liczby to ESTYMATY; walidator wymaga co najmniej 3 kandydatów na każde miejsce w każdym roku, PP-050). Trzy miejsca na zespół; rodzaje miejsc wynikają z epoki: do 1967 (`national_racing_colours`) trzy miejsca techniczne lub mecenasa, od 1968 główne, dodatkowe i techniczne; tytoń i alkohol dodatkowo wg `tobacco_advertising` i `alcohol_advertising`. Rozmowy to gra w czekanie: warunki rosną z dniem do limitu zależnego od negocjatora (dyrektor komercyjny, jeśli rola istnieje, inaczej szef zespołu), a ukryty rywal może podpisać sponsora. Rywala losuje `SponsorDayHandler` ze strumienia `Market` (dziecko po organizacji, sponsorze, miejscu i dacie otwarcia, nie po liczniku, więc cudza umowa nie przesuwa rzutów); komenda nie losuje, bo nie ma dostępu do strumieni. Raty i premie idą do księgi T37 (`LedgerCategories.Sponsor`). Warunek sponsora to cel T36; skutek (premia jednorazowa i zaufanie albo odejście sponsora) stosuje `SponsorOutcomes.Apply`, który host woła po dniu z tymi samymi zdarzeniami co `ObjectiveOutcomes.Apply`. Sekcja `objectives` (T36) dostała w V012 własny zapis. Komendy: `BeginSponsorTalks`, `SignAtCurrentTerms`, `WalkAwayFromTalks`, `RespondToSponsorOffer` (każda z `managerId`). Widok `SponsorQuery` zwraca tylko własne dane; rywala widzi tylko dobry negocjator (INV-003).
- **Rozwój auta (T42, V016, PP-043 ścieżka A):** sekcja `development` (plany, konta, projekty `dev:n`) i czysty serwis `DevelopmentEngine`, który nie zna gracza: wejściem jest plan zespołu (podział: bieżące auto / konto / przyszły rok, priorytety obszarów), a projekty wybierają inżynierowie (`EngineerChoice`, wagi to ESTYMATY), każdy wybór zostawia `DecisionTrace` (`Who` = inżynier). Szum wykonania i porażkę losuje strumień `Development`, dziecko ze znacznikiem zespołu, startu, rodzaju i obszaru projektu (nigdy numer globalny, więc cudzy projekt nie przesuwa losowań); remis propozycji rozstrzyga `AiDecisions` (dziecko: zespół, dzień, miejsce). Wydatki idą do księgi (`development`, co tydzień). Liczebność i jakość zespołu to skalar zastępczy za działy z DESIGN §6.2 (nie przyjęte). Podpięcie do pętli kariery (dzień: `DevelopmentDayHandler`, po wyścigu: `DevelopmentRaceHook`) robi host, rejestrację komend `DevelopmentRegistration`. **Produkcja koncepcji (T42c, V017):** komenda `CommitConcept` (albo opcja „zatwierdź” decyzji skrzynki `development.concept`, albo termin `WhenReady`/`AfterRaces`) przenosi gotową koncepcję do statusu `InProduction` z `ProductionEnds` i `ProductionCostCents`; koszt idzie do księgi od razu, a `TeamDay` wdraża koncepcję w pierwszym dniu po `ProductionEnds` (nigdy w dniu końca, więc nie w środku weekendu). Czas i koszt liczy czysta funkcja `ConceptProduction.Plan`, tę samą co widzi `DevelopmentQuery` (INV-005); produkcja nie losuje. Nowa pozycja kanonicznego tekstu projektu (` prod <koniec> <koszt>`) jest zapisywana tylko dla projektu zatwierdzonego, więc hash bez produkcji się nie zmienia. V017 przebudowuje `development_projects` (SQLite nie zmienia CHECK): dopuszcza status `InProduction` oraz termin `Hold` (V016 go nie znał, więc zapis trzymanego projektu by padł) i dodaje kolumny `production_ends`, `production_cost`. Kalendarz wyścigów dla widoku to opcjonalny port `INextRaceSource` w `DevelopmentEnvironment`.
- **Dostawy (T43, V015):** sekcja `supply` trzyma umowy (`supply:n`) i rozmowy (`sneg:n`) o silnik, opony i paliwo; rodzaje: fabryczna, partnerska, kliencka, silnik z zeszłego roku. Rodzaj ustala opóźnienie wersji (fabryczna 0, partnerska i kliencka 1, zeszłoroczna 2 sezony) i cenę; wszystkie liczby to ESTYMATY (brak danych R13/R14). Umowy startowe powstają z `engines.json` (`InitialSupplyFactory`; `works`→fabryczna, `partner`→partnerska, `customer`, `badged` i `unknown`→kliencka) i ustawiają `TeamCar.EngineKey` na id umowy (`SupplyCars.Sync`). Rozmowy używają słownika i stałych rdzenia T39 (`NegotiationStatus`, `NegotiationEstimates`), ale odpowiedź dostawcy to `SupplierResponder`: rdzeń T39 odpowiada za osobę (osobowość, użyteczność kontraktu). Opóźnienie odpowiedzi losuje `SupplyDayHandler` ze strumienia `Market` (dziecko po id rozmowy i rundzie); opłata roczna idzie do księgi T37 (`LedgerCategories.Supply`), a dostawca z księgą księguje sprzedaż. Wektor auta czyta `SupplyPerformance.Resolve` (silnik) i `TyresFor` (profil T30); bez umowy wektor jest taki jak w T41. Komendy: `ProposeSupplyDeal`, `RespondToSupplyOffer` (z `managerId`); widok `SupplyQuery` pokazuje tylko własne umowy i widełki własnego silnika (INV-003). Własny program silnikowy (PP-019) czeka na T42: punkt rozszerzenia to `IEngineProgrammes`.
- **Moduły kariery (#160):** pętla kariery nie zna żadnego systemu z nazwy. System dołącza do niej jako `ICareerModule` (`Paddock.Application.Career`) wpisany jedną linijką do `CareerModules.Default`; `CareerHost` i `CareerModuleHost` tylko przebiegają dni. Moduł deklaruje swoje sekcje świata, wpisy kodeków komend (`CommandCodec.Production` czyta je z listy), handlery komend, handlery dnia, hak poranny (komendy AI, które wrzuca do kolejki), hak po dniu (skutki zdarzeń dnia) i `Open` (uzupełnienie tego, czego brakuje w świecie, także po wznowieniu). Host wywołuje `Configure` wszystkich modułów (porty i fakty: finanse dają płatnika, zarząd daje reputację i „kto kieruje”), potem `Attach` wszystkich (moduł stoi na liście po modułach, których usługi czyta), potem `Open`. Kolejność w dniu wyznacza wyłącznie `IDayHandler.Order`, nie miejsce na liście. Dołączenie systemu wymaga jeszcze tego, co należy do warstwy zapisu: migracji w `SaveMigrations` i store'a w `SectionStores` (po jednym wpisie w linii; test sprawdza, że każda sekcja deklarowana przez moduł ma store). Świat jest jeden: sesja go trzyma, a książki (kontrakty, zarząd) czytają i piszą go przez `Bind`; książka z własną kopią sekcji (kontrakty, skrzynka) zrzuca ją do świata (`AddFlush`) po poranku i po każdym dniu, zanim cokolwiek zostanie zahaszowane lub zapisane.
  - **Kolejność w dniu (handlery):** pula talentów 10, starzenie 20, ostatni sezon 25, wygaśnięcie kontraktu 30, zamknięcie sezonu 40, negocjacje 700, cykl życia kontraktów 710, sponsorzy 750, finanse 800, cele (objectives) 900, zarząd 910. Zarezerwowane luki: rozwój auta (T42), dostawcy (T43) i AI zarządzające (T44) wybierają numer z tej skali i dopisują go tu; numer musi stać po tych, których wynik czytają.
  - **Po dniu (skutki zdarzeń dnia, z tymi samymi zdarzeniami):** sponsorzy 750 (premia i odejście sponsora), zarząd 910 (skutek celu dla zaufania, potem rozliczenie celów), samo rozliczenie celów 990 (to, czego nikt nie przejął). Zarząd musi widzieć cel jeszcze otwarty, więc rozliczenie jest ostatnie.
  - **Rano:** wygasłe wpisy skrzynki idą jako komendy (`InboxExpiry`), potem hak każdego modułu (placeholder AI umów do T44; zatwierdzenie koncepcji auta dla zespołu bez aut, losowanie ze strumienia `Development`). Komendy skrzynki (`Resolve`, `Dismiss`, `Expire`) rejestruje host.
  - **Dane wejściowe** (`CareerInputs`): epoki finansów, reguły slotów sponsorów, katalog sponsorów, benchmark płac i źródło poziomów zespołów. Ładuje je host (SimRunner: `CareerInputsLoader`); bez nich moduł, który ich potrzebuje (finanse, sponsorzy), nic nie robi, a zaślepki zostają. Nowa kariera dostaje księgi dla każdego aktywnego zespołu, dwa auta (inicjalizator świata) i zarząd z szefem na papierze od pierwszego dnia.
  - **Bez wyścigów w pętli:** bieg nie ma jeszcze kalendarza wyścigów (T47), więc nie napływają pieniądze z wyścigów ani od sponsorów AI (T44), a cele pozycji nie mają faktu o klasyfikacji i kończą jako niespełnione. Zarząd ocenia zespoły po dniu wyścigowym, więc dopóki wyścigów nie ma, nikt nie jest zwalniany. Bez dochodu zespoły AI tylko by wydawały i po kilku sezonach byłyby niewypłacalne i bez prawa do przedłużenia kontraktu (zmierzone: bieg Chaos od 1950 nie ma kontraktów w 1959), więc moduł `finance` daje zastępczo każdemu zespołowi z księgą 1 stycznia tę samą kwotę startową (SZACUNEK: `StartMoneyShare` typowego budżetu epoki; źródło `ILedgerSource`). Zaślepka znika razem z wejściem wyścigów do pętli (T47), które księgują prawdziwe wyniki.
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
- **Relacja z wyścigu** (`Paddock.Application.Racing.RaceReportBuilder`, T35+) to czysta funkcja `RaceTape` + `RaceWeekendResult` (INV-005, bez RNG). Zwraca sekcje klucz + argumenty (z liczbą dla form mnogich), a `RaceReportRenderer` składa tekst przez `ILocalizer`. Wiedza widza: warunki to tylko stan toru i temperatura na starcie oraz zmiany z taśmy, nigdy prawdziwa pogoda (INV-003). Koloryt epoki bierze się z danych (taśma, postoje, wycofania, zmiany kierowców), a nie z tekstu per rok. Nazwy typów zdarzeń nigdy nie trafiają do tekstu: każdy `RaceEventKind` ma klucz w `RaceReportKeys`. `SimRunner race` drukuje relację domyślnie, `--verbose` dodaje przebieg okrążenie po okrążeniu, a surowy log zostaje pod `--log`.

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
- **Testy UI:** `node --test "ui/prototype/tests/*.test.mjs"` (cudzysłów jest potrzebny, bo Node 22 nie przyjmuje katalogu; potrzebny Node 21+). Dwa kolejne pliki prototypu: `js/track-shape.js` (wybór kształtu toru, §6.5) i `js/track-geometry.generated.js` (generowany).

### 6.5. Geometria torów (PP-048, PP-049, #170)
- **Jeden plik na układ:** `data/authored/tracks/geometry/<layout_id>.json`. Ten sam plik czyta symulacja i UI; to ten format czyta i zapisuje edytor torów (`ui/track-editor`, PR #146).
  - `layout_id`: id z `circuits.json`, nazwa pliku musi być `<layout_id>.json`;
  - `control_points`: zamknięta pętla `[x, y]` w metrach (x na wschód, y na północ), punkt 0 leży na linii mety;
  - `source`: uczciwe pochodzenie (dziś „approximate, hand-authored from general layout knowledge, ESTIMATE”), `notes`: czym jest kształt;
  - `corners` (opcjonalne): `{ "point": indeks punktu kontrolnego, "name": "Parabolica" }`. Nazwa jest przypięta do punktu, więc wędruje razem z nim przy edycji. To nazwy własne (jak nazwy torów), nie przechodzą przez `strings/`.
- **Pokrycie:** pliki mają wszystkie układy ścigane w latach 1950–1960 (26, test `EveryLayoutRacedFrom1950To1960_HasAuthoredGeometry`). Kształty są ręcznie ułożone z ogólnej wiedzy i publicznych opisów tekstowych (kolejność i kierunek zakrętów, proste), bez map i obrysów (PP-041); stopień pewności każdego toru jest w `notes`. Najsłabsze (stylizowane): Pedralbes, Boavista, Ain-Diab, Monsanto, Sebring, Pescara (odcinek górski), Bremgarten, Rouen 1952.
- **Krzywa:** zamknięty centripetal Catmull-Rom (`TrackGeometry`), przeskalowany do `length_km` z `circuits.json`. Backend bierze ją przez `TrackGeometryCatalog.Resolve(layoutId)` (zwraca geometrię, źródło i zakręty z ułamkiem okrążenia). Geometria nie zmienia stanu świata, więc nie rusza hashy determinizmu.
- **Edycja** (to wszystko, co trzeba zrobić, żeby zmienić tor w symulacji i w UI):
  1. Zmień plik JSON ręcznie albo w edytorze torów (Wczytaj JSON, przesuń punkty, Zapisz JSON do tego samego pliku; pole `corners` edytor zachowuje).
  2. Walidacja: `dotnet run --project tools/Paddock.DataPipeline -- validate-authored` oraz `dotnet test`. Reguły: układ istnieje w `circuits.json`, co najmniej 8 punktów, sąsiednie punkty co najmniej 1 m od siebie, surowa długość w granicach ±15% od `length_km`, brak samoprzecięć, brak ostrych załamań (zwrot o ponad 25° na 2 m), poprawne `corners`, brak dwóch plików dla jednego układu. Testy dodatkowo wymagają ±2% długości i odstępu co najmniej 5 m dla każdego zatwierdzonego pliku.
  3. UI: `node ui/prototype/tools/build-track-geometry.mjs` przepisuje `ui/prototype/js/track-geometry.generated.js` (plik jest w repo, bo prototyp otwiera się dwuklikiem i nie może pobrać JSON-a w czasie działania). Test `track-shape.test.mjs` pada, jeśli plik jest nieaktualny; `--check` robi to samo z linii poleceń.
  4. Podgląd wszystkich torów: `ui/prototype/track-preview.html` (punkty kontrolne i nazwy zakrętów).
- **UI tylko rysuje krzywą** (TECH §3): `TrackShape.resolve` wybiera kształt, `TrackSpline` (`js/race-map.js`) to rendererowa kopia tej samej krzywej. Zgodność pilnuje fixture `ui/prototype/tests/fixtures/track-geometry-reference.json`, generowany testem .NET z `TrackGeometry` (`PADDOCK_UPDATE_FIXTURES=1 dotnet test --filter TrackSplineReference`); test JS porównuje próbki z tolerancją 2 cm (zmierzone poniżej 2 mm).
- **Brak pliku to zdefiniowany przypadek, nie błąd:** backend zwraca `TrackGeometry.Fallback` (neutralny stadion o długości `length_km`, `Source = Fallback`, bez nazw zakrętów), UI robi to samo (`TrackShape`, źródło `fallback`). Układ spoza `circuits.json` to błąd programisty (`ArgumentException`). Dla prototypu istnieje jeszcze trzecia ścieżka, `legacy`: ręczne punkty `map` w `data.js` dla układów 1976 bez pliku; usuwamy je wraz z pojawieniem się pliku. Że plik istnieje dla każdego układu sezonu startowego (1955), pilnuje test, a nie walidator, bo pozostałe sezony jeszcze nie mają geometrii.
- **To są szacunki.** Kształty są ręcznie rysowane z ogólnej wiedzy o układach (ESTIMATE), skalowane do znanej długości. Nie commitujemy obrazów ani śladów z zewnętrznych źródeł (PP-041); zastąpienie pliku dokładniejszym to zwykła edycja tego samego pliku.

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
- **Regresja determinizmu:** hash stanu po N sezonach. Przebieg sezonu 1955 w SimRunnerze (`run`) musi dać dwa razy ten sam hash, a zapis w dowolnym dniu sezonu wznowiony do tego samego dnia końcowego musi dać identyczny hash, dziennik komend i podsumowania sezonów (`CareerWiringTests`, `CareerResumeTests`).
  - **Złote hashe:** nie ma komendy, która je przepisuje, i nie ma być (test drukuje wartość rzeczywistą i nigdy jej nie zapisuje). Zmienia je człowiek, ręcznie, w stałej testu (`CareerRunTests.StoredWorldHash`), a PR mówi, dlaczego hash się zmienił (zwykle: nowa sekcja świata albo nowa komenda w dzienniku). Hash zmieniony bez takiego wyjaśnienia to błąd, nie aktualizacja.
- **Test wierności historii (PP-012):** przebieg okresu bez gracza, potem porównanie z rzeczywistością (rozkład mistrzów, udział ukończonych wyścigów, dominacja). Raport z SimRunnera zamiast asercji 1:1.
- **Kalibracja silnika wyścigu (#122):** `SimRunner calibrate-race --from Y --to Y [--stride N] [--seeds N] [--cache <katalog jolpica>]` symuluje wyścigi sezonów na syntetycznej stawce, zbiera je w pasma epok i porównuje z lokalnym cache Jolpica (odsetek ukończeń, podział mechanika/wypadek, mediana przewagi zwycięzcy, zdublowani, pole-to-win) oraz z pogodą R9 (deszcz w wyścigu, temperatura powietrza). Postoje i zmiany prowadzenia nie mają danych historycznych w cache, więc raport sprawdza je tylko względem zakresów orientacyjnych (ESTIMATE). Raport trafia do `data/cache/reports/` (poza repo, PP-041). Stałe skalibrowane tym raportem pozostają ESTYMATAMI: opis przy każdej stałej mówi, względem jakich sezonów były dostrajane; zakresy tolerancji są w `CalibrationTargets`.
- **Stres:** kariera 1950→2100, czas przeliczenia sezonu i rozmiar save'a.
- **UI:** zrzuty ekranu kluczowych widoków w trybie deweloperskim (przeglądarka).
- **Błędy:** najpierw test, który je odtwarza, potem poprawka (za Pelotonem, D-030).
