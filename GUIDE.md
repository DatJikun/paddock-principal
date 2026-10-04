# Jak działa gra

Każdy system gry w skrócie: jak działa, o czym decydujesz, co masz poczuć w grze i którą liczbę zmienić, jeśli coś nie gra. Pełny projekt: [DESIGN.md](DESIGN.md). Decyzje: [VISION.md](VISION.md).

```pola
Liczby | wszystkie to szacunki | chyba że wiersz mówi „skalibrowane”
Źródło liczb | kod gry, czytany przy każdym budowaniu tej strony | nie da się ich rozjechać z grą
Uwagi | napisz, co czujesz w grze | np. „sponsorzy podpisują z rywalami za szybko”
Precyzyjnie | nazwa z tabeli + nowa wartość | np. `RivalSignChance` z 2% na 1%
```

```pola
Działa | napisane i przetestowane
Działa, podłączanie do kariery | system gotowy, pętla kariery go jeszcze nie woła (#172)
Zaprojektowane | opisane w DESIGN, bez kodu
Później | po MVP
```

---

## 1. Start kariery

```stan dziala
Ekran tworzenia kariery powstanie razem z UI (faza 6).
```

Wybierasz rok, zespół i to, jak mocno świat trzyma się historii (PP-046).

### Osie ustawień

```pola
Ludzie | trajektoria · potencjał · prawdziwe nazwiska, losowe umiejętności · generowani
Przepisy | historyczne · głosowane co sezon
Zachowanie AI | odtwarza historię · reaguje · losowo
Siła historii | 0–100 | jak chętnie AI powtarza prawdziwe zdarzenia
Losowość | 0–100 | rozwój, forma, awarie
Śmiertelność | domyślnie wyłączona | PP-006
```

### Presety

```porownanie Trajektoria | Prawdziwy potencjał
Kierowca jest tak dobry | jak naprawdę w danym roku | jak pozwoli mu Twój świat
Prawdziwa kariera to | krzywa rok po roku | sufit talentu
Preset | Najbardziej historyczny | Zbalansowany (domyślny)
```

```pola
MVP | sezon 1955, przejęty zespół | PP-050
Stawka | 2 auta i 2 stałych kierowców na zespół | bez przesiadek w sezonie
```

```wgrze
Czołówka 1955 wygląda obco (Mercedes słaby, Fangio daleko) | siła aut na starcie, oceny kierowców (rozdział 5)
Po kilku sezonach świat wygląda losowo | test wierności historii (PP-012)
```

---

## 2. Czas i skrzynka

```stan dziala
```

Czas płynie dzień po dniu. „Dalej” przewija do najbliższej sprawy (PP-016).

```kroki
Dalej | dni bez wydarzeń mijają od razu
Sprawa w skrzynce | wiadomość nie blokuje, decyzja zatrzymuje czas
Wybór opcji | skutek widać przed kliknięciem
Potwierdź | dopiero to wykonuje decyzję
```

### Decyzje z terminem

```pola
Koncepcja gotowa: produkcja czy dalszy rozwój | po {DevelopmentEstimates.ConceptDecisionDays|dni} | domyślnie: rozwijamy dalej
Inżynier prosi o czas po zmianie podziału | po {DevelopmentEstimates.ReplyDays|dni} | domyślnie: trzymamy plan
Oferta pracy po zwolnieniu | po {BoardEstimates.OfferValidDays|dni} | wygasa jako odrzucona
Odpowiedź w negocjacjach | po {NegotiationEstimates.ResponseHoldDays|dni} | wygasa
```

```wgrze
Skrzynka zatrzymuje co chwilę bez ważnego powodu | napisz, jaka to była sprawa
```

---

## 3. Kierowcy

```stan dziala
Cechy (mistrz deszczu, niszczyciel opon…) są zaprojektowane, jeszcze nie działają w wyścigu.
```

11 atrybutów 1–20 i gwiazdki. Pełny opis atrybutów: DESIGN §6.1.

```pola
Atrybuty | 1–20 | zakręty, hamowanie, płynność, wyprzedzanie, obrona, regularność, opanowanie, adaptacja, deszcz, kondycja, informacja zwrotna
Gwiazdki | średnia atrybutów ÷ 4 | 20 = 5★, 10 = 2,5★ (PP-047)
Ocena ogólna | 1–100, średnia ważona | wagi zależą od epoki
Doświadczenie | liczniki, nie atrybut | starty, okrążenia na torze, wyścigi w deszczu, sezony w zespole
Cudzy kierowca | pasmo, np. 12–16 | zawęża się z obserwacją (rozdział 6)
```

```wykres wagi-oceny
W latach 50. płynność i kondycja ratowały wyścig, dziś więcej daje regularność i informacja zwrotna.
```

```wgrze
3★ z lat 50. jeździ jak 4★ (albo odwrotnie) | wagi epoki, skala ocen (rozdział 5)
```

```strojenie src/Paddock.Domain/People/GenerationEstimates.cs
EarlyEraLastYear | Ostatni rok „wczesnych” wag oceny | 
ModernEraFirstYear | Pierwszy rok „nowoczesnych” wag oceny | 
EarlySmoothnessMeanBonus | Fikcyjni kierowcy do 1960: premia do płynności | pkt
EarlyFitnessMeanBonus | Fikcyjni kierowcy do 1960: premia do kondycji | pkt
ModernFeedbackMeanBonus | Fikcyjni kierowcy od 1994: premia do informacji zwrotnej | pkt
```

---

## 4. Wiek: rozwój, szczyt, spadek, emerytura

```stan dziala
Łuk wylicza pipeline ocen. Zmiana atrybutów sezon po sezonie (poza pulą talentów) jeszcze nie działa.
```

Kształt jest wspólny dla wszystkich. Dane decydują tylko, jak wysoko i kiedy (PP-047).

