# Paddock Principal — Domain Data Model

**Wersja:** 1.0  
**Status:** DRAFT / APPROVED FOR PRE-PRODUCTION  
**Cel:** Definicja modelu danych, encji, atrybutów i relacji w warstwie domenowej `Paddock.Domain`. Wszystkie typy są niemutowalne lub ściśle kontrolowane przez metody biznesowe.

---

## 1. Diagram Relacji Domenowych

```text
┌────────────────┐        1:N        ┌────────────────┐
│     Series     ├───────────────────►   TeamEntry    │
│  (Regulations) │                   │ (Budżet, Auto) │
└───────┬────────┘                   └───────┬────────┘
        │ 1:N                                │ 1:N
        ▼                                    ▼
┌────────────────┐                   ┌────────────────┐        1:1        ┌────────────────┐
│   TrackEvent   │                   │  StaffMember   ├───────────────────►    Contract    │
│  (Kalendarz)   │                   │(Inżynierowie)  │                   │  (Pensja, Typ) │
└────────────────┘                   └───────┬────────┘                   └────────────────┘
                                             │ 1:N
                                             ▼
                                     ┌────────────────┐        1:1        ┌────────────────┐
                                     │     Driver     ├───────────────────►    Contract    │
                                     │  (Atrybuty)    │                   │ (#1, #2, Wiek) │
                                     └───────┬────────┘                   └────────────────┘
                                             │ 1:1
                                             ▼
                                     ┌────────────────┐
                                     │  DriverCareer  │
                                     │(Peak, Potencjał│
                                     └────────────────┘
```

---

## 2. Główne Encje Domenowe

### 2.1. Driver (Kierowca)
Kierowca jest kluczowym aktorem sportowym. Jego atrybuty mieszczą się w przedziale 1–100:

- **Atrybuty Wyścigowe**:
  - `Pace`: Czyste tempo na pojedynczym okrążeniu w idealnych warunkach.
  - `Consistency`: Zdolność do powtarzania równych czasów okrążeń bez wahań.
  - `Focus`: Odporność psychiczna pod presją rywala i unikanie błędów.
  - `WetWeather`: Zdolność do jazdy w deszczu i na przesychającym torze.
  - `TireManagement`: Płynność jazdy redukująca tempo zużycia i przegrzewania opon.
  - `Starts`: Refleks i pozycjonowanie na pierwszym okrążeniu wyścigu.
  - `Aggression`: Skłonność do agresywnych manewrów (zwiększa tempo wyprzedzania, ale podnosi ryzyko kolizji i zużycie sprzętu).
  - `Attacking`: Skuteczność wyprzedzania rywali w strefach hamowania.
  - `Defending`: Zdolność do utrzymania pozycji i blokowania ataków.
  - `Braking`: Precyzja dohamowań do wolnych i średnich zakrętów.

- **Progresja i Cykl Kariery (`DriverCareerProfile`)**:
  - `CareerPhase`: `Rising`, `Peak`, `Plateau`, `Decline`.
  - `PeakAge`: Wiek osiągnięcia maksimum możliwości (zwykle 26–29 lat).
  - `DeclineAge`: Wiek początku powolnego spadku atrybutów fizycznych (zwykle 34–37 lat).
  - `SkillCeiling`: Ukryty maksymalny potencjał (widoczny tylko w Paddock Spy / Developer Mode).
  - `DeclineRate`: Tempo utraty refleksu i tempa po przekroczeniu wieku plateau.

- **Cechy (Traits)**:
  - *Stałe*: `Qualifier` (+bonus w Q), `RainMaster` (+tempo w deszczu), `SmoothOperator` (-15% degradacji opon), `IceCold` (odporność na błędy).
  - *Tymczasowe*: `HighMorale`, `Demotivated`, `MinorInjury`, `ContractDispute`.

---

### 2.2. StaffMember (Personel Zespołu)
Personel ma decydujący wpływ na jakość decyzji automatycznych oraz rozwój bolidu:

