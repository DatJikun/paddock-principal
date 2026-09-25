# Paddock Principal — Race Engine & Simulation Pipeline

**Wersja:** 1.0  
**Status:** DRAFT / APPROVED FOR PRE-PRODUCTION  
**Cel:** Specyfikacja matematycznego, deterministycznego silnika symulacji sesji weekendu wyścigowego (Kwalifikacje + Wyścig). Wszystkie decyzje strategiczne i taktyczne są podejmowane automatycznie na bazie kompetencji zatrudnionego personelu inżynieryjnego.

---

## 1. Architektura Wyścigu: Bezgłowy Pipeline Matematyczny

Silnik wyścigowy (`RaceSimulationEngine`) działa bez interfejsu graficznego jako sekwencja deterministycznych równań różnicowych i stanów dyskretnych.

W konsolowym MVP wyścig nie wymaga mikrozarządzania gracza z okrążenia na okrążenie. Założeniem gry menedżerskiej jest przygotowanie zespołu:
1. Skomponowanie składu kierowców i inżynierów.
2. Rozwój podzespołów bolidu i ustawienie balansu pod tor.
3. Wybór personelu strategicznego (`Race Engineer`, `Head of Strategy`).
4. **Automatyczna egzekucja**: Sztab inżynieryjny kalkuluje optymalny plan, reaguje na incydenty, pogodę i zużycie opon, a Watcher zrzuca telemetrię wyjaśniającą każdą decyzję.

---

## 2. Model Osiągów: Performance Pipeline

Czas okrążenia w warunkach idealnych nie jest losowany z kapelusza. Powstaje w wielowarstwowym pipeline:

```text
Track Base Lap Time (np. 80.000s)
  - Indywidualne Dopasowanie Bolidu (Aero + Silnik + Zawieszenie * Wagi Toru)
  - Wpływ Umiejętności Kierowcy (Pace + Consistency + Braking)
  + Waga Paliwa (+0.035s / kg)
  + Zużycie i Degradacja Termiczna Opon
  + Wpływ Brudnego Powietrza (Dirty Air / Traffic Penalty)
  - Zysk z Otwartego DRS (-0.650s na okrążenie w strefie)
  + Stan Toru i Warunki Pogodowe (Deszcz / Przesychanie)
  + Deterministyczna Zmienność Losowa (RNG Lap Noise: +/- 0.050s)
  = Wynikowy Czas Okrążenia (Lap Time)
```

### 2.1. Dopasowanie Bolidu do Profilu Toru (Track Suitability)
Każdy tor posiada profil wagowy (suma wag = 1.0):
- $w_{straight}$: Znaczenie mocy silnika i niskiego oporu (np. Monza = 0.45).
- $w_{highSpeed}$: Znaczenie docisku aerodynamicznego w szybkich łukach (np. Silverstone = 0.40).
- $w_{lowSpeed}$: Znaczenie przyczepności mechanicznej i zawieszenia (np. Monako = 0.45).
- $w_{braking}$: Stabilność na dohamowaniach.

Osiągi bolidu $P_{car}$ wyliczane są jako iloczyn skalarny parametrów bolidu i wag toru:

$$P_{car} = \sum_{k} (Rating_{component, k} \times Weight_{track, k})$$

Dzięki temu bolid z mocnym silnikiem, ale słabym dociskiem wygra na Monzy, ale spadnie na koniec stawki na Hungaroringu.

---

## 3. Fizyka Degradacji Opon (Tire Model)

Model opon opiera się na dwóch zjawiskach:
1. **Zużycie strukturalne (Linear Wear)**: Równomierny spadek bieżnika na okrążenie w zależności od agresywności kierowcy i chropowatości asfaltu toru.
2. **Klif termiczny (Degradation Cliff)**: Po przekroczeniu granicy zużycia (np. 70% dla Soft, 80% dla Hard) opona drastycznie traci przyczepność:

$$\Delta Time_{tire} = BaseDelta_{compound} + (\frac{Wear}{100})^2 \times CliffMultiplier$$

| Mieszanka | Osiągi początkowe ($\Delta$ bazowa) | Trwałość bazowa | Podatność na przegrzanie |
| :--- | :--- | :--- | :--- |
| **Soft (C5/C4)** | -0.900 s/okrążenie | 12 - 18 okrążeń | Bardzo wysoka |
| **Medium (C3)** | -0.450 s/okrążenie | 22 - 30 okrążeń | Średnia |
| **Hard (C2/C1)** | 0.000 s (Referencyjna) | 35 - 50 okrążeń | Niska |
| **Intermediate** | Optymalne na wilgotnym torze (15-60% wody) | Niszczeją błyskawicznie na suchym asfalcie | - |
| **Wet** | Optymalne na zalanym torze (> 60% wody) | Przegrzewają się i niszczą przy braku wody | - |

---

## 4. Automatyczny Dobór Strategii Przez Personel (Staff-Driven Strategy)

Zgodnie z wymogiem MVP, strategie są kalkulowane automatycznie przez zatrudniony sztab (`Head of Strategy` oraz `Race Engineer`).

### 4.1. Algorytm Przedwyścigowy (Pre-Race Optimizer)
Przed startem inżynierowie symulują wirtualny wyścig dla wszystkich legalnych kombinacji pit-stopów (1-stop, 2-stop, 3-stop):