```kroki
Rozwój | od {RatingsCareerArc.YearsOfGrowthBeforeDebut|lat} przed debiutem w F1
Szczyt | {RatingsCareerArc.MinPeakAge}–{RatingsCareerArc.MaxPeakAge} lat, z najlepszego sezonu
Plateau | do początku spadku
Spadek | od {RatingsCareerArc.DeclineStartMin}–{RatingsCareerArc.DeclineStartMax} lat, {RatingsCareerArc.DeclineLevelsPerYear|pkt} poziomu na rok
```

```wykres luk-kariery
Kto odszedł na szczycie (Fangio), słabnie dopiero po ostatnim prawdziwym sezonie.
```

### Emerytura

```pola
Prawdziwy kierowca | 31 grudnia ostatniego prawdziwego sezonu | jeśli w Twoim świecie wciąż jeździ
Pozostali kierowcy | szansa od {CareerDayEstimates.DriverRetirementFromAge|lat} | pewna w wieku {CareerDayEstimates.DriverRetirementCertainAge|lat}
Personel | szansa od {CareerDayEstimates.StaffRetirementFromAge|lat} | pewna w wieku {CareerDayEstimates.StaffRetirementCertainAge|lat}
```

```wykres emerytura
```

```wgrze
40-latek wygrywa seryjnie | początek albo tempo spadku
23-letni talent nie rośnie z sezonu na sezon | rozwój przed szczytem
```

```strojenie tools/Paddock.DataPipeline/Ratings/RatingsCareerArc.cs
YearsOfGrowthBeforeDebut | Lat rozwoju przed debiutem w F1 | lat
MinPeakAge | Najwcześniejszy szczyt | lat
MaxPeakAge | Najpóźniejszy szczyt | lat
DeclineStartMin | Najwcześniejszy początek spadku | lat
DeclineStartMax | Najpóźniejszy początek spadku | lat
DeclineLevelsPerYear | Spadek poziomu (1–20) na rok | pkt
AtPeakTolerance | Ostatni sezon tyle poniżej szczytu = „odszedł na szczycie” | pkt
```

```strojenie src/Paddock.Domain/Career/CareerDayEstimates.cs
DriverRetirementFromAge | Kierowca: od tego wieku może odejść | lat
DriverRetirementCertainAge | Kierowca: w tym wieku odchodzi na pewno | lat
StaffRetirementFromAge | Personel: od tego wieku może odejść | lat
StaffRetirementCertainAge | Personel: w tym wieku odchodzi na pewno | lat
```

```strojenie src/Paddock.Domain/People/GenerationEstimates.cs
DriverGrowthStartMin | Fikcyjny kierowca: najwcześniejszy start rozwoju | lat
DriverYearsToPeakMin | Fikcyjny kierowca: najmniej lat do szczytu | lat
DriverYearsToPeakMaxExclusive | Fikcyjny kierowca: lat do szczytu, górna granica (bez niej) | lat
DriverPlateauMaxExclusive | Fikcyjny kierowca: plateau, górna granica (bez niej) | lat
DriverDeclineMilliMin | Fikcyjny kierowca: najwolniejszy spadek (tysięczne oceny ogólnej na rok) | 
DriverDeclineMilliMaxExclusive | Fikcyjny kierowca: najszybszy spadek, górna granica | 
```

---

## 5. Oceny prawdziwych kierowców

```stan dziala
Pipeline danych (oceny v1). Kalibracja trwa, raport w #131.
```

Nikt nie wpisuje ocen ręcznie. Gra liczy je z wyników od 1950 (PP-011).

```kroki
Pojedynek z partnerem | to samo auto, więc różnica mówi o kierowcy
Sieć partnerów | zmiany zespołów łączą całą historię
Względem epoki | porównanie ze stawką swoich czasów
Mało danych | kierowca przyciągany do średniej
```

```wykres skala-ocen
Przeciętny kierowca F1 swojej epoki ma poziom 12. Powyżej średniej krzywa zbliża się do 20, ale jej nie przekracza.
```

```wykres przyciaganie
To przyciąganie obniżyło zbyt wysoką ocenę Castellottiego.
```

```wgrze
Znany kierowca jest wyraźnie za wysoko albo za nisko | podaj nazwisko: model albo plik ręcznych korekt
Wielcy kierowcy wszyscy mają prawie 5★ | `SaturationSd`
Kierowcy z krótką karierą są zawyżeni | `ShrinkHalfDuels`
```

```strojenie tools/Paddock.DataPipeline/Ratings/RatingsMapping.cs
LevelAtFieldMean | Poziom przeciętnego kierowcy epoki | 
LevelsPerSd | Poziomy na odchylenie poniżej średniej | 
SaturationSd | Jak szybko krzywa dochodzi do 20 (mniej = szybciej) | 
```

```strojenie tools/Paddock.DataPipeline/Ratings/RatingsEraScale.cs
ShrinkHalfDuels | Pojedynki, przy których zostaje połowa przewagi | 
WindowYears | Sąsiednie sezony w porównaniu ze stawką | lat
MinFieldSize | Najmniejsza stawka do porównania | 
```

---

## 6. Pula talentów i skauci

```stan toku
```

Ludzie wchodzą do gry przez pulę: abstrakcyjny świat poza F1 (PP-018).

```pola
Prawdziwy kierowca | {PeopleScheduleRules.DefaultPoolLeadYears|lat} przed debiutem | najwcześniej w wieku {PeopleScheduleRules.PoolMinimumAgeYears|lat}
Fikcyjni juniorzy | do {PoolEstimates.TargetSize|osób} w puli | co sezon; AI nie wie, kto jest prawdziwy
Odpada | po {PoolEstimates.MaxSeasonsInPool|sezonów} bez kontraktu albo po {PoolEstimates.MaxAge|lat} | trafia do kroniki
Rozwój | {PoolEstimates.DevelopmentRatePercent|%%} dystansu do potencjału na sezon | najwyżej {PoolEstimates.MaxAnnualStep|pkt} na atrybut
```

```wykres pula-rozwoj
Program przyspiesza rozwój, ale nie podnosi sufitu.
```