- **Role**:
  - `TechnicalDirector`: Nadzoruje projekty R&D, wyznacza kierunek rozwoju pakietu aero i podwozia.
  - `HeadOfStrategy`: Odpowiada za kalkulację planów wyścigowych (1-stop vs 2-stop) oraz reakcje na Safety Car.
  - `RaceEngineer`: Bezpośrednio współpracuje z konkretnym kierowcą; wpływa na tempo adaptacji do toru i komunikację radiową.
  - `PitChief`: Zarządza mechanikami w alei serwisowej.

- **Atrybuty Personelu (1–100)**:
  - `AeroCompetence`, `EngineCompetence`, `ChassisCompetence`.
  - `StrategyCalculation`: Precyzja estymacji degradacji opon i okien wyjazdowych.
  - `Adaptability`: Szybkość i trafność reakcji na nieprzewidziane zdarzenia (deszcz, wypadek).
  - `PitCrewTraining`: Zmniejsza średni czas postoju i redukuje szansę na zacięcie pistoletu do kół.

---

### 2.3. Car & Components (Bolid i Podzespoły)
Bolid składa się z niezależnie rozwijanych komponentów. Każdy komponent posiada ocenę osiągów (1–100), niezawodności (1–100) oraz aktualne zużycie (0–100%):

- `FrontWing`: Wpływa na docisk w zakrętach o średniej i niskiej prędkości oraz stabilność przodu.
- `RearWing`: Wpływa na maksymalny docisk w szybkich łukach oraz opór powietrza (drag) na prostych.
- `FloorAndDiffuser`: Generuje docisk z efektu przyziemnego bez nadmiernego oporu.
- `PowerUnit` (Silnik + ERS): Moc maksymalna, przyspieszenie, elastyczność i prędkość maksymalna.
- `Gearbox` (Skrzynia biegów): Przeniesienie napędu, kluczowy element podatny na awarie mechaniczne.
- `Suspension` (Zawieszenie): Wybieranie nierówności, przyczepność mechaniczna w wolnych sekcjach, zarządzanie oponami.
- `Brakes` (Układ hamulcowy): Droga hamowania, chłodzenie i odporność na fading termiczny.

---

### 2.4. Track (Tor Wyścigowy)
Tor definiuje warunki brzegowe symulacji:
- `TrackId`, `Name`, `Country`.
- `LengthKm`: Długość pojedynczego okrążenia.
- `LapCount`: Liczba okrążeń w wyścigu.
- `BaseLapTimeMs`: Czas bazowy okrążenia (w milisekundach).
- `PitLaneDeltaMs`: Czas stracony na przejazd przez aleję serwisową (z ogranicznikiem prędkości).
- `OvertakingDifficulty`: Mnożnik trudności wyprzedzania (1.0 = łatwe, 3.0 = Monako).
- `TireDegradationFactor`: Chropowatość nawierzchni przyspieszająca zużycie gumy.
- `SuitabilityProfile`: Wagi toru (`StraightWeight`, `HighSpeedAeroWeight`, `MechanicalGripWeight`, `BrakingWeight`).

---

### 2.5. Contract (Kontrakty)
- `ContractId`, `PartyAId` (Team), `PartyBId` (Driver / Staff).
- `SalaryPerYear`: Wynagrodzenie roczne.
- `BonusPerPoint`, `BonusPerWin`.
- `StartSeasonYear`, `ExpirySeasonYear`.
- `StatusPriority`: `FirstDriver`, `EqualStatus`, `SecondDriver`, `ReserveDriver`.
- `BuyoutClause`: Kwota odszkodowania za wcześniejsze zerwanie umowy.

---

### 2.6. SeriesRegulations (Regulacje Serii)
- `PointsSystem`: Punkty za miejsca (np. 25-18-15-12-10-8-6-4-2-1 + 1 za najszybsze okrążenie).
- `BudgetCap`: Roczny limit wydatków (np. $135,000,000 lub brak limitu w erach historycznych).
- `TireRegulations`: Obowiązkowa zmiana mieszanki w trakcie wyścigu (Tak/Nie), dostępne mieszanki.
- `RefuelingEnabled`: Czy dozwolone jest tankowanie w trakcie wyścigu (np. F1 1994 = Tak, F1 2024 = Nie).
- `DrsEnabled`: Czy seria korzysta z ruchomych skrzydeł DRS.
