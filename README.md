# Paddock Principal

Zaawansowany symulator zarządzania zespołem motorsportowym oparty na architekturze **headless modular monolith** w .NET 9 / C#.

Gra łączy głęboki model matematyczny osiągów bolidu, degradacji opon i dynamiki wyścigu z unikalnym systemem **Paddock Spy (Watcher)** — silnikiem telemetrii decyzyjnej, który w czasie rzeczywistym rejestruje i wyjaśnia motywacje autonomicznych decyzji AI (na rynku kierowców, w fabryce przy rozwoju bolidu oraz na torze przy doborze strategii przez personel inżynieryjny).

---

## Główne Cechy Architektury

- **100% Headless Core**: Czysta biblioteka domenowa w C#, całkowicie odcięta od silników graficznych. Interfejs użytkownika jest wyłącznie pasywnym konsumentem danych.
- **Konsolowe MVP (`Paddock.Cli`)**: Pierwsza wersja gry jest w pełni grywalna w terminalu — interaktywne zarządzanie zespołem, przegląd rynku transferowego, fabryki, tabel mistrzostw oraz symulacja weekendów wyścigowych.
- **Watcher (Paddock Spy)**: Pełna przejrzystość decyzji sztucznej inteligencji. Zero syndromu „czarnej skrzynki” — każdy ruch AI ma udokumentowane uzasadnienie w strukturze `DecisionTrace`.
- **Automatyczna Strategia Personelu**: Wyścigi to realistyczna, automatyczna symulacja kwalifikacji i wyścigu. Za wybór mieszanek, moment zjazdu do boksu, reakcję na Safety Car i deszcz odpowiada zatrudniony sztab (`Race Engineer`, `Head of Strategy`).
- **Data-Driven Content Packs**: Pełna elastyczność i wsparcie dla moddingu. Wszystkie sezony (np. F1 2024, historyczny F1 1994, WEC) definiowane są w plikach JSON z obsługą skryptowanych zdarzeń historycznych.
- **Relacyjne Zapisy w SQLite**: Baza SQLite per kariera (`.paddock`). Natychmiastowe ładowanie, transakcyjność i brak problemów z pamięcią w 50-letnich karierach.
- **Ścisły Determinizm**: Identyczny seed + sekwencja komend = identyczny wynik każdego okrążenia i wyścigu.

---

## Dokumentacja Techniczna i Projektowa

Kompletna specyfikacja architektury znajduje się w poniższych dokumentach:

1. [**ARCHITECTURE.md**](ARCHITECTURE.md) — Architektura techniczna, inwarianty, podział solucji na warstwy i cykl wykonawczy.
2. [**PADDOCK_SPY_AND_DECISION_TRACING.md**](PADDOCK_SPY_AND_DECISION_TRACING.md) — Specyfikacja Watchera: telemetria decyzyjna, schemat `DecisionTrace` i separacja Simulation Truth od wiedzy gracza.
3. [**AI_PRINCIPAL_SYSTEM.md**](AI_PRINCIPAL_SYSTEM.md) — Mózg decyzyjny AI: archetypy szefów zespołów, funkcje użyteczności, rozwój R&D i zasada poświęcenia sezonu.
4. [**RACE_ENGINE_DESIGN.md**](RACE_ENGINE_DESIGN.md) — Silnik wyścigowy: matematyka czasów okrążeń, model opon, brudne powietrze, kwalifikacje i automatyczny dobór strategii przez inżynierów.
5. [**DATA_MODEL.md**](DATA_MODEL.md) — Model domenowy: encje kierowców, personelu, bolidów, podzespołów, torów i kontraktów.
6. [**CONTENT_FORMAT.md**](CONTENT_FORMAT.md) — Format paczek zawartości JSON, sezony historyczne i zdarzenia narracyjne.
7. [**SAVE_FORMAT.md**](SAVE_FORMAT.md) — Baza danych SQLite: schemat tabel, migracje wersji i kompaktowanie historii.
8. [**DETERMINISM_AND_EVENT_CONTRACTS.md**](DETERMINISM_AND_EVENT_CONTRACTS.md) — Izolacja generatorów losowości (RNG streams), komendy `ICommand` i zdarzenia.
9. [**DOCS_INDEX.md**](DOCS_INDEX.md) — Główny indeks dokumentacji i zasady wprowadzania zmian.

---

## Wymagania i Uruchomienie (Docelowe)

- **.NET SDK 9.0** (lub wspierany .NET 8.0 LTS)
- Dowolny system operacyjny: Windows, Linux, macOS.

### Budowanie i testy
```bash
dotnet build PaddockPrincipal.sln
dotnet test PaddockPrincipal.sln
```

### Uruchomienie Konsolowego MVP
```bash
dotnet run --project src/Paddock.Cli
```

### Uruchomienie Bezzwłocznego SimRunnera (Symulacje Wsadowe)
```bash
# Symulacja 5 sezonów w trybie wsadowym z podanym seedem
dotnet run --project tools/Paddock.SimRunner -- simulate --pack f1_2024 --seasons 5 --seed 42069

# Zrzut telemetrii Watchera z ostatniego wyścigu dla wybranego zespołu
dotnet run --project tools/Paddock.SimRunner -- trace --pack f1_2024 --race 3 --team Ferrari
```