```wybory
Obserwuj całą pulę | wolno, wszyscy naraz
Obserwuj jedną osobę | szybko, jedno pasmo się zawęża
Opłać sezon juniorski | tani i wolny albo drogi i szybki
Podpisz | kierowca wyścigowy, testowy albo junior z opcją
```

```wykres pasmo-skauta
Pasmo potencjału jest szersze niż pasmo atrybutu. Słaby skaut może się mylić.
```

```wgrze
Pula pełna pewniaków albo samych wypełniaczy | proporcje jakości fikcyjnych juniorów
Junior z programem nie jest gotowy po 2–3 sezonach | `DevelopmentRatePercent`, tempo programów
```

```strojenie src/Paddock.Domain/Pool/PoolEstimates.cs
TargetSize | Do ilu osób pula jest uzupełniana | osób
MaxSeasonsInPool | Sezony bez kontraktu do odpadnięcia | sezonów
MaxAge | Wiek odpadnięcia | lat
DevelopmentRatePercent | Część dystansu do potencjału na sezon | %%
MaxAnnualStep | Najwięcej punktów na atrybut w sezonie | pkt
LuckMinPercent | Najgorsze szczęście sezonu | %%
LuckMaxPercent | Najlepsze szczęście sezonu | %%
CheapSlowSpeedPercent | Tempo w tanim programie | %%
ExpensiveFastSpeedPercent | Tempo w drogim programie | %%
StartHalfWidth | Połowa pasma na starcie obserwacji | pkt
MinHalfWidth | Najwęższa połowa pasma | pkt
PersonFocusMilliPerMonth | Obserwacja jednej osoby na miesiąc (tysięczne punktu) | 
PoolFocusMilliPerMonth | Obserwacja całej puli na miesiąc (tysięczne punktu) | 
SharedRaceMilli | Obserwacja za wspólny wyścig (tysięczne punktu) | 
WorstScoutBias | Największy błąd najgorszego skauta | pkt
```

---

## 7. Kontrakty i negocjacje

```stan toku
Osobowość jest jeszcze wyliczana z ziarna, a nie z bazy ludzi.
```

Nie ma „wartości rynkowej” (PP-035). Kierowca ocenia ofertę według swojej osobowości.

### Co waży oferta

```pola NegotiationEstimates
Prestiż zespołu | waga {NegotiationEstimates.WeightPrestige}
Przewidywane auto | waga {NegotiationEstimates.WeightCar} | punkty, wygrane, szansa na tytuł
Pensja | waga {NegotiationEstimates.WeightSalary} | względem typowej pensji tego poziomu w epoce
Status | waga {NegotiationEstimates.WeightStatus} | #1 = 1, równy = {NegotiationEstimates.StatusScoreEqual}, #2 = {NegotiationEstimates.StatusScoreNumberTwo}
Ryzyko (minus) | waga {NegotiationEstimates.WeightRisk} | długość, opcje, klauzula wyjścia
```

### Przebieg

```kroki
Oferta | pensja, lata, status, opcje, klauzule
Odpowiedź | po kilku dniach, najwcześniej po {NegotiationEstimates.ResponseDelayMinDays|dni}
Rundy | {NegotiationEstimates.MinRounds}–{NegotiationEstimates.MaxRounds}, zależnie od charakteru
Decyzja | najlepsza oferta; przy remisie zaufanie
```

```pola
Drobna podwyżka | traci zainteresowanie | poniżej {NegotiationEstimates.MinMeaningfulImprovementPercent|%%} to nie jest zmiana
Kontrakt rywala | można ruszyć {NegotiationEstimates.NegotiationWindowDays|dni} przed końcem
Najdłuższy kontrakt | {NegotiationEstimates.MaxYears|sezonów}
Zerwanie umowy | płacisz {NegotiationEstimates.TerminationShare|%} reszty pensji
```

```wgrze
Wszyscy podpisują wszystko | `ReservationUtility` (minimum, poniżej którego kierowca czeka)
Gwiazdor idzie do zespołu z końca stawki za samą kasę | `WeightSalary`, `WeightCar`
Negocjacje frustrują | `NudgePenalty`, liczba rund
```

```strojenie src/Paddock.Domain/Contracts/NegotiationEstimates.cs
WeightPrestige | Waga prestiżu | 
WeightCar | Waga auta | 
WeightSalary | Waga pensji | 
WeightStatus | Waga statusu | 
WeightRisk | Waga ryzyka | 
StatusScoreNumberTwo | Wartość statusu #2 | 
ReservationUtility | Minimum do podpisu | 
AcceptanceMargin | O ile oferta musi przebić alternatywę | 
MinRounds | Najmniej rund | rund
MaxRounds | Najwięcej rund | rund
NudgePenalty | Utrata zainteresowania za kosmetyczną poprawkę (tysięczne) | 
MinMeaningfulImprovementPercent | Najmniejsza realna podwyżka | %%
ResponseDelayMinDays | Najkrótsza odpowiedź | dni
NegotiationWindowDays | Okno przed końcem kontraktu | dni
MaxYears | Najdłuższy kontrakt | sezonów
TerminationShare | Odprawa przy zerwaniu | %
ParallelBase | Negocjacje naraz bez wprawnego szefa | 
```

---

## 8. Auto: koncepcja

```stan dziala
Przełożenie osi koncepcji na osiągi to propozycja do Twojej oceny.
```

Auto to wektor osiągów. Koncepcja to sześć osi i każda ma swoją cenę (DESIGN §5.2).

```pola
Osiągi | moc · docisk · przyczepność mechaniczna · hamowanie · niezawodność | docisk ograniczony epoką
Osie koncepcji | aero · filozofia · okno pracy · chłodzenie · opony · silnik
Dopasowanie do kierowcy | balans, trakcja, styl hamowania | rozjazd kosztuje {CarEstimates.PaceSecondsPerMismatch|s} na okrążeniu za jednostkę
Zrozumienie | nowa koncepcja startuje z {CarEstimates.NewConceptUnderstanding} na 100 | rośnie z kilometrami i pracą
```

