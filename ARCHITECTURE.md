# Paddock Principal — Technical Architecture & Modularity Design

**Wersja:** 1.0  
**Status:** DRAFT / APPROVED FOR PRE-PRODUCTION  
**Cel:** Kompletna specyfikacja architektury technicznej symulatora motorsportowego w modelu *data-driven headless modular monolith*. Zero zależności od silników graficznych w warstwie symulacji, konsolowe MVP, determinizm i pełna obsługa długich karier.

---

## 1. Fundament Architektoniczny: Headless Modular Monolith

Paddock Principal jest projektowany jako **data-driven headless modular monolith**.

Odrzucamy architekturę opartą o przeglądarkę, obiekty DOM czy skrypty powiązane z UI. Silnik gry jest czystą biblioteką w C# (.NET 9 / LTS), która nie ma pojęcia o istnieniu ekranów, przycisków czy klatek renderowania.

Główne zasady:
- **Jeden model świata**: Całość stanu symulacji żyje w deterministycznym stanie `WorldState`.
- **Brak UI w silniku**: Interfejs to tylko konsument danych (read-only queries) i nadawca intencji gracza (`ICommand`).
- **Konsolowe MVP (`Paddock.Cli`)**: Pierwsza grywalna wersja działa w 100% w terminalu, pozwalając na interaktywne zarządzanie zespołem oraz błyskawiczne symulowanie weekendów wyścigowych.
- **Wydajny SimRunner (`Paddock.SimRunner`)**: Narzędzie deweloperskie do symulacji 10, 50 lub 100 sezonów w parę sekund w celu weryfikacji balansu, ekonomii i regresji determinizmu.
- **Potencjalny klient graficzny w przyszłości**: Dowolny frontend (np. Godot 4.x C#) będzie tylko cienką nakładką prezentacyjną. Zmiana frontendu nie dotknie ani jednej linijki logiki symulacyjnej.

---

## 2. Docelowy Stack Technologiczny

1. **Język i platforma**: **C# / .NET 9 LTS** (strict nullable reference types, records dla niemutowalnych DTO, unikanie niepotrzebnych alokacji pamięci na stercie w pętli wyścigu).
2. **Persistence (Save System)**: **SQLite** (`Microsoft.Data.Sqlite`). Transakcyjna baza danych per zapis kariery. Pozwala na relacyjne trzymanie historii dziesiątek sezonów, tabel wyników, statystyk kierowców i telemetrii bez ładowania gigabajtowego JSON-a do RAM-u.
3. **Paczki zawartości (Content Packs)**: **JSON** z walidacją `JsonSchema`. Definicje serii (F1 1994, 2024, WEC), zespołów, torów, kierowców, personelu, reguł sportowo-technicznych oraz skryptowanych wydarzeń historycznych.
4. **CLI Framework**: `Spectre.Console` dla czytelnego, szybkiego interfejsu terminalowego z tabelami, kolorami zespołów i wykresami telemetrycznymi w wersji MVP.

---

## 3. Kluczowe Inwarianty Systemu (System Invariants)

Zasady, których nie wolno złamać w żadnym module:

- **INV-001 (Brak logiki w UI)**: Żaden widok terminala ani przyszły ekran Godota nie ma prawa mutować stanu świata bezpośrednio. Mutacja zachodzi wyłącznie przez zatwierdzenie `ICommand` w silniku.
- **INV-002 (Pełny determinizm)**: `Same Build + Same Content Pack + Same Initial Seed + Same Command Sequence = Same Result`. Dwa uruchomienia z tym samym seedem dają identyczne czasy okrążeń, zużycie opon, awarie i wyniki wyścigu z dokładnością do 1 milisekundy.
- **INV-003 (Separacja Prawdy i Wiedzy)**: `SimulationTruth` (rzeczywisty potencjał kierowcy, ukryte zużycie silnika rywali, dokładne plany taktyczne) jest odseparowane od `ActorKnowledge`. AI i gracz operują wyłącznie w granicach legalnej wiedzy (`AccessContext`).
- **INV-004 (RNG Isolation)**: Losowość jest podzielona na niezależne strumienie (`RngStream`). Sprawdzenie prawdopodobieństwa deszczu nie może przesuwać wskaźnika generatora dla szansy awarii zawieszenia czy negocjacji kontraktowych.
- **INV-005 (Automatyczna strategia wyścigowa)**: W wyścigu gracz ani AI nie sterują bolidem ręcznie z okrążenia na okrążenie. Za strategię pit-stopów, dobór opon, tempo i reakcję na deszcz/Safety Car odpowiada zatrudniony sztab (`Race Engineer`, `Head of Strategy`). Jakość decyzji zależy wprost od ich atrybutów.
- **INV-006 (Wyjaśnialność decyzji — Watcher First)**: Każda decyzja AI (transferowa, techniczna w fabryce, strategiczna na torze) generuje pasywny rekord `DecisionTrace` przed wykonaniem mutacji.
- **INV-007 (Bezpieczeństwo zapisu)**: Zapis gry do bazy SQLite następuje w stabilnych punktach kalendarza (np. po zakończeniu weekendu wyścigowego, przed rozpoczęciem nowego weekendu). Wyścig jest pojedynczą jednostką wykonawczą.

---

## 4. Architektura Warstwowa Projektu

Projekt podzielony jest na ścisłe biblioteki w ramach solucji (`PaddockPrincipal.sln`):

```text
┌─────────────────────────────────────────────────────────────┐
│                       Interfejsy                            │
│   Paddock.Cli (Terminal MVP)  │  Paddock.SimRunner (Dev CLI)│
└──────────────────────────────┬──────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────┐
│                    Paddock.Application                      │
│   Use Cases, Command Handlers, Query Handlers, Career Loop  │
└──────────────────────────────┬──────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────┐
│                      Paddock.Domain                         │
│   Model Świata, Encje (Driver, Car, Team, Track),           │
│   Race Engine (Symulacja Kwalifikacji i Wyścigu),           │
│   AI Principal Brain, R&D Engine, Transfer Market           │
└──────────────────────────────┬──────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────┐
│                   Paddock.Infrastructure                    │
│   SQLite Save Repository, JSON Content Loader,               │
│   Deterministic RNG (PCG/Xoshiro), File Logs                │
└─────────────────────────────────────────────────────────────┘
                               ▲
┌──────────────────────────────┴──────────────────────────────┐
│                     Paddock.Diagnostics                     │
│   Paddock Spy (Watcher), Decision Tracing, Telemetria       │
└─────────────────────────────────────────────────────────────┘
```

### 1. `Paddock.Domain`
- Czysty C#, zero zewnętrznych bibliotek frameworkowych.
- Encje domenowe: `Team`, `Driver`, `StaffMember`, `Car`, `Component`, `Track`, `SeriesRules`.
- `RaceSimulationEngine`: deterministyczny model czasów okrążeń, kwalifikacji, degradacji opon i incydentów.
- `AiManagerEngine`: mózg szefów zespołów AI decydujący o alokacji budżetów i transferach.
- `StaffStrategyEngine`: algorytm układający plan wyścigu na bazie kompetencji personelu inżynieryjnego.

### 2. `Paddock.Application`
- Orkiestracja pętli kariery: `AdvanceCalendarDay()`, `ExecuteRaceWeekend()`, `SubmitContractOffer()`, `StartComponentDevelopment()`.
- Obsługa zapytań (CQRS-lite): `GetDriverStandingsQuery`, `GetTeamFinancesQuery`, `GetTelemetryTraceQuery`.
- Kontrakty `ICommand` z walidacją reguł budżetowych i terminów.

### 3. `Paddock.Infrastructure`
- Serializacja i deserializacja stanu świata do SQLite (`Microsoft.Data.Sqlite` / Dapper z transakcjami).
- `ContentPackLoader`: wczytywanie i walidacja baz danych JSON (kierowcy historyczni, tory, kalendarze).
- Generator liczb pseudolosowych z jawnym stanem i seedowaniem domenowym.

### 4. `Paddock.Diagnostics` (Paddock Spy / Watcher)
- Centralna szyna zbierania zdarzeń telemetrycznych i motywacji decyzyjnych.
- Generowanie raportów diagnostycznych w formacie JSON i czytelnym Markdownie.

### 5. `Paddock.Cli` (Konsolowe MVP)
- Główny punkt wejścia dla gracza w fazie MVP oparty o `Spectre.Console`.
- Pętla tekstowa: pulpit menedżera, raporty fabryki, rynek kierowców, podgląd weekendu wyścigowego (tekstowa relacja z kwalifikacji i wyścigu) oraz podgląd telemetrii Watchera.

---

## 5. Cykl Życia Gry i Pętla Wykonawcza (Execution Loop)

Gra operuje na dwóch głównych skalach czasu:

```text
               KARIERA (Dni / Tygodnie)
┌─────────────────────────────────────────────────────┐
│  Advance Day -> Aktualizacja Fabryki (R&D)          │
│              -> Negocjacje Kontraktowe              │
│              -> Finanse i Budżet Cap                │
│              -> Wydarzenia Historyczne / Losowe     │
└──────────────────────────┬──────────────────────────┘
                           │ [Gdy dzień wyścigu]
                           ▼
              WEEKEND WYŚCIGOWY (Sesje)
┌─────────────────────────────────────────────────────┐
│ 1. Kwalifikacje (Symulacja sesji i gridu)           │
│ 2. Dobór Strategii (Automatycznie przez Inżynierów) │
│ 3. Wyścig (Deterministyczna symulacja okrążeń)      │
│ 4. Punkty, Kary, Zużycie podzespołów, Nagrody       │
│ 5. Zrzut Telemetrii i Zapis SQLite                  │
└─────────────────────────────────────────────────────┘
```

Wszystkie operacje są wywoływane w trybie synchronicznym i bezgłowym. Użytkownik w CLI może zasymulować wyścig w ułamku sekundy lub włączyć tryb "tekstowej relacji okrążenie po okrążeniu" z regulowaną prędkością.
