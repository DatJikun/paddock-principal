# Paddock Principal — Paddock Spy & Decision Tracing Framework

**Wersja:** 1.0  
**Status:** DRAFT / APPROVED FOR PRE-PRODUCTION  
**Cel:** Zdefiniowanie zunifikowanego, pasywnego i deterministycznego systemu telemetrii decyzyjnej. Odpowiada na pytanie: *dlaczego AI podjęło daną decyzję* na rynku transferowym, przy rozwoju bolidu, w doborze strategii wyścigowej oraz w zarządzaniu finansami.

---

## 1. Zasada Nadrzędna (North Star)

> **Każda autonomiczna decyzja w Paddock Principal musi być w 100% wyjaśnialna w oparciu o stan wiedzy aktora, jego cele, ograniczenia, poziom zatrudnionego personelu oraz rozważane opcje w ułamku sekundy, w którym została podjęta.**

Watcher (Paddock Spy) istnieje po to, aby wyeliminować syndrom „czarnej skrzynki” w symulatorze. Jeśli zespół AI nagle zatrudnia debiutanta zamiast weterana, albo zjeżdża po opony deszczowe za późno, gracz i deweloper muszą dokładnie widzieć matematyczne i personalne powody tej decyzji.

---

## 2. Dwa Poziomy Wglądu: Developer Spy vs Player-Facing "Why"

Architektura ściśle rozdziela surowy wgląd deweloperski od informacji dostępnych dla gracza w trakcie rozgrywki:

```text
┌─────────────────────────────────────────────────────────────────────────┐
│                           Simulation Truth                              │
│  (Rzeczywisty stan świata, ukryty potencjał kierowców, fabryki rywali)  │
└────────────────────────────────────┬────────────────────────────────────┘
                                     │
                 ┌───────────────────┴───────────────────┐
                 ▼                                       ▼
    ┌──────────────────────────┐            ┌──────────────────────────┐
    │  Paddock Spy (Developer) │            │   Player-Facing "Why"    │
    │  - Pełny wgląd w prawde  │            │  - Mgła wojny (Scouting) │
    │  - Surowe wagi utility   │            │  - Raporty inżynierów    │
    │  - Seedy losowości RNG   │            │  - Komunikaty radiowe    │
    │  - Wewnętrzne stany AI   │            │  - Feedback od agentów   │
    └──────────────────────────┘            └──────────────────────────┘
```

### Developer Spy (Paddock Spy)
Dostępny w narzędziu `Paddock.SimRunner` oraz w trybie debuggowym CLI (`--spy`):
- Widzi rzeczywiste atrybuty wszystkich kierowców (w tym ukryty potencjał i odporność psychiczną).
- Widzi surowe wagi funkcji użyteczności (utility scores) dla każdej odrzuconej opcji.
- Pozwala zweryfikować, czy decyzja AI była logicznym wyborem z perspektywy jej cech, czy błędem w kodzie.

### Player-Facing "Why" (Raporty i Radio w Grze)
Dostępny w normalnej rozgrywce w `Paddock.Cli`:
- Pokazuje uzasadnienia przefiltrowane przez poziom wiedzy zespołu gracza (`AccessContext`).
- Przykład rynku: Agent kierowcy informuje: *„Carlos odrzucił ofertę: zespół nie gwarantuje statusu pierwszego kierowcy, a pakiet aerodynamiczny na torach szybkich budzi wątpliwości”*.
- Przykład wyścigu: Raport inżyniera po wyścigu: *„Wybraliśmy strategię 2-stopów (Soft -> Medium -> Medium), ponieważ przewidywane okno degradacji opon Soft wynosiło tylko 14 okrążeń przy tempie 1:28.4”*.

---

## 3. Kluczowe Domeny Decyzyjne i Ich Śledzenie