$$TotalRaceTime = \sum_{lap=1}^{N} LapTime(lap, compound) + (Stops \times PitLaneDelta)$$

Gdzie:
- $PitLaneDelta$: Czas przejazdu przez aleję serwisową z ogranicznikiem prędkości (zwykle 20-25s) + czas wymiany kół przez mechaników (`PitCrewLevel`).

### 4.2. Wpływ Kompetencji Personelu na Jakość Decyzji
Poziom umiejętności sztabu (skala 1-100) bezpośrednio wpływa na celność kalkulacji i podatność na błędy:

- **Personel Elitarny (Skill 85 - 99)**:
  - Bezbłędna estymacja okna degradacji (z dokładnością do 0.5 okrążenia).
  - Natychmiastowe reakcje na Safety Car (darmowy pit-stop, gdy strata wynosi tylko ~12s zamiast 22s).
  - Idealne przewidywanie okna wyjazdowego po pit-stopie (unikanie utknięcia w pociągu DRS za wolniejszymi autami).
- **Personel Przeciętny (Skill 65 - 84)**:
  - Dobre strategie bazowe, ale sztywne trzymanie się planu A.
  - Zdarza się opóźnienie zjazdu o 1-2 okrążenia po wejściu na "klif oponiarski".
- **Personel Słaby / Tani (Skill 30 - 64)**:
  - Błędy w szacowaniu tempa zużycia opon (przecenienie żywotności opon miękkich).
  - **Ryzyko wpadki strategicznej (Strategic Blunder)**: 
    - Zjazd na pit-stop okrążenie przed ogłoszeniem neutralizacji lub tuż po jej zakończeniu.
    - Zastosowanie złej mieszanki w niepewnych warunkach pogodowych.
    - Wypuszczenie kierowcy prosto w najgęstszy ruch na torze.

Każdy taki błąd lub sukces jest rejestrowany w Watcherze (`DecisionTrace` z jawnym parametrem `StaffSkillLevel`).

---

## 5. Przebieg Sesji: Kwalifikacje i Wyścig

### 5.1. Kwalifikacje
- Każdy kierowca wykonuje okrążenia pomiarowe na oponach miękkich przy minimalnym ładunku paliwa (5-8 kg).
- Czas okrążenia kwalifikacyjnego zależy od atrybutu `Pace`, `Qualifier Trait` oraz opanowania pod presją (`Focus`).
- Błąd kierowcy (zblokowane koło, wyjazd poza limity toru) powoduje stratę od 0.150s do unieważnienia okrążenia.
- Wynikiem kwalifikacji jest uporządkowana lista pozycji startowych (Starting Grid).

### 5.2. Symulacja Wyścigu (Krok po Kroku)
Pętla wyścigu wykonuje się okrążenie po okrążeniu:
1. **Początek okrążenia**: Aktualizacja ładunku paliwa (-1.6 kg/okr) oraz stanu toru (temperatura, poziom wody).
2. **Kalkulacja tempa**: Obliczenie czasów sektorów dla wszystkich bolidów.
3. **Interakcje bezpośrednie (Ruch i Wyprzedzanie)**:
   - Jeśli bolid B jedzie < 1.0s za bolidem A:
     - Wpływ brudnego powietrza w zakrętach: strata czasu i wyższa degradacja opon.
     - Strefa DRS: jeśli tor posiada DRS i bolid jest w oknie 1.0s, uzyskuje bonus do prędkości na prostej.
     - Porównanie `Driver.Attacking` vs `Driver.Defending` + przewaga tempa opon. Przy sukcesie następuje zmiana pozycji.
4. **Incydenty i Awarie**:
   - Szansa na kolizję (zależna od `Driver.Aggression`, pogody i charakterystyki toru).
   - Szansa na awarię mechaniczną (zależna od stanu zużycia silnika/skrzyni i jakości fabryki).
   - Skutek: Żółta flaga, Wirtualny Samochód Bezpieczeństwa (VSC), Samochód Bezpieczeństwa (SC) lub Czerwona Flaga.
5. **Krok Strategiczny**:
   - Przegląd stanu bolidu przez algorytm inżyniera wyścigowego.
   - Decyzja o ewentualnym zjeździe do alei serwisowej na bieżącym okrążeniu.
   - Obsługa postoju (czas pit-stopu + kara za błąd mechanika).
6. **Rejestracja Telemetrii**: Zapisanie czasów okrążeń, pozycji i zdarzeń do bufora pamięci.

---

## 6. Prezentacja w Konsoli MVP

W terminalu `Paddock.Cli` użytkownik może uruchomić wyścig na dwa sposoby:
- **Instant Result (`--instant`)**: Błyskawiczne przeliczenie całego weekendu w 15 milisekund i wyświetlenie końcowych wyników, punktów i raportu inżyniera.
- **Live Text Broadcast (`--live`)**: Tekstowa relacja na żywo z odświeżaniem w terminalu (Spectre.Console Live Table): aktualne pozycje, różnice czasowe (gaps), piktogramy zużycia opon, zjazdy do boksu oraz komunikaty radiowe Watchera.