### Ewolucja czy rewolucja

```porownanie Ewolucja | Rewolucja
Średni sufit | {CarEstimates.EvolutionCeilingMean} | {CarEstimates.RevolutionCeilingMean}
Rozrzut sufitu | ±{CarEstimates.EvolutionCeilingSd} | ±{CarEstimates.RevolutionCeilingSd}
Auto na start | {CarEstimates.EvolutionStartFraction|%} sufitu | {CarEstimates.RevolutionStartFraction|%} sufitu
```

```wykres koncepcja
Nikt nie zna sufitu dokładnie, także dyrektor techniczny.
```

```wgrze
Rewolucja opłaca się zawsze | sufit albo start rewolucji
Mistrz jest szybki w każdym aucie | `PaceSecondsPerMismatch`
```

```strojenie src/Paddock.Domain/Cars/CarNumbers.cs
EvolutionCeilingMean | Średni sufit ewolucji (0–100) | 
RevolutionCeilingMean | Średni sufit rewolucji | 
EvolutionCeilingSd | Rozrzut ewolucji | 
RevolutionCeilingSd | Rozrzut rewolucji | 
EvolutionStartFraction | Start ewolucji (część sufitu) | %
RevolutionStartFraction | Start rewolucji (część sufitu) | %
ExecutionFloor | Najsłabszy personel dowozi tę część sufitu | %
NewConceptUnderstanding | Zrozumienie nowej koncepcji (na 100) | 
PaceSecondsPerMismatch | Strata za niedopasowanie do kierowcy | s
ConfidencePerMismatch | Utrata pewności siebie za niedopasowanie | 
```

---

## 9. Rozwój auta

```stan toku
Ekran jest w prototypie („Auto i rozwój”). Ręczny wybór projektów i przełomy (PP-042) przyjdą później.
```

Nie wybierasz części. Dzielisz zasoby, a projekty wybierają inżynierowie (PP-043).

```pola DevelopmentEstimates
Bieżące auto | domyślnie {DevelopmentEstimates.DefaultCurrentPercent|%%} | poprawki na ten sezon
Konto rozwoju | domyślnie {DevelopmentEstimates.DefaultAccountPercent|%%} | wiedza na później
Przyszły rok | domyślnie {DevelopmentEstimates.DefaultNextYearPercent|%%} | nowa koncepcja
Priorytety | aerodynamika · podwozie · niezawodność · opony | 0–10
```

```pola
Zysk | część dystansu do sufitu koncepcji | blisko sufitu każda dziesiątka kosztuje więcej
Ludzie | skracają czas | nie podnoszą jakości
Porażka | {DevelopmentEstimates.BaseRisk|%} szansy przed umiejętnościami | koncepcja {DevelopmentEstimates.ConceptRiskMultiple}× ryzykowniejsza
Projekty naraz | 1 na {DevelopmentEstimates.HeadcountPerSlot|osób} inżynierów | najwyżej {DevelopmentEstimates.MaxSlots}
```

```wykres konto-rozwoju
Zmiana przepisów zabiera dodatkową część konta.
```

### Nowa koncepcja

```kroki
Gotowa | decyzja w skrzynce: produkcja czy dalszy rozwój
Produkcja | koszt {DevelopmentEstimates.ConceptProductionCostShare|%} kosztu rozwoju, płatny od razu, bez anulowania
Stare auto jedzie | wyścigi do końca produkcji
Nowe auto | pierwszego dnia po produkcji, nigdy w środku weekendu
```

```wykres czas-produkcji
```

```wybory
Podział zasobów | co dostaje bieżące auto, konto i przyszły rok
Priorytety obszarów | czym inżynierowie zajmą się najpierw
Zatwierdzić czy czekać | punkty teraz albo lepsze auto później
Inżynier prosi o czas | trzymać plan albo ciąć projekt (zostaje to, co zrobiono)
```

```wgrze
Wszyscy rozwijają się w tym samym tempie | `MaxShare`, sufity koncepcji
Konto nigdy się nie opłaca | `AccountDailyDecay`
Nowe auto przychodzi za późno | czas produkcji, `ConceptProductionBaseDays`
```

```strojenie src/Paddock.Domain/Development/DevelopmentEstimates.cs
DefaultCurrentPercent | Podział domyślny: bieżące auto | %%
DefaultAccountPercent | Podział domyślny: konto | %%
DefaultNextYearPercent | Podział domyślny: przyszły rok | %%
UpgradeBaseDays | Czas poprawki | dni
ResearchBaseDays | Czas badań na konto | dni
ConceptBaseDays | Czas projektu koncepcji | dni
MaxShare | Najwięcej zapasu w jednym projekcie | %
BaseRisk | Szansa porażki poprawki | %
ConceptRiskMultiple | Ile razy ryzykowniejsza koncepcja | 
AccountDailyDecay | Dzienna utrata wartości konta | %
RuleChangeLossScale | Utrata konta przy zmianie przepisów | 
ConceptProductionBaseDays | Czas produkcji w 1950 | dni
ConceptProductionCostShare | Koszt produkcji (część kosztu rozwoju) | %
ConceptDecisionDays | Termin decyzji „produkcja czy rozwój” | dni
CloseProgress | Od tego postępu inżynier prosi o czas | %
ReplyDays | Termin odpowiedzi na prośbę inżyniera | dni
AdaptationYears | Lata do pełnego wdrożenia inżyniera | lat
HeadcountPerSlot | Inżynierów na jeden projekt naraz | osób
MaxSlots | Najwięcej projektów naraz | 
```

---

## 10. Dostawcy: silnik, opony, paliwo

```stan dziala
Własny program silnikowy (PP-019) czeka na #164.
```

Rodzaj umowy decyduje, kiedy dostajesz nowości i ile płacisz (PP-030).

