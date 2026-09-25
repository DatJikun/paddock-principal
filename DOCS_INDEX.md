# Paddock Principal — Master Documentation Index & Governance

**Wersja:** 1.0  
**Status Projektu:** FAZA PLANOWANIA UKOŃCZONA / GOTOWE DO BOOTSTRAPU  
**Lokalizacja:** `C:\Users\mwojn\Desktop\Paddock Principal`

---

## 1. Mapa Dokumentacji Architektonicznej

Dokumentacja projektu została zorganizowana w zestaw ściśle powiązanych specyfikacji technicznych, eliminujących potrzebę zgadywania podczas implementacji kodu:

| Dokument | Obszar | Opis |
| :--- | :--- | :--- |
| **[README.md](file:///C:/Users/mwojn/Desktop/Paddock%20Principal/README.md)** | Start Projektu | Wymagania środowiska, instrukcje uruchomienia CLI MVP oraz komendy SimRunnera. |
| **[ARCHITECTURE.md](file:///C:/Users/mwojn/Desktop/Paddock%20Principal/ARCHITECTURE.md)** | Fundament Systemu | Architektura *headless modular monolith* w .NET 9 / C#, inwarianty determinizmu i podział na biblioteki. |
| **[PADDOCK_SPY_AND_DECISION_TRACING.md](file:///C:/Users/mwojn/Desktop/Paddock%20Principal/PADDOCK_SPY_AND_DECISION_TRACING.md)** | Watcher / Telemetria AI | System śledzenia i wyjaśniania motywacji decyzji AI (transfery, R&D bolidu, automatyczne strategie na torze). |
| **[AI_PRINCIPAL_SYSTEM.md](file:///C:/Users/mwojn/Desktop/Paddock%20Principal/AI_PRINCIPAL_SYSTEM.md)** | Logika AI | Archetypy szefów zespołów, funkcje użyteczności, alokacja budżetów i równanie poświęcenia sezonu. |
| **[RACE_ENGINE_DESIGN.md](file:///C:/Users/mwojn/Desktop/Paddock%20Principal/RACE_ENGINE_DESIGN.md)** | Silnik Wyścigowy | Matematyczny pipeline kwalifikacji i wyścigu, degradacja opon, pogoda oraz automatyczny dobór strategii przez personel. |
| **[DATA_MODEL.md](file:///C:/Users/mwojn/Desktop/Paddock%20Principal/DATA_MODEL.md)** | Model Domenowy | Encje i relacje: Seria, Zespoły, Kierowcy, Personel, Bolidy, Podzespoły, Tory, Regulacje i Kontrakty. |
| **[CONTENT_FORMAT.md](file:///C:/Users/mwojn/Desktop/Paddock%20Principal/CONTENT_FORMAT.md)** | Modding i Paczki Danych | Struktura plików JSON w `content/`, schematy danych, obsługa sezonów historycznych (F1 1994, 2024) i zdarzeń. |
| **[SAVE_FORMAT.md](file:///C:/Users/mwojn/Desktop/Paddock%20Principal/SAVE_FORMAT.md)** | Zapis Stanu Gry | Baza SQLite per kariera, schemat tabel, migracje wersji i kompaktowanie historii. |
| **[DETERMINISM_AND_EVENT_CONTRACTS.md](file:///C:/Users/mwojn/Desktop/Paddock%20Principal/DETERMINISM_AND_EVENT_CONTRACTS.md)** | Determinizm i Zdarzenia | Izolowane strumienie RNG, generator PCG64/Xoshiro, komendy `ICommand` i cykl wykonawczy. |

---

## 2. Kluczowe Zasady Projektowe (Design Principles)

Podczas prac programistycznych obowiązują twarde reguły:

1. **Bezwzględny zakaz logiki gry w interfejsie**: Warstwa konsoli (`Paddock.Cli`) oraz przyszłe UI są wyłącznie pasywnymi czytnikami danych. Wszelkie zmiany stanu zachodzą przez wysłanie `ICommand` do silnika.
2. **Zero magii i ukrytych bonusów**: Wyniki na torze i decyzje AI wynikają wprost z matematyki, atrybutów bolidów, kierowców i personelu.
3. **Zasada Watcher-First**: Każda nowa logika decyzyjna dodawana do AI musi rejestrować rekord `DecisionTrace` przed wykonaniem mutacji stanu.
4. **Nienaruszalność strumieni losowości**: Zmiana w jednym podsystemie (np. negocjacje kontraktu) nie może zmienić wskaźnika losowości w innym podsystemie (np. awarii silnika w wyścigu).

---

## 3. Plan Implementacji: Faza Konsolowego MVP

Kolejność prac programistycznych dla zespołu / agentów kodujących:

```text
Krok 1: Bootstrap Solucji .NET 9
  └── Solucja PaddockPrincipal.sln, biblioteki Paddock.Domain, Paddock.Application,
      Paddock.Infrastructure, Paddock.Diagnostics, Paddock.Cli, Paddock.SimRunner.

Krok 2: Model Domenowy i Izolowane RNG
  └── Implementacja klas encji z DATA_MODEL.md oraz generatorów liczb z DETERMINISM_AND_EVENT_CONTRACTS.md.

Krok 3: Silnik Wyścigowy i Automatyczna Strategia Personelu
  └── Implementacja RaceSimulationEngine (Kwalifikacje + Wyścig) zgodnie z RACE_ENGINE_DESIGN.md.

Krok 4: Paddock Spy (Watcher)
  └── Implementacja szyny rejestrowania DecisionTrace w Paddock.Diagnostics.

Krok 5: Content Loader i Paczka F1 2024 / F1 1994
  └── Parsowanie JSON-ów, walidacja i inicjalizacja stanu początkowego.

Krok 6: Konsolowy Terminal MVP (Paddock.Cli)
  └── Interaktywna pętla kariery w terminalu z użyciem Spectre.Console:
      - Menu główne, podgląd fabryki, rynek transferowy, symulacja wyścigu i podgląd Watchera.

Krok 7: Zapis SQLite (Save System)
  └── Implementacja repozytorium bazy danych SQLite zgodnie z SAVE_FORMAT.md.
```
