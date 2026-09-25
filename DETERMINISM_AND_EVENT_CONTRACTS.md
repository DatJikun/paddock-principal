# Paddock Principal — Determinism, RNG & Event Contracts

**Wersja:** 1.0  
**Status:** DRAFT / APPROVED FOR PRE-PRODUCTION  
**Cel:** Zdefiniowanie kontraktu deterministyczności, izolacji generatorów liczb pseudolosowych (RNG Streams) oraz architektury komend i zdarzeń domenowych.

---

## 1. Złoty Kontrakt Determinizmu (The Determinism Contract)

> **Ten sam kod silnika + ta sama wersja paczki danych + ten sam Master Seed + identyczna sekwencja komend = w 100% identyczny stan świata i wynik każdego wyścigu.**

Determinizm jest fundamentem, który umożliwia:
1. **Bezbłędny replay i debugowanie**: Jeśli deweloper lub gracz zgłosi nietypowy błąd w wyścigu, wystarczy podać Master Seed i numer rundy, aby odtworzyć identyczny przebieg każdego okrążenia.
2. **Automatyczne testy regresyjne w CI**: SimRunner może zasymulować 100 lat i porównać końcowy hash bazy danych z wartością oczekiwaną. Każda niepożądana zmiana w kodzie matematycznym natychmiast wywoła błąd w testach.
3. **Pasywność Watchera**: Włączenie lub wyłączenie szczegółowego logowania telemetrii nie ma prawa zmienić wyników rywalizacji na torze.

---

## 2. Izolacja Strumieni Losowości (RNG Stream Isolation)

Jednym z najczęstszych błędów w symulatorach jest korzystanie z jednego globalnego `System.Random`. 
W takim błędnym modelu, jeśli gracz otworzy okno negocjacji kontraktowych i wykona dodatkowy rzut kością, przesuwa stan generatora, co sprawia, że w wyścigu za tydzień bolid rywala nie ulegnie awarii, mimo że powinien.

W Paddock Principal losowość jest **ściśle domenowa i izolowana**:

```text
                        ┌──────────────────┐
                        │ Master Seed (64b)│
                        └────────┬─────────┘
                                 │
     ┌──────────────┬────────────┼────────────┬──────────────┐
     ▼              ▼            ▼            ▼              ▼
┌──────────┐  ┌──────────┐ ┌───────────┐ ┌──────────┐ ┌──────────────┐
│  RNG     │  │  RNG     │ │   RNG     │ │  RNG     │ │     RNG      │
│ Weather  │  │ LapNoise │ │ Incidents │ │ Failures │ │ Transfers/AI │
└──────────┘  └──────────┘ └───────────┘ └──────────┘ └──────────────┘
```

### Podział Strumieni (`DomainRngStreams`):
- `RngStream.Weather`: Generuje krzywe opadów deszczu i temperatury asfaltu dla całego weekendu przed rozpoczęciem sesji.
- `RngStream.LapNoise`: Drobna naturalna wariancja czasów okrążeń kierowców (+/- 0.050s).
- `RngStream.Incidents`: Kalkulacja błędów kierowców i kolizji przy manewrach wyprzedzania.
- `RngStream.MechanicalFailures`: Przegrzania silników, awarie skrzyń biegów i układu hydraulicznego.
- `RngStream.PitStopExecution`: Drobne opóźnienia i zacięcia nakrętek podczas wymiany kół.
- `RngStream.TransfersAndAI`: Losowe preferencje życiowe kierowców i decyzje agentów.
- `RngStream.YouthRegens`: Generowanie cech i potencjałów nowych juniorów wchodzących do motorsportu.

Algorytm bazowy: **PCG64** lub **Xoshiro256\*\*** (szybki, 64-bitowy, powtarzalny na każdej platformie procesora x86/ARM).

---

## 3. Kontrakt Komend (`ICommand`)

Wszelkie modyfikacje stanu gry zachodzą wyłącznie przez wysłanie ustrukturyzowanej komendy do szyny aplikacyjnej (`CommandDispatcher`). 
Ani terminal CLI, ani przyszłe UI nie mają prawa edytować pól obiektów świata bezpośrednio.

Przykłady komend:
```csharp
public interface ICommand
{
    Guid CommandId { get; }
    long TimestampDay { get; }
}

public record AdvanceCalendarDayCommand(
    Guid CommandId,
    long TimestampDay
) : ICommand;

public record StartComponentDevelopmentCommand(
    Guid CommandId,
    long TimestampDay,
    string TeamId,
    ComponentType Component,
    int AllocatedWindTunnelHours,
    int AllocatedCfdMegaflops,
    long BudgetAllocatedCents
) : ICommand;

public record SubmitContractOfferCommand(
    Guid CommandId,
    long TimestampDay,
    string TeamId,
    string TargetPersonId,
    long OfferedSalaryCents,
    int ContractYears,
    ContractPriorityRole PriorityRole
) : ICommand;

public record SimulateRaceWeekendCommand(
    Guid CommandId,
    long TimestampDay,
    string TrackId,
    bool BroadcastMode
) : ICommand;
```

---

## 4. Pipeline Przetwarzania Komendy

Każda komenda przechodzi przez trzystopniowy proces:

```text
1. Walidacja (Validator)
   - Czy zespół posiada wystarczający budżet?
   - Czy limit godzin w tunelu nie został przekroczony?
   - Czy kierowca nie ma już podpisanego ważnego kontraktu?
   -> Błąd: Komenda odrzucona (stan świata bez zmian).

2. Rejestracja Motywacji (Watcher / DecisionTrace)
   - Jeśli komendę wysłało AI, zarejestruj dlaczego wybrało tę opcję.

3. Egzekucja (Handler)
   - Deterministyczna mutacja stanu w pamięci.
   - Emisja zdarzenia domenowego (np. `ComponentDevelopmentStartedEvent`).
   - Zapis do bazy SQLite w punkcie kontrolnym.
```

Dzięki temu system jest całkowicie odcięty od prezentacji, w 100% testowalny jednostkowo i odporny na desynchronizację stanu.