```pola
Fabryczna | nowości od razu | wspólny rozwój, zależność
Partnerska | 1 sezon opóźnienia | dostawca dodaje {SupplyEstimates.PartnerReliabilitySupport|pkt} niezawodności
Kliencka | 1 sezon opóźnienia | tanio, produkt dla wszystkich
Silnik z zeszłego roku | 2 sezony opóźnienia | najtańsza opcja awaryjna
```

```pola
Wyłączność | +{SupplyEstimates.ExclusiveMarkupMilli|m%} ceny
Dłuższa umowa | −{SupplyEstimates.YearsDiscountMilli|m%} za każdy kolejny sezon | najwyżej −{SupplyEstimates.YearsDiscountCapMilli|m%}
Postęp silnika | +{SupplyEstimates.ProgressPerSeason|pkt} na sezon
Klienci jednego dostawcy | najwyżej {SupplyEstimates.MaxCustomersPerSupplier}
```

```wgrze
Klient jest tak szybki jak zespół fabryczny | opóźnienie wersji, `PartnerReliabilitySupport`
Wybór dostawcy opon nic nie zmienia | `TyreGripSpread`, `TyrePartnerBonus`
```

```strojenie src/Paddock.Domain/Supply/SupplyEstimates.cs
ExclusiveMarkupMilli | Dopłata za wyłączność | m%
YearsDiscountMilli | Rabat za kolejny sezon | m%
YearsDiscountCapMilli | Największy rabat | m%
MaxCustomersPerSupplier | Klienci jednego dostawcy | 
PartnerReliabilitySupport | Niezawodność od partnera | pkt
EnginePowerWeight | Wpływ silnika na moc auta | 
EngineReliabilityWeight | Wpływ silnika na niezawodność auta | 
ProgressPerSeason | Postęp silnika na sezon | pkt
TyreGripSpread | Różnica przyczepności między dostawcami opon | 
TyrePartnerBonus | Przewaga opony partnera | 
```

---

## 11. Pieniądze i popularność

```stan toku
Kredyty (PP-048) jeszcze nie działają.
```

Każdy przychód i koszt to wpis w księdze, kwoty w dolarach z epoki (PP-050).

```porownanie Przychody | Koszty
Lata 50. | pieniądze startowe za każdy start, nagrody za miejsca | pensje 1. dnia miesiąca
Od 1968 | + sponsorzy | auto i rozwój co tydzień
Później | + TV, nagrody za konstruktorów | dostawy, wyjazdy na wyścigi
```

```pola FinanceEstimates
Saldo pod kreską | dozwolone | na powrót masz cały sezon, potem niewypłacalność
Popularność | {FinanceEstimates.MinPopularityMilli}–{FinanceEstimates.MaxPopularityMilli} (start {FinanceEstimates.BaselinePopularityMilli}) | od niej zależą pule pieniędzy (PP-025)
Wyrównany sezon | +{FinanceEstimates.CloseFightGain} popularności
Dominacja | −{FinanceEstimates.DominanceLoss} popularności
Finanse rywali | nieznane | widzisz tylko swoje i prognozę do końca sezonu
```

```wgrze
Pieniądze nie mają znaczenia | budżety epoki, `PrizeMoneyShare`
Mały zespół bankrutuje po kilku wyścigach | `StartMoneyShare`, `RaceRunningShare`
```

```strojenie src/Paddock.Domain/Finance/FinanceEstimates.cs
StartMoneyShare | Pieniądze startowe (część typowego budżetu) | %
PrizeMoneyShare | Nagrody (część typowego budżetu) | %
RaceRunningShare | Wyjazdy na wyścigi (część typowego budżetu) | %
PrizePositions | Miejsca z nagrodą | 
CloseFightGain | Wzrost popularności po wyrównanym sezonie | 
DominanceLoss | Spadek popularności po dominacji | 
MinPopularityMilli | Najniższa popularność (tysięczne) | 
MaxPopularityMilli | Najwyższa popularność (tysięczne) | 
```

---

## 12. Sponsorzy

```stan dziala
Sponsorzy są fikcyjni. Pakiet sponsora założycielskiego jest po MVP.
```

Trzy miejsca na zespół, na każde kilku kandydatów (PP-050).

```kroki
Otwarcie | sponsor proponuje {SponsorEstimates.OpeningTermsMilli|m%} pełnej ceny
Czekanie | +{SponsorEstimates.WaitingGainMilliPerDay|m%} dziennie, do limitu negocjatora
Ryzyko | {SponsorEstimates.RivalPresenceChance|%} szans, że rozmawia też rywal; podpisuje z szansą {SponsorEstimates.RivalSignChance|%} dziennie
Umowa | {SponsorEstimates.DealDays|dni}, raty co miesiąc, cel sponsora
```

```wykres sponsor-czekanie
Przerywana linia: ile średnio zdobędziesz, czekając tyle dni, gdy nie wiesz, czy rywal jest przy stole.
```

> **Uwaga:** przy obecnych liczbach czekanie **średnio się nie opłaca**. Opłaca się tylko, gdy dobry negocjator (`RivalInsightSkill`) widzi, że rywala nie ma. Jeśli dylemat ma być prawdziwy także bez tej wiedzy: mniejsze `RivalSignChance` albo większe `WaitingGainMilliPerDay`.

```pola
Cel spełniony | premia {SponsorEstimates.BonusMilli|m%} rocznej kwoty | +{SponsorEstimates.TrustOnMet} zaufania
Cel niespełniony | −{SponsorEstimates.TrustOnFailed} zaufania | może odejść
Przedłużenie | sponsor proponuje sam | od {SponsorEstimates.RenewalMinTrust} zaufania
Miejsce dodatkowe | {SponsorEstimates.SecondarySlotMilli|m%} kwoty głównego
```

```wgrze
Zawsze opłaca się czekać albo zawsze podpisać od razu | `RivalSignChance`, `WaitingGainMilliPerDay`
Sponsor odchodzi za szybko | `TrustOnFailed`, `RenewalMinTrust`
```