### 3.1. Rynek Transferowy i Kontrakty (`Domain.Transfers`)
Śledzi decyzje kierowców, inżynierów i szefów zespołów:
- **Ocena ofert przez kierowcę**: Funkcja użyteczności uwzględniająca:
  - Konkurencyjność bolidu (przewidywana pozycja w mistrzostwach).
  - Wynagrodzenie bazowe i premie za punkty/podium.
  - Status w zespole (#1 vs równy status vs #2).
  - Prestiż zespołu i jakość fabryki.
  - Długość kontraktu i klauzula wykupu.
- **Zwolnienia i przedłużenia umów przez AI**: Analiza stosunku koszt/efekt, bilansu punktowego względem kolegi z zespołu oraz spadku formy związanego z wiekiem (faza Decline).

### 3.2. Badania i Rozwój Bolidu (`Domain.RnD`)
Śledzi decyzje Dyrektorów Technicznych AI:
- **Alokacja CFD i Tunelu Aerodynamicznego**: Wybór komponentu (np. przednie skrzydło, podłoga, sekcje boczne) na podstawie charakterystyki nadchodzących torów w kalendarzu.
- **Zasada Poświęcenia Sezonu (Season Sacrifice)**: Moment w roku, w którym zespół AI kalkuluje, że nie ma szans na poprawę pozycji w klasyfikacji i przenosi 100% zasobów na projektowanie bolidu na kolejny rok (szczególnie krytyczne przed zmianą regulacji technicznych).
- **Ograniczenia Budget Cap**: Zrzucenie śladu decyzyjnego, gdy pakiet poprawek zostaje anulowany lub opóźniony z powodu braku limitu finansowego.

### 3.3. Dobór i Realizacja Strategii Wyścigowej (`Domain.RaceStrategy`)
W konsolowym MVP wszystkie wyścigi to automatyczna symulacja kwalifikacji i wyścigu. Za strategię odpowiada zatrudniony personel (`Race Engineer`, `Head of Strategy`).
Watcher rejestruje:
- **Plan Przedwyścigowy (Pre-Race Plan)**:
  - Dlaczego wybrano strategię 1-stop (Medium -> Hard) zamiast 2-stop (Soft -> Medium -> Soft).
  - Wpływ wiedzy personelu: Doświadczony inżynier (Skill 85+) trafnie kalkuluje tempo degradacji i czasy straty w alei serwisowej; słaby inżynier (Skill 45) przecenia żywotność opon miękkich o 4 okrążenia.
- **Reakcje w Trakcie Wyścigu (In-Race Triggers)**:
  - *Undercut/Overcut*: Decyzja o wcześniejszym zjeździe w odpowiedzi na spadek tempa rywala przed nami.
  - *Safety Car / VSC*: Kalkulacja "taniego pit-stopu" (strata w alei serwisowej zredukowana o np. 40%). Dlaczego bolid zjechał lub został na torze (track position vs świeża guma).
  - *Dynamiczna Pogoda*: Przejście z opon Slick na Intermediate przy wilgotności toru > 30%. Słaby personel zwleka o 2 okrążenia za długo, generując kolosalne straty czasowe.

### 3.4. Finanse i Zarządzanie Fabryką (`Domain.Finance`)
- Wybór sponsorów (płatność gwarantowana vs premie za wysokie pozycje na mecie w zależności od pewności zespołu).
- Inwestycje w fabrykę: budowa nowego tunelu aerodynamicznego vs rozbudowa symulatora dla rozwoju kierowców.

---

## 4. Kontrakt Danych: `DecisionTrace`

Każdy ślad decyzyjny jest reprezentowany przez ustrukturyzowany rekord w C#:

```csharp
public record DecisionTrace(
    Guid DecisionId,
    long TimestampDay,               // Dzień w kalendarzu kariery
    int? RaceIndex,                  // Indeks wyścigu w sezonie (jeśli dotyczy)
    int? LapNumber,                  // Numer okrążenia (dla decyzji wyścigowych)
    DecisionDomain Domain,           // Transfers, RnD, RaceStrategy, Finance, Governance
    string DecisionType,             // np. "ContractEvaluation", "PitWindowTrigger", "SeasonSacrifice"
    string ActingEntityId,           // Id zespołu lub kierowcy
    string? ActorPersonId,           // Id konkretnej osoby podejmującej decyzję (np. Race Engineer)
    int StaffSkillLevel,             // Poziom kompetencji personelu (1-100)
    string TriggerDescription,       // Co wywołało konieczność podjęcia decyzji
    IReadOnlyList<DecisionGoal> Goals,
    IReadOnlyList<string> Constraints,
    IReadOnlyList<OptionEvaluation> EvaluatedOptions,
    string SelectedOptionId,
    string SelectionReason,
    double ConfidenceRating,         // 0.0 - 1.0
    SimulationTruthContext? TruthContext // Dostępny tylko w Developer Spy
);

public record OptionEvaluation(
    string OptionId,
    string Description,
    double UtilityScore,             // Obliczona wartość użyteczności
    IReadOnlyDictionary<string, double> FactorBreakdown // Wagi składowe (np. "Salary": 0.4, "CarPace": 0.8)
);
```

---

## 5. Przykładowy Zrzut Watchera w CLI

Widok z komendy `paddock-cli trace --last-race --team Ferrari`:

```text
[LOKALIZACJA: GP Włoch, Okrążenie 24/53]
DOMENA: RaceStrategy (In-Race Adaptation)
AKTOR: Inżynier Wyścigowy (Riccardo Adami, Poziom: 82)
BIEŻĄCY BOLID: Samochód #16 (Charles Leclerc, P2)

WYZWALACZ: Strata do lidera wynosi 1.8s; opony Medium (C3) osiągnęły 68% degradacji termicznej.
ROZWAŻANE SCENARIUSZE:
  1. Zjazd natychmiast (Undercut, Opony Hard) -> Score: 88.4
     [+ Zysk na świeżej gumie: +1.4s/okr, - Ryzyko utknięcia w ruchu: Niskie, Okno wyjazdu: Czysty tor]
  2. Wydłużenie stintu o 5 okrążeń (Overcut)  -> Score: 41.2
     [- Strata tempa na zużytych oponach: -1.8s/okr, - Zagrożenie ze strony P3]

DECYZJA: Wybrano Opcję 1 (Natychmiastowy zjazd do boksu na okrążeniu 24).
WPŁYW PERSONELU: Inżynier bezbłędnie oszacował okno wyjazdu przed bolidami Williamsa.
```

---

## 6. Przechowywanie i Wydajność (Retention Policy)

Aby baza SQLite nie puchła w nieskończoność w trakcie 50-letniej kariery:
1. **Pamięć bieżąca**: Wszystkie ślady z trwającego weekendu wyścigowego żyją w buforze kołowym w RAM (`RingBuffer<DecisionTrace>`).
2. **Kariera**: Po zakończeniu sezonu szczegółowe ślady okrążeń są agregowane do podsumowań statystycznych. Pełne rekordy `DecisionTrace` są archiwizowane w SQLite tylko dla kluczowych wydarzeń (transfery, mistrzostwa, wielkie awarie) lub zrzucane do opcjonalnego pliku logów JSON/Markdown na żądanie gracza/dewelopera.
3. **Deterministyczna neutralność**: Watcher jest **pasywnym obserwatorem**. Włączenie lub wyłączenie zbierania telemetrii decyzyjnej nie może zużyć ani jednego wywołania generatora liczb losowych (`RngStream`), gwarantując stuprocentową zgodność wyników.
