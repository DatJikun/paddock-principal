# Paddock Principal — AI Team Principal & Management System

**Wersja:** 1.0  
**Status:** DRAFT / APPROVED FOR PRE-PRODUCTION  
**Cel:** Specyfikacja systemów decyzyjnych dla sztucznej inteligencji zarządzającej zespołami motorsportowymi. Definiuje archetypy szefów zespołów, funkcje użyteczności (utility formulas), alokację budżetów R&D oraz logikę transferową.

---

## 1. Filozofia AI: Wyraziste Archetypy zamiast Sztywnego Skryptu

W Paddock Principal zespoły sterowane przez komputer nie korzystają ze sztucznych bonusów finansowych czy skryptów gwarantujących wygraną („Rubber-banding”). 
Każdy zespół AI podlega dokładnie tym samym ograniczeniom regulaminowym (Budget Cap, limity czasu w tunelu aero/CFD, zasady rynkowe). 

Różnice w zachowaniu wynikają z **osobowości i profilu Szefa Zespołu (Team Principal Profile)** oraz sytuacji finansowej i sportowej organizacji.

---

## 2. Archetypy Szefów Zespołów (Principal Archetypes)

Każdy zarządca AI ma sparametryzowany profil decyzyjny:

| Archetyp | Profil / Przykłady | Priorytet R&D | Podejście Transferowe | Skłonność do Ryzyka |
| :--- | :--- | :--- | :--- | :--- |
| **Aggressive Contender** | Zespoły walczące o mistrzostwo (Red Bull, Mercedes) | Maksymalne tempo poprawek bieżących, eksploatacja limitu budżetowego | Ściąganie gwiazd (#1), bezlitosne traktowanie słabszych kierowców | Wysoka (agresywne strategie, praca na krawędzi limitu) |
| **Methodical Builder** | Tradycyjne potęgi w przebudowie (Ferrari, McLaren) | Długoterminowa rozbudowa fabryki i tunelu, zrównoważony rozwój części | Lojalność wobec wychowanków, stabilne długie kontrakty | Umiarkowana (decyzje oparte o twarde dane analityczne) |
| **Tactical Opportunist** | Środek stawki walczący o punkty (Aston Martin, Alpine) | Skupienie na 2-3 kluczowych komponentach dających przewagę na specyficznych torach | Polowanie na doświadczonych weteranów lub utalentowanych odrzutków | Średnio-wysoka (szukanie szans w nietypowych warunkach pogodowych) |
| **Survivalist Backmarker** | Zespoły z końca stawki (Haas, Sauber, Williams) | Ochrona budżetu, unikanie kar za wymianę silników, wczesne przejście na kolejny rok | Kierowcy wnoszący sponsoring (Pay Drivers) lub młodzi juniorzy z akademii | Niska (priorytet: niezawodność bolidu i przetrwanie finansowe) |

---

## 3. Matematyka Decyzji: Funkcje Użyteczności (Utility Functions)

AI podejmuje decyzje wybierając akcję o najwyższym wskaźniku użyteczności $U(A)$.

### 3.1. Ocena Oferty Kontraktowej przez Kierowcę
Gdy kierowca otrzymuje ofertę od zespołu (gracza lub AI), oblicza jej użyteczność według wzoru:

$$U_{contract} = w_{prestige} \cdot P_{team} + w_{car} \cdot C_{projected} + w_{salary} \cdot S_{rel} + w_{status} \cdot St - w_{risk} \cdot R_{team}$$

Gdzie:
- $P_{team}$: Prestiż i historia zespołu (0-100).
- $C_{projected}$: Przewidywana konkurencyjność bolidu w nadchodzącym sezonie na podstawie symulacji osiągów.
- $S_{rel}$: Oferowane wynagrodzenie w relacji do wartości rynkowej kierowcy.
- $St$: Status w zespole:
  - Gwarantowany Kierowca #1: premia dla kierowców z cechą *Alpha/Leader*.
  - Równy status: neutralny dla większości, ujemny dla mistrzów świata.
  - Kierowca #2: potężna kara w utility, akceptowana tylko przez weteranów u schyłku kariery lub debiutantów za sowite wynagrodzenie.
- $R_{team}$: Ryzyko niestabilności (np. częste zmiany szefów, słaby silnik klienta).

Każda odrzucona i zaakceptowana oferta generuje `DecisionTrace` w domenie `Transfers`, wyjaśniając, który czynnik przeważył.

---

### 3.2. Zarządzanie R&D: Alokacja Zasobów i Wybór Części
W każdym cyklu rozwoju bolidu Dyrektor Techniczny AI wylicza deficyt bolidu względem stawki:

$$\Delta Performance_{component} = TargetRank - CurrentRank_{component}$$

Wagi inwestycji zależą od:
1. **Analizy Torów w Kalendarzu**: Jeśli w kolejnych 4 wyścigach dominują tory o wysokim zapotrzebowaniu na docisk w szybkich zakrętach (np. Silverstone, Suzuka), waga badań nad przednim skrzydłem i podłogą rośnie o 40%.
2. **Kalkulacji Sprawności Personelu**: Słaby inżynier projektujący skomplikowane zawieszenie ma wysokie prawdopodobieństwo błędu projektowego (spadek niezawodności bolidu o 15%).

---

### 3.3. Równanie Poświęcenia Sezonu (Season Sacrifice Equation)
Jedna z najważniejszych decyzji strategicznych w wieloletniej karierze. Zespół AI musi zdecydować, w którym wyścigu $R_{curr}$ wyłączyć rozwój bieżącego bolidu i przenieść 100% czasu tunelu/CFD na bolid na kolejny rok.

AI kalkuluje wskaźnik szansy punktowej:

$$SacrificeScore = \frac{MaxPossiblePoints - CurrentPoints}{PointsGapToNextRank} \times RegulationVolatility$$

- Jeśli przed przerwą wakacyjną zespół ma ugruntowaną pozycję (np. pewne P4 w konstruktorach, bez szans na P3 i z bezpieczną przewagą nad P5), $SacrificeScore$ przekracza próg graniczny.
- **Efekt**: AI natychmiast zamraża bieżące pakiety i rzuca pełen budżet na przyszłoroczny koncept. 
- Watcher rejestruje: `Trigger: SeasonSacrifice -> 100% R&D shifted to NextSeasonCar (Reason: Stable P4 standing, major aero regulation overhaul next year)`.

---

## 4. Zarządzanie Sztabem i Delegowanie Uprawnień

Szef Zespołu AI nie zarządza wszystkim osobiście — zatrudnia specjalistów i deleguje odpowiedzialność:

1. **Dyrektor Techniczny (`Technical Director`)**:
   - Decyduje o filozofii bolidu (niski opór powietrza i prędkość na prostych vs maksymalny docisk w zakrętach).
   - Zarządza balansem między osiągami a niezawodnością.
2. **Główny Inżynier Wyścigowy (`Head of Strategy` / `Race Engineer`)**:
   - Przygotowuje domyślne plany na wyścig (1-stop vs 2-stop).
   - W konsolowym MVP to właśnie jego poziom umiejętności (1-100) determinuje, czy zespół zjedzie na pit-stop w idealnym oknie, czy popełni katastrofalny błąd taktyczny.
3. **Szef Załogi Pit-Stopowej (`Pit Chief`)**:
   - Poziom wytrenowania załogi wpływa bezpośrednio na średni czas zmiany kół (np. 2.1s dla elity vs 3.8s dla słabej załogi) oraz prawdopodobieństwo błędu przy dokręcaniu nakrętki.

---

## 5. Spójność ze Standardem Headless

Wszystkie obliczenia AI opierają się o czyste klasy domenowe C# (`AiPrincipalBrain.cs`, `TransferMarketEvaluator.cs`, `RnDAllocationPlanner.cs`). 
Nie ma tu żadnych opóźnień asynchronicznych ani zależności od klatek animacji. Kompletny sezon dla 10 zespołów AI może zostać przeliczony w ułamku sekundy w trybie wsadowym.