```strojenie src/Paddock.Domain/Sponsors/SponsorEstimates.cs
OpeningTermsMilli | Warunki na otwarcie | m%
WaitingGainMilliPerDay | Poprawa warunków dziennie | m%
BaseCapMilli | Limit poprawy bez umiejętności | m%
CapMilliPerSkill | Limit poprawy za punkt negocjatora | m%
RivalPresenceChance | Szansa, że rozmawia rywal | %
RivalSignChance | Szansa podpisu rywala dziennie | %
RivalInsightSkill | Negocjator widzi rywala od | 
DealDays | Długość umowy | dni
BonusMilli | Premia za cel | m%
TrustOnMet | Zaufanie za spełniony cel | 
TrustOnFailed | Utrata zaufania za niespełniony cel | 
RenewalMinTrust | Zaufanie potrzebne do przedłużenia | 
SecondarySlotMilli | Miejsce dodatkowe względem głównego | m%
```

---

## 13. Zarząd i zwolnienia

```stan dziala
Właściciele i zarządy nie mają atrybutów (PP-050).
```

Zarząd ocenia szefa po każdym wyścigu. Za długo pod progiem oznacza zwolnienie.

```pola BoardEstimates
Reputacja menedżera | start {BoardEstimates.InitialReputationTenths|t} ze 100 | wyniki ponad oczekiwania, tytuły; spada za długi i zwolnienie
Zaufanie zarządu | start {BoardEstimates.InitialConfidenceTenths|t} | po wyścigu nadrabia {BoardEstimates.ReviewSmoothing|%} różnicy do celu
Cierpliwość | {BoardEstimates.PatienceBase} + wiek zespołu w latach | starszy zespół jest cierpliwszy
Zwolnienie | {BoardEstimates.ReviewsToDismissBase}+ ocen z rzędu pod progiem | cierpliwy zarząd czeka dłużej
Ochrona nowego szefa | pierwszy pełny sezon | + {BoardEstimates.ProtectionDaysPerReputationPoint|dni} za punkt reputacji
Po zwolnieniu | oferty pracy | najpóźniej po {BoardEstimates.GuaranteeWindowDays|dni} zawsze jakaś
```

```wykres zarzad-prog
```

```wgrze
Jeden słaby sezon kończy pracę | `ReviewsToDismissBase`, próg zaufania
Trzy złe sezony uchodzą płazem | to samo, w drugą stronę
```

```strojenie src/Paddock.Domain/Board/BoardEstimates.cs
InitialReputationTenths | Reputacja nowego menedżera | t
InitialConfidenceTenths | Zaufanie zarządu na start | t
ReviewSmoothing | Nadrabianie różnicy do celu po wyścigu | %
DismissBelowAtPatience50Tenths | Próg zaufania przy średniej cierpliwości | t
ReviewsToDismissBase | Oceny pod progiem do zwolnienia | 
PatienceBase | Bazowa cierpliwość | 
ProtectionDaysPerReputationPoint | Ochrona za punkt reputacji | dni
SeasonObjectiveTenths | Zaufanie za cel sezonu | t
TitleTenths | Reputacja za tytuł | t
DismissalTenths | Utrata reputacji za zwolnienie | t
GuaranteeWindowDays | Najdłużej bez oferty pracy | dni
```

---

## 14. Weekend wyścigowy

```stan dziala
Silnik okrążeniowy z kalibracją #122. Mapa na żywo jest w prototypie, ciągła symulacja po MVP (PP-052).
```

Wynik wyścigu powstaje okrążenie po okrążeniu. Każda warstwa zależy od epoki.

### Czas okrążenia

```kroki
Baza toru | długość ÷ prędkość epoki × charakter toru
Auto | dopasowanie parametrów do fragmentów toru
Kierowca | tempo, regularność, opanowanie w deszczu
Paliwo i opony | ciężar baku, zużycie
Ruch | brudne powietrze, walka o pozycję
Szum | mniejszy u regularnych kierowców
```

```wykres predkosc-epoki
```

```wykres dopasowanie-toru
Dlatego to samo auto jest na jednym torze z przodu, a na innym w środku stawki (PP-049).
```

```wykres auto-kontra-kierowca
Ocena kierowcy w wyścigu to atrybut × 5, więc +10 punktów oceny to +2 punkty atrybutu.
```

### Wyprzedzanie

```pola WeekendConstants
Brudne powietrze | do {PaceConstants.DirtyAirMaxLossSeconds|s} straty na okrążeniu
Żeby wyprzedzić | przewaga tempa powyżej {WeekendConstants.OvertakeMarginSeconds|s} | margines rośnie z obroną broniącego, maleje z wyprzedzaniem atakującego
Bez wyprzedzenia | jedzie {WeekendConstants.FollowGapSeconds|s} za autem przed nim
```

### Opony i paliwo

```wykres opony
Miękka szybsza na starcie, klif przychodzi wcześniej. W latach 50. jedna twarda opona na cały wyścig.
```

```pola
Paliwo | {PaceConstants.FuelSecondsPerKg|s} straty za kilogram na okrążeniu
Płynny kierowca | wolniejsze zużycie opon | do {TyreFuelConstants.SmoothnessWearEffect|%}
Tankowanie | tylko w latach, w których było naprawdę
```

### Pogoda

```wykres deszcz
Skalibrowane na wyścigach 1950–2025 (#122).
```

```pola
Pogoda | zmienia się minuta po minucie | deszcz narasta i słabnie
Tor | moknie i schnie | linia wyścigowa {WeatherConstants.RacingLineDryingFactor}× szybciej
Prognoza w boksie | ma błąd | najlepsza osoba {WeatherConstants.GoodForecasterScale}×, najsłabsza {WeatherConstants.PoorForecasterScale}× błędu
```

### Awarie

```wykres słupki
tytuł: Odsetek startujących, którzy odpadli przez awarię (cel kalibracji)
format: %
max: 50
1950–59 | 43
1960–69 | 37
1970–79 | 33
1980–93 | 36
1994–2009 | 22
od 2010 | 8
opis: Gra dostraja ryzyko awarii tak, żeby trafić w te odsetki (#122).
```

```pola
Ryzyko awarii | epoka, niezawodność auta, przebieg części, tempo, upał, płynność kierowcy
Ostrzeżenie | {ReliabilityConstants.DefaultWarningLeadLaps} okrążenia wcześniej | auto zwalnia
Skutek | koniec wyścigu, utrata mocy albo naprawa | naprawa {ReliabilityConstants.RepairMinSeconds|s}–{ReliabilityConstants.RepairMaxSeconds|s}
```

### Incydenty

```wykres incydenty
```

```pola
Pierwsze okrążenie | {IncidentConstants.FirstLapFactor}× groźniejsze | drugie {IncidentConstants.SecondLapFactor}×
Mokry tor | do +{IncidentConstants.WetnessWeight|%} ryzyka
Auta w pobliżu | +{IncidentConstants.ContactDensityWeight|%} za każde
Neutralizacja | samochód bezpieczeństwa, VSC i czerwona flaga | tylko w epokach, w których istniały
Śmierć | tylko przy włączonej opcji | PP-006
```

### Postoje i strateg

```wykres mechanicy
```

```pola PitConstants
Błąd w boksie | +{PitConstants.ErrorMinSeconds|s}–{PitConstants.ErrorMaxSeconds|s} | np. zablokowana nakrętka
Tryb ataku | −{PitConstants.PushPaceGainSeconds|s} na okrążeniu | opony zużywają się {PitConstants.PushWearFactor}× szybciej
Strateg | plan postojów, opony, tempo | słaby gorzej ocenia zużycie opon i plany
Zmęczenie kierowcy | po {PitConstants.DriverFatigueOnsetLaps} okrążeniach
```

```wgrze
Wyprzedzania za dużo albo za mało | `OvertakeMarginSeconds`
Co wyścig kraksa na pierwszym okrążeniu | `FirstLapFactor`
Lata 50. bez awarii albo współczesność z awariami | kalibracja awaryjności (#122)
Wyścig z 1955 i 1988 wygląda tak samo | napisz, w czym: to bramka fazy 3
```

```strojenie src/Paddock.Simulation/Racing/Pace/PaceConstants.cs
DriverSensitivity | Wpływ tempa kierowcy (część okrążenia na punkt) | 
MaxAffinitySeconds | Największe ukryte powinowactwo do toru | s
FuelSecondsPerKg | Strata za kilogram paliwa | s
TrafficMaxLossSeconds | Strata tuż za innym autem | s
DirtyAirMaxLossSeconds | Strata w brudnym powietrzu | s
WetBaseFraction | Strata na mokrym, dobre opony | %
WetMismatchFraction | Dodatkowa strata na mokrym, złe opony | %
NoiseSigmaAtZeroConsistency | Rozrzut czasów bez regularności | s
NoiseSigmaAtMaxConsistency | Rozrzut czasów przy pełnej regularności | s
```

```strojenie src/Paddock.Simulation/Racing/Weekend/WeekendConstants.cs
OvertakeMarginSeconds | Przewaga potrzebna do wyprzedzenia | s
OvertakeSkillWeight | Wpływ wyprzedzania i obrony | 
SafetyCarLapFactor | Okrążenie za samochodem bezpieczeństwa | 
MinorIncidentLossSeconds | Strata za drobny incydent | s
StrategistCadenceLaps | Co ile okrążeń pytany jest strateg | 
```

```strojenie src/Paddock.Simulation/Racing/Tyres/TyreFuelConstants.cs
WearGrowthShare | Przyspieszanie zużycia przed klifem | 
CliffDropSeconds | Nagła strata na klifie | s
CliffSlopeMultiplier | Narastanie straty po klifie | 
WarmUpPeakLossSeconds | Strata na zimnym komplecie | s
SmoothnessWearEffect | Wpływ płynności na zużycie | %
SupplierGripSwingSeconds | Różnica między dostawcami opon | s
FuelSavingPaceLossSeconds | Strata za oszczędzanie paliwa | s
```

```strojenie src/Paddock.Simulation/Racing/Weather/WeatherConstants.cs
RaceRainProbabilityLow | Deszcz: tor suchy (skalibrowane) | %
RaceRainProbabilityMedium | Deszcz: tor umiarkowany | %
RaceRainProbabilityHigh | Deszcz: tor deszczowy (skalibrowane) | %
BaseOffLineDryingPerMinute | Schnięcie toru na minutę | 
RacingLineDryingFactor | Ile razy szybciej schnie linia | 
PoorForecasterScale | Błąd najsłabszej prognozy | 
GoodForecasterScale | Błąd najlepszej prognozy | 
```

```strojenie src/Paddock.Simulation/Racing/Reliability/ReliabilityConstants.cs
RatingEffect | Ile ryzyka usuwa niezawodność auta | %
AgeSlope | Wzrost ryzyka z przebiegiem | 
SympathyStressWeight | Ile psuje niepłynny kierowca | 
DefaultWarningLeadLaps | Ostrzeżenie przed awarią | 
RepairMinSeconds | Najkrótsza naprawa | s
RepairMaxSeconds | Najdłuższa naprawa | s
```

```strojenie src/Paddock.Simulation/Racing/Incidents/IncidentConstants.cs
BaseIncidentRatePerCarLap | Bazowa szansa incydentu na okrążenie | %
FirstLapFactor | Mnożnik pierwszego okrążenia | 
SecondLapFactor | Mnożnik drugiego okrążenia | 
WetnessWeight | Wpływ mokrego toru | 
ContactDensityWeight | Wpływ aut w pobliżu | 
SafetyCarRetirementLaps | Okrążenia za samochodem bezpieczeństwa | 
```

```strojenie src/Paddock.Simulation/Racing/Pits/PitConstants.cs
FaultProbabilityAtQuality0 | Błąd w boksie: najsłabsza ekipa | %
FaultProbabilityAtQuality100 | Błąd w boksie: najlepsza ekipa | %
ErrorMinSeconds | Najmniejsza strata przy błędzie | s
ErrorMaxSeconds | Największa strata przy błędzie | s
PushPaceGainSeconds | Zysk w trybie ataku | s
PushWearFactor | Zużycie opon w trybie ataku | 
PlanNoiseSdSecondsAtSkill0 | Pomyłka najsłabszego stratega w ocenie planu | s
WearBeliefSdAtSkill0 | Pomyłka najsłabszego stratega w zużyciu opon | 
DriverFatigueOnsetLaps | Okrążenia do zmęczenia kierowcy | 
```

---

## 15. Tory

```stan dziala
Czas okrążenia z krzywizny toru jest w toku (#138). Edytor torów: PR #146.
```

Tor to zamknięta pętla punktów w metrach. Symulacja i mapa używają tej samej krzywej (PP-048, PP-049).

```pola
Tory i układy | `data/authored/tracks/circuits.json` | lata, długość, charakter
Wyścig → układ | `data/authored/tracks/race_layout_map.json`
Kształt | `data/authored/tracks/geometry/<układ>.json` | punkty kontrolne, nazwy zakrętów
Brak pliku | zastępczy owal właściwej długości | nic się nie psuje
Dziś | tory kalendarza 1955 i Monza 1972 | dobre: Indianapolis, reszta do poprawki
```

### Poprawić tor

```kroki
Edytuj | edytor torów albo ręcznie w JSON
Waliduj | długość, brak przecięć i ostrych załamań, nazwy zakrętów
Przebuduj UI | `node ui/prototype/tools/build-track-geometry.mjs`
Obejrzyj | `ui/prototype/track-preview.html`, wszystkie tory naraz
```

### Dodać tor

```kroki
Układ | wpis w `circuits.json`: id, lata, długość, charakter
Wyścigi | przypisanie w `race_layout_map.json`
Kształt | punkty w edytorze, punkt 0 na linii mety
Zakręty | nazwy z epoki (Gazomètre w Monako 1950, nie Rascasse)
```

Dokładne komendy: TECH §6.5. Obrazów i śladów z zewnętrznych źródeł nie commitujemy (PP-041).

```wgrze
Sylwetka toru jest nierozpoznawalna | podaj tor i zakręt
```

---

## 16. Dane świata i edycja bazy

```stan dziala
Edytor bazy i własne scenariusze startowe (PP-051) przyjdą później.
```

Świat powstaje z prawdziwych wyników i z plików autorskich w `data/authored/`.

```pola
Jolpica-F1 | kierowcy, zespoły, tory, wyniki od 1950 | licencja niekomercyjna: pobierasz u siebie, nie trafia do repo (PP-041)
`regulations/` | przepisy rok po roku | 41 wymiarów, 1950–2026
`eras/` | budżety, model przychodów
`tracks/` | tory i geometria
`teams/` | silniki, ciągłość zespołów, założyciele
`people/` | personel techniczny w sezonach
`tech/` | technologie z najwcześniejszym realnym rokiem
`commercial/` | fikcyjni sponsorzy
`weather/` | klimat torów, pogoda prawdziwych wyścigów
`safety/` | bezpieczeństwo epok, wypadki
`names/` | imiona i nazwiska fikcyjnych ludzi
```

```kroki
Prośba | np. „w 1976 Tyrrell ma mocniejsze auto”
Zmiana | Claude zmienia plik (PP-051)
Walidacja | `validate-authored` pilnuje spójności
Zapis gry | pamięta wersję bazy, stara kariera nie zepsuje się po cichu
```

---

## 17. Inne zespoły (AI)

```stan czesciowo
Szefowie AI: #109. Upadki, wykupy, propozycje historyczne: #111.
```

AI gra na tych samych zasadach, nie widzi ukrytych atrybutów i nie zna przyszłości (D-010). Każda ważna decyzja ma zapisane uzasadnienie (Paddock Spy).

```porownanie Działa | Zaprojektowane
Inżynierowie | wybierają projekty jak u Ciebie | 
Kierowcy i personel | oceniają oferty wszystkich zespołów | 
Sponsorzy | rywal może podpisać przed Tobą | 
Zarządy | zwalniają szefów AI, nowy ma inny archetyp | 
Dostawcy | odpowiadają na propozycje | 
Szefowie AI | | Pretendent, Budowniczy, Oportunista, Przetrwanie
Historia | | prawdziwe zdarzenia jako propozycje z warunkami
Zespoły | | powstają, upadają, są wykupowane
```

```wgrze
Rywale zachowują się bezmyślnie albo wszechwiedząco | opisz konkretną sytuację
```

---

## 18. Prawda i wiedza

```stan dziala
```

Gra liczy prawdę, a pokazuje tylko to, co wie Twój zespół (INV-003).

```porownanie Widzisz | Nie widzisz
Kierowcy | swoich dokładnie po czasie, cudzych w pasmach | ukrytego potencjału
Auto | przedziały zysku i sufitu | prawdziwego sufitu
Rywale | wyniki, czasy, klasyfikacje | finansów, umów, wektora auta
Pogoda | prognozę z błędem | przyszłej pogody
```

Tryb bez liczb (PP-031) pokazuje to samo słowami Twoich ludzi: „młody jest szybki, ale zjada opony”.

```wgrze
Gdzieś widać dokładną ukrytą wartość | to błąd, zgłoś ekran
```

---

## 19. Czego jeszcze nie ma

```pola
Pętla kariery | finanse, sponsorzy, auta, zarząd | #172
Grywalny sezon | w konsoli | #112, bramka #113
Szefowie AI | #109 | cykl życia zespołów #111
Tor z geometrii | czas okrążenia z krzywizny | #138
Własne silniki | #164
Później | przełomy (PP-042), kronika, Hall of Fame, Paddock Monthly, UI (faza 6), multiplayer (PP-045)
```
