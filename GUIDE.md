# Jak działa gra

**Rola:** przewodnik po wszystkich systemach gry, pisany dla gracza i właściciela, a nie dla programisty. Mówi, jak coś działa, o czym decydujesz, co powinieneś czuć w trakcie gry i które liczby można zmienić, jeśli coś nie gra. Decyzje i ich uzasadnienia są w [VISION.md](VISION.md), pełny projekt systemów w [DESIGN.md](DESIGN.md), technika w [TECH.md](TECH.md).

Ten plik jest źródłem strony „Jak działa gra” w dokumentacji HTML (PP-054). Tabele „Liczba do strojenia” i wykresy nie są przepisane ręcznie: generator czyta je za każdym razem z kodu gry, więc zawsze pokazują to, co gra robi dzisiaj.

---

## 0. Jak czytać ten przewodnik

Każdy rozdział ma te same części:

- **Jak to działa:** mechanika po ludzku, bez kodu.
- **Co decydujesz:** gdzie masz wpływ.
- **W grze:** co powinieneś zauważyć podczas grania. Jeśli czujesz coś innego, to sygnał, że liczba jest źle ustawiona.
- **Liczby do strojenia:** aktualne wartości z kodu. Każdy wiersz ma nazwę i link do miejsca w kodzie.
- **Stan:** czy to już działa, czy jest zaprojektowane, czy przyjdzie później.

**Wszystkie liczby to szacunki** (PP-047, VISION: „nie piszemy liczb na oko jako faktów”), chyba że wiersz mówi inaczej. Część jest skalibrowana na prawdziwych danych (np. awaryjność i deszcz, #122), ale nadal można je zmieniać.

### Jak zgłaszać uwagi

Wystarczy napisać, co czujesz w grze, np. „sponsorzy podpisują z rywalami za szybko” albo „juniorzy rozwijają się za wolno”. Jeśli chcesz być precyzyjny, podaj nazwę z tabeli i nową wartość, np. „`RivalSignChance` z 2% na 1%”. Zmiana liczby to zwykle jedna linijka w kodzie, a strona przeliczy się sama przy następnym budowaniu.

### Stany

| Stan | Znaczenie |
|---|---|
| Działa w kodzie | system jest napisany i przetestowany; da się go uruchomić w konsoli (SimRunner) |
| Podłączanie do kariery | system działa sam, ale pętla kariery jeszcze go nie woła (PR [#172](https://github.com/DatJikun/paddock-principal/pull/172)) |
| Zaprojektowane | opisane w DESIGN, kodu jeszcze nie ma |
| Później | po MVP (faza 7+) |

---

## 1. Start kariery i ustawienia świata

### Jak to działa

Karierę zaczynasz od wyboru roku i zestawu ustawień. Ustawienia to kilka niezależnych osi (PP-046), a presety tylko je składają:

| Oś | Możliwości |
|---|---|
| Ludzie | prawdziwa trajektoria · prawdziwy potencjał · prawdziwe nazwiska z losowymi umiejętnościami · w pełni generowani |
| Przepisy | historyczne · głosowane co sezon przez zespoły |
| Zachowanie AI | odtwarza historię · reaguje na sytuację · czysta losowość |
| Suwaki | siła historii (0–100), losowość (0–100), śmiertelność (domyślnie wyłączona) |

- **Trajektoria:** prawdziwy kierowca jest tak dobry, jak był naprawdę w danym roku. Świat zmienia to, *gdzie* jeździ, a nie *jak dobry* jest.
- **Prawdziwy potencjał:** prawdziwa kariera wyznacza sufit talentu, ale to, czy kierowca go osiągnie, zależy od świata gry.
- **Siła historii:** jak chętnie AI wybiera to, co zdarzyło się naprawdę, jeśli w tym świecie ma to sens. Przy 100% świat bez Twojego udziału idzie torem historii.

Presety: *Najbardziej historyczny* (trajektoria, historyczne przepisy, AI odtwarza historię), *Zbalansowany* (domyślny: prawdziwy potencjał, historyczne przepisy, AI reaguje) i *Chaos*.

MVP to sezon 1955 w przejętym istniejącym zespole (PP-050). Każdy zespół ma dwa auta i dwóch stałych kierowców.

### Co decydujesz

Rok startu, zespół, preset albo ręcznie każdą oś. Ustawień nie zmienisz w trakcie kariery.

> **W grze:** przy „Zbalansowanym” czołówka 1955 powinna wyglądać znajomo (Mercedes mocny, Fangio z przodu), ale nie identycznie. Jeśli po kilku sezonach świat wygląda losowo, zgłoś to: to sprawdzian wierności historii (PP-012).

> **Stan:** działa w kodzie (konfiguracja kariery, inicjalizacja świata, głosowane przepisy). Ekran tworzenia kariery jest w fazie UI.

---

## 2. Czas, skrzynka i decyzje

### Jak to działa

Czas płynie dzień po dniu (PP-016). Przycisk „Dalej” przewija dni, aż pojawi się coś, co wymaga Twojej uwagi. Dni bez wydarzeń mijają prawie natychmiast.

Sprawy przychodzą do skrzynki tylko wtedy, gdy mają treść. Są dwa rodzaje:

- **Wiadomość:** informacja, nic nie blokuje.
- **Decyzja:** ma opcje i zatrzymuje czas, dopóki nie odpowiesz. Niektóre decyzje mają termin i opcję domyślną. Jeśli nie odpowiesz, po terminie gra wybierze ją za Ciebie.

Ważne decyzje zawsze działają tak samo: wybierasz opcję, widzisz jej skutek, potem klikasz **Potwierdź**.

### Przykłady decyzji z terminem

| Decyzja | Domyślnie po terminie |
|---|---|
| Koncepcja auta gotowa: zatwierdzić produkcję czy rozwijać dalej? | rozwijamy dalej |
| Inżynier prosi o czas po zmianie podziału zasobów | trzymamy plan |
| Oferta pracy po zwolnieniu | odrzucona |
| Kontroferta w negocjacjach | wygasa |

```strojenie src/Paddock.Domain/Development/DevelopmentEstimates.cs
ConceptDecisionDays | Ile dni czeka decyzja „zatwierdzić koncepcję czy rozwijać dalej” | dni
ReplyDays | Ile dni czeka odpowiedź inżyniera po zmianie podziału zasobów | dni
```

> **W grze:** skrzynka nie powinna być spamem. Jeśli co drugi dzień zatrzymuje Cię coś nieważnego, zgłoś, co to było.

> **Stan:** działa w kodzie (zegar, kolejka zdarzeń, skrzynka v0, decyzje z terminem).

---

## 3. Kierowcy: atrybuty, gwiazdki, cechy

### Jak to działa

Kierowca ma 11 atrybutów w skali 1–20: zakręty, hamowanie, płynność, wyprzedzanie, obrona, regularność, opanowanie, adaptacja, deszcz, kondycja i informacja zwrotna. Pełny opis każdego jest w DESIGN §6.1.

**Gwiazdki** (0–5, z połówkami) to średnia atrybutów podzielona przez 4: 20/20 to 5★, a 10/20 to 2,5★ (PP-047). Nie ma progów rankingowych.

**Ocena ogólna** (1–100, używana wewnętrznie) to średnia ważona atrybutów, a wagi zależą od epoki. W latach 50. bardziej liczą się płynność i kondycja, bo samochody się psuły, a wyścigi trwały po trzy godziny. Dziś bardziej liczą się regularność, opanowanie i informacja zwrotna.

```wykres wagi-oceny
Ten sam kierowca ma inną ocenę ogólną w różnych epokach, bo inne cechy decydują o wyniku. Płynność to ta sama wartość przez całą karierę, ale jej waga spada z czasem.
```

Doświadczenie nie jest atrybutem, tylko licznikami: starty, okrążenia na danym torze, wyścigi w deszczu, sezony w zespole. Cechy (np. mistrz deszczu, niszczyciel opon) dają konkretne plusy i minusy i można je nabyć albo stracić w trakcie kariery.

### Co decydujesz

Kogo zatrudniasz i obok kogo go stawiasz. Cudzych kierowców widzisz w pasmach (np. 12–16), które zawężają się z obserwacją (rozdział 6).

```strojenie src/Paddock.Domain/People/GenerationEstimates.cs
EarlyEraLastYear | Ostatni rok „wczesnej” epoki wag oceny | 
ModernEraFirstYear | Pierwszy rok „nowoczesnej” epoki wag oceny | 
EarlySmoothnessMeanBonus | Generowani kierowcy wczesnej epoki: premia do średniej płynności | pkt
EarlyFitnessMeanBonus | Generowani kierowcy wczesnej epoki: premia do średniej kondycji | pkt
ModernFeedbackMeanBonus | Generowani kierowcy nowoczesnej epoki: premia do informacji zwrotnej | pkt
```

> **W grze:** gwiazdki powinny zgadzać się z Twoją intuicją o kierowcy. Jeśli 3★ z lat 50. jeździ jak 4★, zgłoś to: winne mogą być wagi epoki albo skala ocen (rozdział 5).

> **Stan:** działa w kodzie (atrybuty, gwiazdki, ocena ogólna, generator). Cechy są zaprojektowane, ale jeszcze nie działają w wyścigu.

---

## 4. Wiek: rozwój, szczyt, spadek, emerytura

### Jak to działa

Każdy kierowca przechodzi ten sam łuk kariery (PP-047): rośnie, stabilizuje się, osiąga szczyt, a słabnie dopiero około 36–40 lat. Dane decydują tylko o tym, **jak wysoko** i **kiedy** kierowca dochodzi do szczytu.

- **Rozwój** zaczyna się kilka lat przed debiutem w F1 (serie juniorskie).
- **Szczyt** przypada na wiek, w którym kierowca miał najlepszy sezon, ale gra przycina go do typowego przedziału. Późny debiutant ma szczyt od razu w roku debiutu.
- **Spadek** zaczyna się w stałym wieku z przedziału 36–40. Każdy kierowca ma swój wiek, zawsze ten sam.
- **Kierowca, który odszedł na szczycie** (jak Fangio), słabnie dopiero po swoim ostatnim prawdziwym sezonie, a nie w typowym wieku.

```wykres luk-kariery
Kształt schematyczny: wysokość i moment szczytu biorą się z danych. Słabszy sezon pod koniec prawdziwej kariery to często słabsze auto albo mocniejszy partner, a nie wiek, dlatego gra nie każe kierowcy słabnąć wcześniej.
```

```strojenie tools/Paddock.DataPipeline/Ratings/RatingsCareerArc.cs
YearsOfGrowthBeforeDebut | Ile lat przed debiutem w F1 zaczyna się rozwój | lat
MinPeakAge | Najwcześniejszy wiek szczytu | lat
MaxPeakAge | Najpóźniejszy wiek szczytu (późni debiutanci mają szczyt w roku debiutu) | lat
DeclineStartMin | Najwcześniejszy początek spadku | lat
DeclineStartMax | Najpóźniejszy początek spadku | lat
DeclineLevelsPerYear | Ile poziomu (skala 1–20) kierowca traci co rok spadku | pkt
AtPeakTolerance | Ostatni sezon tyle poniżej szczytu to jeszcze „odszedł na szczycie” | pkt
```

### Emerytura

Prawdziwy kierowca ze znanym ostatnim sezonem kończy karierę 31 grudnia tego sezonu, chyba że w Twoim świecie potoczyło się inaczej. Pozostali (fikcyjni i prawdziwi bez danych) odchodzą według krzywej wieku: od pewnego wieku przy każdych urodzinach rośnie szansa odejścia, a w ostatnim wieku odchodzą wszyscy.

```wykres emerytura
Kierowcy bez zaplanowanego końca kariery. Personel ma tę samą zasadę, tylko później.
```

```strojenie src/Paddock.Domain/Career/CareerDayEstimates.cs
DriverRetirementFromAge | Od tego wieku kierowca może odejść przy urodzinach | lat
DriverRetirementCertainAge | W tym wieku kierowca odchodzi na pewno | lat
StaffRetirementFromAge | Od tego wieku personel może odejść | lat
StaffRetirementCertainAge | W tym wieku personel odchodzi na pewno | lat
```

### Fikcyjni kierowcy

Generowani kierowcy dostają losowy łuk z tych samych zasad: wiek startu rozwoju, liczbę lat do szczytu, długość plateau i tempo spadku.

```strojenie src/Paddock.Domain/People/GenerationEstimates.cs
DriverGrowthStartMin | Fikcyjny kierowca: najwcześniejszy start rozwoju | lat
DriverYearsToPeakMin | Fikcyjny kierowca: najmniej lat do szczytu | lat
DriverYearsToPeakMaxExclusive | Fikcyjny kierowca: lat do szczytu (górna granica, bez niej) | lat
DriverPlateauMaxExclusive | Fikcyjny kierowca: najdłuższe plateau (górna granica, bez niej) | lat
DriverDeclineMilliMin | Fikcyjny kierowca: najwolniejszy spadek (tysięczne oceny ogólnej na rok) | 
DriverDeclineMilliMaxExclusive | Fikcyjny kierowca: najszybszy spadek (górna granica, bez niej) | 
```

> **W grze:** 40-latek nie powinien wygrywać seryjnie, a 23-latek z talentem powinien wyraźnie rosnąć co sezon. Jeśli weterani trzymają poziom zbyt długo, zgłoś początek albo tempo spadku.

> **Stan:** łuk prawdziwych kierowców jest wyliczany w pipeline ocen. Emerytura działa w kodzie. Rozwój i spadek atrybutów w trakcie kariery (poza pulą talentów) jest zaprojektowany, ale jeszcze nie działa sezon po sezonie.

---

## 5. Skąd się biorą oceny prawdziwych kierowców

### Jak to działa

Nikt nie wpisuje ocen ręcznie (PP-011). Gra wylicza je z prawdziwych wyników od 1950:

1. **Porównanie z partnerem z zespołu.** Ten sam samochód, więc różnica w wynikach mówi o kierowcy, a nie o aucie. Liczą się wyścigi i kwalifikacje, a awarie niezawinione przez kierowcę są pomijane.
2. **Sieć partnerów.** Kierowcy zmieniają zespoły, więc porównania łączą się w sieć przez całą historię.
3. **Ocena względem własnej epoki.** Kierowcę porównuje się ze stawką jego czasów, a nie z kierowcami innych dekad. Najlepsi każdej epoki są blisko szczytu skali.
4. **Ostrożność przy małej liczbie danych.** Kierowca z niewieloma pojedynkami jest przyciągany do średniej, bo jego wynik może być przypadkiem.

```wykres przyciaganie
Kierowca z 40 pojedynkami zachowuje tylko część swojej przewagi nad stawką, a kierowca z pełną karierą prawie całą. Właśnie to obniżyło zbyt wysoką ocenę Castellottiego.
```

```wykres skala-ocen
Przeciętny kierowca F1 swojej epoki ma poziom 12 (3★). Powyżej średniej krzywa łagodnie zbliża się do 20 i nigdy jej nie przekracza, więc wielcy kierowcy różnią się między sobą, a nie stoją wszyscy na suficie.
```

```strojenie tools/Paddock.DataPipeline/Ratings/RatingsMapping.cs
LevelAtFieldMean | Poziom (1–20) przeciętnego kierowcy F1 swojej epoki | 
LevelsPerSd | Poziomy na jedno odchylenie poniżej średniej | 
SaturationSd | Jak szybko krzywa powyżej średniej dochodzi do 20 (mniej = szybciej) | 
```

```strojenie tools/Paddock.DataPipeline/Ratings/RatingsEraScale.cs
ShrinkHalfDuels | Liczba pojedynków, przy której kierowca zachowuje połowę przewagi | 
WindowYears | Ile sąsiednich sezonów wchodzi do porównania ze stawką epoki | lat
MinFieldSize | Najmniejsza stawka do porównania (mniejsza bierze więcej sezonów) | 
```

Ręczne korekty są dozwolone, ale tylko jako jawny plik nadpisań z uzasadnieniem (PP-011). Raport z przeliczenia na pełnych danych jest w issue [#131](https://github.com/DatJikun/paddock-principal/issues/131).

> **W grze:** sprawdzaj kierowców, których znasz. Jeśli ktoś jest wyraźnie za wysoko albo za nisko, podaj nazwisko: to najcenniejsza uwaga dla tego modelu.

> **Stan:** działa w pipeline danych (oceny v1). Kalibracja trwa.

---

## 6. Pula talentów, juniorzy i skauci

### Jak to działa

Gra nie symuluje kartingu ani F3. Ludzie pojawiają się w **puli talentów**, czyli abstrakcyjnym świecie poza F1 (PP-018).

- **Prawdziwy kierowca** trafia do puli 2–3 lata przed swoim prawdziwym debiutem, nie wcześniej niż w wieku 17 lat.
- **Fikcyjni kierowcy** uzupełniają pulę co sezon, żeby nie była listą przyszłych mistrzów. AI nie wie, kto jest prawdziwy.
- **Rozwój w puli:** co sezon junior zamyka część dystansu do swojego ukrytego potencjału, z losowym szczęściem i limitem punktów na atrybut.
- **Odpadnięcie:** kto zbyt długo siedzi w puli bez kontraktu albo jest za stary, odpada. To zdarzenie trafia do kroniki („kariera, która się nie wydarzyła”).

```wykres pula-rozwoj
Średnie szczęście. Opłacony program juniorski przyspiesza rozwój, ale nie podnosi sufitu: potencjał jest ten sam.
```

**Skauci** obserwują całą pulę (wolno) albo jedną osobę (szybko). Pasmo atrybutu zawęża się z czasem, a sieć kontaktów skauta przyspiesza obserwację. Słaby skaut daje szersze pasma i może się pomylić. Samo obserwowanie nigdy nie daje dokładnej wartości.

```wykres pasmo-skauta
Pasmo potencjału jest jeszcze szersze niż pasmo bieżącego atrybutu, bo potencjał trudniej ocenić.
```

### Co decydujesz

Kogo obserwujesz, komu opłacasz sezon juniorski (tani i wolny albo drogi i szybki) i kogo podpisujesz.

```strojenie src/Paddock.Data/Historical/PeopleSchedule.cs
DefaultPoolLeadYears | Ile lat przed prawdziwym debiutem kierowca wchodzi do puli | lat
PoolMinimumAgeYears | Najmłodszy wiek wejścia do puli | lat
```

```strojenie src/Paddock.Domain/Pool/PoolEstimates.cs
TargetSize | Do ilu osób pula jest uzupełniana co sezon | osób
FillerAgeMin | Najmłodszy fikcyjny junior | lat
MaxSeasonsInPool | Po ilu sezonach bez kontraktu junior odpada | sezonów
MaxAge | Po przekroczeniu tego wieku junior odpada | lat
DevelopmentRatePercent | Jaką część dystansu do potencjału junior zamyka w sezonie | %%
MaxAnnualStep | Najwięcej punktów na atrybut w jednym sezonie | pkt
LuckMinPercent | Najgorsze szczęście sezonu (część tempa) | %%
LuckMaxPercent | Najlepsze szczęście sezonu | %%
CheapSlowSpeedPercent | Tempo rozwoju w tanim programie | %%
ExpensiveFastSpeedPercent | Tempo rozwoju w drogim programie | %%
StartHalfWidth | Połowa szerokości pasma na starcie obserwacji | pkt
MinHalfWidth | Najwęższa połowa pasma, nawet po latach obserwacji | pkt
PersonFocusMilliPerMonth | Punkty obserwacji na miesiąc przy skupieniu na jednej osobie (tysięczne) | 
PoolFocusMilliPerMonth | Punkty obserwacji na miesiąc przy obserwacji całej puli (tysięczne) | 
SharedRaceMilli | Punkty obserwacji za każdy wspólny wyścig (tysięczne) | 
WorstScoutBias | O ile najgorszy skaut może się mylić | pkt
```

> **W grze:** dobry junior z programem powinien być gotowy na F1 po 2–3 sezonach. Jeśli pula jest pełna „pewniaków” albo same „wypełniacze”, zgłoś to.

> **Stan:** działa w kodzie (pula, rozwój, programy juniorskie, skauting). Podłączanie do kariery: [#172](https://github.com/DatJikun/paddock-principal/pull/172).

---

## 7. Kontrakty i negocjacje

### Jak to działa

Nie ma „wartości rynkowej” (PP-035). Kierowca albo członek personelu ocenia ofertę jak osoba: liczy, ile jest dla niego warta, według swojej osobowości. Najważniejsze składniki:

- **prestiż zespołu,**
- **przewidywane auto** (ile punktów, zwycięstw i szans na tytuł się spodziewa),
- **pensja** względem typowej pensji kierowcy o jego poziomie w danej epoce,
- **status** (#1, równy, #2, rezerwowy),
- **ryzyko** (odejmowane): długość umowy, kto ma opcję, klauzula wyjścia.

Lojalny kierowca dodaje coś obecnemu pracodawcy. Starszy kierowca coraz poważniej rozważa emeryturę. Jeśli żadna oferta nie przekracza jego minimum, woli poczekać na rynku.

**Negocjacje:** każda osoba daje kilka rund (więcej, jeśli jest profesjonalna, mniej, jeśli porywcza). Drobne podbijanie pensji o grosze irytuje i obniża zainteresowanie. Liczy się realna zmiana: status, klauzula, lata. Odpowiedź przychodzi po kilku dniach. Jeśli kierowca ma kilka akceptowalnych ofert, czeka do najwcześniejszego terminu, a przy remisie wygrywa zaufanie.

### Co decydujesz

Pensję, długość, status, opcje i klauzule. Termin, do którego czekasz na odpowiedź. Kiedy odejść od stołu.

```strojenie src/Paddock.Domain/Contracts/NegotiationEstimates.cs
WeightPrestige | Waga prestiżu zespołu w ocenie oferty | 
WeightCar | Waga przewidywanego auta | 
WeightSalary | Waga pensji | 
WeightStatus | Waga statusu w zespole | 
WeightRisk | Waga ryzyka (odejmowana) | 
StatusScoreNumberTwo | Ile wart jest status #2 (status #1 to 1) | 
ReservationUtility | Minimum, poniżej którego osoba woli czekać na rynku | 
AcceptanceMargin | O ile oferta musi przebić najlepszą alternatywę | 
MinRounds | Najmniej rund negocjacji | rund
MaxRounds | Najwięcej rund negocjacji | rund
NudgePenalty | Utrata zainteresowania za „kosmetyczną” poprawkę oferty (tysięczne) | 
MinMeaningfulImprovementPercent | Najmniejsza podwyżka, która liczy się jako realna zmiana | %%
ResponseDelayMinDays | Najkrótszy czas na odpowiedź | dni
ResponseDelayJitterDays | Losowe dodatkowe dni odpowiedzi (do) | dni
NegotiationWindowDays | Osobę z kontraktem można zaczepić dopiero tyle dni przed końcem umowy | dni
RenewalPromptDays | Przypomnienie o przedłużeniu tyle dni przed końcem | dni
MaxYears | Najdłuższy kontrakt | sezonów
TerminationShare | Jaką część reszty pensji płacisz przy zerwaniu umowy | %
ParallelBase | Ile negocjacji naraz bez wprawnego szefa | 
```

> **W grze:** gwiazdor powinien odrzucić zespół z końca stawki, chyba że zapłacisz bardzo dużo albo dasz status #1. Jeśli wszyscy podpisują wszystko, podnieś `ReservationUtility`. Jeśli negocjacje są frustrujące, sprawdź `NudgePenalty` i liczbę rund.

> **Stan:** działa w kodzie (kontrakty v1, silnik negocjacji). Podłączanie do kariery: [#172](https://github.com/DatJikun/paddock-principal/pull/172). Osobowość jest jeszcze wyliczana z ziarna, a nie z bazy ludzi.

---

## 8. Auto: koncepcja i dopasowanie do kierowcy

### Jak to działa

Auto ma wektor osiągów: moc, docisk (ograniczony epoką), przyczepność mechaniczna, hamowanie i niezawodność. Koncepcja to sześć osi, a każda strona ma swoją cenę (DESIGN §5.2): aero, filozofia, okno pracy, chłodzenie, opony, integracja silnika.

Najważniejsza oś to **filozofia: ewolucja czy rewolucja.**

```wykres koncepcja
Ewolucja ma niższy, przewidywalny sufit i startuje blisko niego. Rewolucja ma wysoki sufit z dużym rozrzutem, ale nowe auto zaczyna daleko od niego i trzeba je rozwijać w sezonie. Nikt nie zna sufitu dokładnie, nawet dyrektor techniczny.
```

**Dopasowanie kierowcy:** każdy kierowca ma preferencje (balans, trakcja, styl hamowania). Auto, które mu nie leży, kosztuje tempo i pewność siebie. Mistrz może być przeciętny w aucie zbudowanym pod partnera.

**Zrozumienie auta:** nowe auto albo nowa część nie daje pełnych osiągów od razu. Zespół musi je zrozumieć (testy, kilometry, praca inżynierów).

### Co decydujesz

Kierunek koncepcji i to, pod którego kierowcę budujesz auto.

```strojenie src/Paddock.Domain/Cars/CarNumbers.cs
EvolutionCeilingMean | Średni sufit koncepcji ewolucyjnej (skala 0–100) | 
RevolutionCeilingMean | Średni sufit koncepcji rewolucyjnej | 
EvolutionCeilingSd | Rozrzut sufitu ewolucji | 
RevolutionCeilingSd | Rozrzut sufitu rewolucji | 
EvolutionStartFraction | Jaką część sufitu ma auto ewolucyjne na start | %
RevolutionStartFraction | Jaką część sufitu ma auto rewolucyjne na start | %
ExecutionFloor | Najsłabszy personel i tak dowozi tę część sufitu | %
NewConceptUnderstanding | Zrozumienie nowej koncepcji w dniu zatwierdzenia (na 100) | 
PaceSecondsPerMismatch | Strata na okrążeniu za każdą jednostkę niedopasowania do kierowcy | s
ConfidencePerMismatch | Utrata pewności siebie za jednostkę niedopasowania | 
```

> **W grze:** rewolucja powinna być zakładem: czasem wielki sukces, czasem stracony sezon. Jeśli zawsze się opłaca, sufit albo start są źle ustawione.

> **Stan:** działa w kodzie (model auta, koncepcja, dopasowanie). Mapa osi koncepcji na osiągi to propozycja do oceny.

---

## 9. Rozwój auta (ścieżka A: inżynierowie wybierają sami)

### Jak to działa

Nie wybierasz części (PP-043). Ustalasz **podział zasobów**:

- **bieżące auto:** poprawki na ten sezon,
- **konto rozwoju:** badania odkładane na później,
- **przyszły rok:** nowa koncepcja.

Do tego priorytety obszarów (aerodynamika, podwozie, niezawodność, opony). Konkretne projekty wybierają **inżynierowie**, tak jak w zespołach AI, według swoich umiejętności, doświadczenia, stażu w zespole i Twoich priorytetów. Każdy ich wybór ma zapisane uzasadnienie.

Zasady:

- **Zysk zależy od zapasu.** Projekt zamyka część dystansu między obecnym poziomem a sufitem koncepcji. Blisko sufitu każda dziesiątka sekundy kosztuje coraz więcej.
- **Ludzie decydują o czasie, nie o jakości.** Więcej inżynierów kończy projekt szybciej, ale nie robi z przeciętnej koncepcji genialnej.
- **Ryzyko:** projekt może się nie udać. Koncepcja jest bardziej ryzykowna niż poprawka.
- **Konto rozwoju** traci wartość z czasem, bo rywale idą do przodu, a zmiana przepisów zabiera jego część.

```wykres konto-rozwoju
Odkładanie wiedzy ma sens przed ważną zmianą albo na przyszłe auto, ale trzymanie jej zbyt długo kosztuje.
```

**Nowa koncepcja: zatwierdzić czy czekać.** Gdy koncepcja jest gotowa, dostajesz decyzję: zatwierdzić produkcję teraz albo rozwijać dalej. Widzisz przedział dalszego zysku, dni do wyścigu, czas i koszt produkcji. Po zatwierdzeniu stare auto jedzie dalej, a nowe wchodzi pierwszego dnia po końcu produkcji (nigdy w środku weekendu). Produkcji nie da się anulować.

```wykres czas-produkcji
Produkcja trwa dłużej w nowszych epokach, bo auta są bardziej złożone. Większy zespół skraca ten czas.
```

**Odpowiedź inżyniera:** gdy zmienisz podział, a projekt jest blisko końca, inżynier może poprosić o czas („jeszcze 2 tygodnie”). Decydujesz: trzymać plan albo ciąć projekt (zostaje to, co zrobiono do tej pory).

### Co decydujesz

Podział zasobów, priorytety obszarów, moment zatwierdzenia nowej koncepcji i reakcję na prośby inżynierów.

```strojenie src/Paddock.Domain/Development/DevelopmentEstimates.cs
DefaultCurrentPercent | Podział domyślny: bieżące auto | %%
DefaultAccountPercent | Podział domyślny: konto rozwoju | %%
DefaultNextYearPercent | Podział domyślny: przyszły rok | %%
UpgradeBaseDays | Czas poprawki przy typowym zespole epoki | dni
ResearchBaseDays | Czas badań na konto | dni
ConceptBaseDays | Czas projektu nowej koncepcji | dni
MaxShare | Najwięcej zapasu, jaki jeden projekt może zamknąć | %
BaseRisk | Szansa porażki poprawki, zanim liczą się umiejętności | %
ConceptRiskMultiple | Ile razy ryzykowniejsza jest koncepcja | 
AccountDailyDecay | Dzienna utrata wartości konta | %
RuleChangeLossScale | Jak mocno zmiana przepisów obcina konto | 
ConceptProductionBaseDays | Czas produkcji koncepcji w 1950 | dni
ConceptProductionCostShare | Koszt produkcji jako część kosztu rozwoju koncepcji | %
CloseProgress | Od tego postępu inżynier prosi „dajcie nam jeszcze czas” | %
AdaptationYears | Po ilu latach inżynier jest w pełni wdrożony w zespole | lat
HeadcountPerSlot | Jeden równoległy projekt na tylu inżynierów | osób
MaxSlots | Najwięcej równoległych projektów | 
```

> **W grze:** różnica między zespołem, który trafił koncepcję, a resztą powinna rosnąć w sezonie, a stawka ściskać się w dojrzałych przepisach. Jeśli wszyscy rozwijają się w tym samym tempie, zgłoś to.

> **Stan:** działa w kodzie (T42, T42b, T42c). Podłączanie do kariery: [#172](https://github.com/DatJikun/paddock-principal/pull/172). Ekran jest w prototypie (`ui/prototype`, „Auto i rozwój”). Ręczny wybór projektów (ścieżka B) i przełomy technologiczne od ludzi (PP-042) przyjdą później.

---

## 10. Silniki, opony i paliwo od dostawców

### Jak to działa

Silnik, opony i paliwo kupujesz w umowach (PP-030). Rodzaj umowy decyduje, kiedy dostajesz nowości i ile płacisz:

| Rodzaj | Nowości | Uwagi |
|---|---|---|
| Fabryczna | od razu | wspólny rozwój, zależność |
| Partnerska | z opóźnieniem jednego sezonu | dostawca wspiera niezawodność |
| Kliencka | z opóźnieniem jednego sezonu | tanio, produkt „dla wszystkich” |
| Silnik z zeszłego roku | z opóźnieniem dwóch sezonów | najtańsza opcja awaryjna |

Dostawca silników ma swoją moc, niezawodność i wydajność, które rosną co sezon. Dostawca opon ma charakter: szybsze opony zużywają się szybciej. Opona dostrojona pod zespół partnerski daje mu przewagę.

Umowy startowe powstają z danych historycznych (kto komu dostarczał silniki w danym roku).

### Co decydujesz

Z kim podpisujesz, na ile lat i na jakich warunkach (wyłączność kosztuje więcej, dłuższa umowa daje rabat).

```strojenie src/Paddock.Domain/Supply/SupplyEstimates.cs
ExclusiveMarkupMilli | Dopłata za wyłączność | m%
YearsDiscountMilli | Rabat za każdy sezon po pierwszym | m%
YearsDiscountCapMilli | Największy rabat za długą umowę | m%
MaxCustomersPerSupplier | Ilu klientów obsługuje jeden dostawca | 
PartnerReliabilitySupport | Ile niezawodności dodaje wsparcie partnera | pkt
EnginePowerWeight | Jak mocno ocena silnika przesuwa moc auta | 
EngineReliabilityWeight | Jak mocno ocena silnika przesuwa niezawodność auta | 
ProgressPerSeason | Roczny postęp silnika dostawcy | pkt
TyrePartnerBonus | Przewaga opony dostrojonej pod partnera | 
```

> **W grze:** zespół fabryczny powinien mieć wyraźną przewagę nad klientami tego samego silnika. Jeśli klient jest równie szybki, opóźnienie albo wsparcie są za małe.

> **Stan:** działa w kodzie (T43). Własny program silnikowy (PP-019) czeka na [#164](https://github.com/DatJikun/paddock-principal/issues/164).

---

## 11. Pieniądze i popularność

### Jak to działa

Każdy zespół ma księgę: każdy przychód i koszt to osobny wpis, a saldo to ich suma. Kwoty są w dolarach z epoki (PP-050).

**Przychody w latach 50.:** pieniądze startowe od organizatorów (za każdy start) i nagrody za miejsca. Od 1968 dochodzą sponsorzy, potem pieniądze z TV. Model przychodów zmienia się z epoką, bo to wpis osi czasu.

**Koszty:** pensje (1. dnia miesiąca), budowa i rozwój auta (co tydzień), dostawy, wyjazdy na wyścigi.

**Saldo może spaść poniżej zera.** Na powrót masz cały sezon. Dopiero po roku pod kreską pada niewypłacalność.

**Popularność sportu** rośnie z wyrównaną walką o tytuł (wielu zwycięzców, mała różnica punktów), a spada przy dominacji jednego zespołu. Od popularności zależą pule pieniędzy, więc pensje i budżety rosną razem ze sportem, a nie z automatyczną inflacją (PP-025).

### Co decydujesz

Na co wydajesz. Gra pokazuje gotówkę, zobowiązania i prognozę do końca sezonu dla Twojego zespołu. Finansów rywali nie znasz.

```strojenie src/Paddock.Domain/Finance/FinanceEstimates.cs
StartMoneyShare | Pieniądze startowe za sezon jako część typowego budżetu | %
PrizeMoneyShare | Nagrody za sezon jako część typowego budżetu | %
RaceRunningShare | Koszt wyjazdów na wyścigi jako część typowego budżetu | %
PrizePositions | Ile miejsc dostaje nagrodę | 
CloseFightGain | Wzrost popularności po wyrównanym sezonie | 
DominanceLoss | Spadek popularności po sezonie dominacji | 
MinPopularityMilli | Najniższa popularność (tysięczne, 1000 = start) | 
MaxPopularityMilli | Najwyższa popularność (tysięczne) | 
```

> **W grze:** mały zespół w 1955 powinien ledwo wiązać koniec z końcem, a dobry wynik w wyścigu powinien być odczuwalny w kasie. Jeśli pieniądze nie mają znaczenia, budżety albo nagrody są za wysokie.

> **Stan:** działa w kodzie (T37). Kredyty (PP-048) są na razie pustym miejscem w kodzie. Podłączanie do kariery: [#172](https://github.com/DatJikun/paddock-principal/pull/172).

---

## 12. Sponsorzy

### Jak to działa

Każdy zespół ma trzy miejsca na sponsora, a na każde miejsce jest kilku kandydatów (PP-050). Do 1967 to miejsca techniczne lub mecenasa, od 1968 główne, dodatkowe i techniczne. Tytoń i alkohol pojawiają się i znikają zgodnie z przepisami epoki. Sponsorzy są fikcyjni.

**Rozmowa to gra w czekanie.** Sponsor zaczyna od niższej kwoty, a z każdym dniem rozmów warunki się poprawiają, do limitu zależnego od Twojego negocjatora (dyrektor komercyjny albo szef zespołu). Ryzyko: z tym samym sponsorem może rozmawiać rywal i podpisać go przed Tobą. Dobry negocjator wie, że rywal jest przy stole.

```wykres sponsor-czekanie
Czekanie podnosi warunki, ale każdy dzień to szansa, że rywal podpisze pierwszy. Przerywana linia pokazuje, ile średnio zdobędziesz, czekając tyle dni, jeśli nie wiesz, czy rywal jest przy stole.
```

**Uwaga do strojenia:** przy obecnych liczbach czekanie **średnio się nie opłaca**: przerywana linia od razu spada. Czekać warto tylko wtedy, gdy dobry negocjator widzi, że rywala nie ma. Jeśli dylemat ma być prawdziwy także bez tej wiedzy, trzeba zmniejszyć `RivalSignChance` albo zwiększyć `WaitingGainMilliPerDay`.

Umowa trwa około sezonu i płaci miesięczne raty. Sponsor ma cel (np. podium, kierowca danej narodowości). Spełniony cel daje premię i zaufanie, a niespełniony obniża zaufanie i może skończyć się odejściem. Zadowolony sponsor sam proponuje przedłużenie.

### Co decydujesz

Z kim rozmawiasz, kiedy podpisujesz i kiedy odchodzisz od stołu.

```strojenie src/Paddock.Domain/Sponsors/SponsorEstimates.cs
OpeningTermsMilli | Warunki na otwarcie rozmów (część pełnej ceny) | m%
WaitingGainMilliPerDay | O ile poprawiają się warunki z każdym dniem | m%
BaseCapMilli | Limit poprawy przy negocjatorze bez umiejętności | m%
CapMilliPerSkill | O ile każdy punkt negocjatora podnosi limit | m%
RivalPresenceChance | Szansa, że rywal rozmawia z tym samym sponsorem | %
RivalSignChance | Szansa dziennie, że rywal podpisze | %
RivalInsightSkill | Od tej umiejętności negocjator widzi rywala przy stole | 
DealDays | Długość umowy | dni
BonusMilli | Premia za spełniony cel (część rocznej kwoty) | m%
TrustOnMet | Zaufanie za spełniony cel | 
TrustOnFailed | Utrata zaufania za niespełniony cel | 
RenewalMinTrust | Poniżej tego zaufania sponsor nie proponuje przedłużenia | 
SecondarySlotMilli | Ile płaci miejsce dodatkowe względem głównego | m%
```

> **W grze:** decyzja „podpisać czy czekać” powinna być prawdziwym dylematem. Jeśli zawsze opłaca się czekać do końca albo zawsze podpisać od razu, popraw `RivalSignChance` albo `WaitingGainMilliPerDay`.

> **Stan:** działa w kodzie (T38). Pakiet sponsora założycielskiego jest po MVP.

---

## 13. Zarząd, reputacja i zwolnienia

### Jak to działa

**Reputacja menedżera** (0–100) rośnie, gdy wyniki są lepsze od oczekiwań, i przy tytułach, a spada przy złym zarządzaniu pieniędzmi i po zwolnieniu. Wpływa na oferty pracy i na prestiż zespołu w oczach kierowców.

**Zarząd** każdego zespołu ma cierpliwość (starszy zespół jest cierpliwszy) i zaufanie do szefa. Stawia cel na sezon i cel na kilka lat, oparte na publicznych faktach (zeszłoroczna pozycja, ranga budżetu). Po każdym wyścigu zaufanie zbliża się do poziomu wynikającego z pozycji względem oczekiwań. Ujemna albo szybko topniejąca gotówka je obniża.

**Zwolnienie:** szef, którego zaufanie jest poniżej progu przez kilka ocen z rzędu, traci pracę. Nowy szef ma ochronę przez pierwszy pełny sezon, dłużej przy wysokiej reputacji. Zwolniony gracz ogląda świat jako obserwator i dostaje oferty pracy. Po pewnym czasie zawsze przychodzi jakaś oferta, więc nie utkniesz.

```wykres zarzad-prog
Cierpliwy zarząd toleruje niższe zaufanie i czeka dłużej. Nowy, niecierpliwy zespół zwolni szybciej.
```

```strojenie src/Paddock.Domain/Board/BoardEstimates.cs
InitialReputationTenths | Reputacja nowego menedżera | t
InitialConfidenceTenths | Zaufanie zarządu na start | t
ReviewSmoothing | Jaką część różnicy do celu zaufanie nadrabia po wyścigu | %
DismissBelowAtPatience50Tenths | Próg zaufania przy przeciętnej cierpliwości | t
ReviewsToDismissBase | Ile ocen z rzędu poniżej progu zwalnia (u najmniej cierpliwych) | 
PatienceBase | Cierpliwość zarządu bez historii (plus wiek zespołu) | 
ProtectionDaysPerReputationPoint | Dodatkowe dni ochrony za punkt reputacji | dni
SeasonObjectiveTenths | Zmiana zaufania za cel sezonowy | t
TitleTenths | Premia reputacji za tytuł konstruktorów | t
DismissalTenths | Utrata reputacji za zwolnienie | t
GuaranteeWindowDays | Najdłużej bez żadnej oferty pracy | dni
```

> **W grze:** przegrany sezon nie powinien od razu kończyć pracy, ale trzy złe sezony z rzędu już tak. Jeśli zarząd jest zbyt nerwowy albo zbyt pobłażliwy, zgłoś to.

> **Stan:** działa w kodzie (T45). Właściciele i zarządy nie mają atrybutów (PP-050).

---

## 14. Weekend wyścigowy

### Kwalifikacje

Format zależy od epoki: od jednej sesji z najlepszym czasem po trzystopniowe Q1–Q3 i prekwalifikacje z lat 1988–1992. Zasada 107% obowiązuje tam, gdzie obowiązywała naprawdę. Tor przyspiesza w trakcie weekendu, więc późniejsze okrążenia są trochę szybsze.

### Czas okrążenia

Czas okrążenia składa się z warstw: baza toru, dopasowanie auta do toru, kierowca, paliwo, opony, ruch i brudne powietrze, mokry tor, szum. Każda warstwa zależy od epoki.

**Baza toru:** długość toru podzielona przez typową prędkość epoki, poprawioną o charakter toru (uliczny wolniej, owal szybciej).

```wykres predkosc-epoki
Prędkość na neutralnym torze. Tor uliczny, techniczny albo szybki przesuwa ją w dół lub w górę.
```

**Dopasowanie auta:** każdy fragment toru nagradza inny parametr auta. Na prostych liczy się moc, w szybkich zakrętach docisk, w wolnych przyczepność mechaniczna, w strefach hamowania hamulce. Dlatego to samo auto jest na jednym torze z przodu, a na innym w środku stawki (PP-049).

```wykres dopasowanie-toru
```

**Auto kontra kierowca:** w latach 50. różnice między autami były ogromne, dziś są mniejsze, więc kierowca waży relatywnie więcej.

```wykres auto-kontra-kierowca
Porównanie dla okrążenia trwającego 100 s. Ocena kierowcy w wyścigu to atrybut razy 5, więc +10 punktów to +2 punkty atrybutu.
```

**Wyprzedzanie:** auto w brudnym powietrzu traci czas. Żeby wyprzedzić, potrzebuje przewagi tempa większej niż margines, który zależy od wyprzedzania atakującego i obrony broniącego.

```strojenie src/Paddock.Simulation/Racing/Pace/PaceConstants.cs
DriverSensitivity | Ile daje punkt tempa kierowcy (część czasu okrążenia) | 
MaxAffinitySeconds | Największe ukryte powinowactwo kierowcy do toru | s
FuelSecondsPerKg | Strata za każdy kilogram paliwa na okrążeniu | s
TrafficMaxLossSeconds | Strata tuż za innym autem | s
DirtyAirMaxLossSeconds | Strata w pełnym brudnym powietrzu | s
WetBaseFraction | Strata na mokrym torze na właściwych oponach (część okrążenia) | %
WetMismatchFraction | Dodatkowa strata na mokrym na złych oponach | %
NoiseSigmaAtZeroConsistency | Rozrzut czasów u kierowcy bez regularności | s
NoiseSigmaAtMaxConsistency | Rozrzut czasów u najbardziej regularnego kierowcy | s
```

```strojenie src/Paddock.Simulation/Racing/Weekend/WeekendConstants.cs
OvertakeMarginSeconds | Przewaga tempa potrzebna do wyprzedzenia | s
OvertakeSkillWeight | Jak mocno wyprzedzanie i obrona zmieniają ten margines | 
SafetyCarLapFactor | Okrążenie za samochodem bezpieczeństwa względem normalnego | 
MinorIncidentLossSeconds | Strata za drobny incydent (obrót, uszkodzenie) | s
StrategistCadenceLaps | Co ile okrążeń zespół pyta stratega | okr.
```

### Opony i paliwo

Opona traci czas z każdym okrążeniem, coraz szybciej, aż do „klifu”, po którym strata gwałtownie rośnie. Miękka opona jest szybsza na starcie, ale klif przychodzi wcześniej. W latach 50. była jedna twarda opona, która wytrzymywała cały wyścig. Płynny kierowca i mniej ścierny tor zużywają opony wolniej.

```wykres opony
Typowe warunki. Strata na starcie to różnica przyczepności między mieszankami.
```

Paliwo waży, więc pełny bak jest wolniejszy. Tankowanie w wyścigu jest dozwolone tylko w latach, w których było naprawdę.

```strojenie src/Paddock.Simulation/Racing/Tyres/TyreFuelConstants.cs
WearGrowthShare | Jak bardzo zużycie przyspiesza przed klifem | 
CliffDropSeconds | Nagła strata po przekroczeniu klifu | s
CliffSlopeMultiplier | Jak szybko rośnie strata po klifie | 
WarmUpPeakLossSeconds | Strata na zimnym, nowym komplecie | s
SmoothnessWearEffect | Jak mocno płynność kierowcy zmienia zużycie | %
SupplierGripSwingSeconds | Różnica przyczepności między dostawcami opon | s
FuelSavingPaceLossSeconds | Strata za oszczędzanie paliwa | s
```

### Pogoda

Każdy tor ma klimat (morski, kontynentalny, śródziemnomorski, pustynny, tropikalny) i skłonność do deszczu. Pogoda zmienia się minuta po minucie: deszcz narasta i słabnie, tor moknie i schnie (linia wyścigowa szybciej). Prognoza w boksie ma błąd, który zależy od jakości osoby od pogody i rośnie z odległością w czasie.

```wykres deszcz
Skalibrowane na prawdziwych wyścigach 1950–2025 (#122).
```

```strojenie src/Paddock.Simulation/Racing/Weather/WeatherConstants.cs
RaceRainProbabilityLow | Szansa deszczu w wyścigu: tor suchy | %
RaceRainProbabilityMedium | Szansa deszczu: tor umiarkowany | %
RaceRainProbabilityHigh | Szansa deszczu: tor deszczowy | %
BaseOffLineDryingPerMinute | Jak szybko schnie tor (poza linią) | 
RacingLineDryingFactor | Ile razy szybciej schnie linia wyścigowa | 
PoorForecasterScale | Błąd prognozy najsłabszej osoby od pogody (mnożnik) | 
GoodForecasterScale | Błąd prognozy najlepszej osoby od pogody (mnożnik) | 
```

### Awarie

Każde auto ma ryzyko awarii na okrążenie, które zależy od epoki, niezawodności auta, przebiegu części, tempa, upału i płynności kierowcy. Część awarii daje ostrzeżenie kilka okrążeń wcześniej (spadek tempa). Awaria może skończyć wyścig, odebrać moc albo wymusić postój na naprawę.

```wykres słupki
tytuł: Odsetek startujących, którzy odpadli przez awarię (cel kalibracji z prawdziwych wyników)
format: %
max: 50
1950–59 | 43
1960–69 | 37
1970–79 | 33
1980–93 | 36
1994–2009 | 22
od 2010 | 8
opis: Gra dostraja ryzyko awarii tak, żeby trafić w te odsetki (#122). W latach 50. awaryjność decydowała o połowie wyników.
```

```strojenie src/Paddock.Simulation/Racing/Reliability/ReliabilityConstants.cs
RatingEffect | Jak mocno niezawodność auta obniża ryzyko awarii | %
AgeSlope | Jak szybko ryzyko rośnie z przebiegiem części | 
SympathyStressWeight | Jak bardzo niepłynny kierowca psuje auto | 
DefaultWarningLeadLaps | Ile okrążeń wcześniej awaria daje ostrzeżenie | okr.
RepairMinSeconds | Najkrótsza naprawa w boksie | s
RepairMaxSeconds | Najdłuższa naprawa w boksie | s
```

### Incydenty

Ryzyko incydentu zależy od kierowcy (agresja zwiększa, opanowanie zmniejsza), liczby aut w pobliżu, mokrego toru, niebezpieczeństwa toru i epoki. Pierwsze okrążenie to chaos. Skutki: od obrotu i straty czasu, przez odpadnięcie, po kontuzję. Śmierć tylko przy włączonej opcji (PP-006). Samochód bezpieczeństwa, wirtualny samochód bezpieczeństwa i czerwona flaga pojawiają się tylko w epokach, w których istniały.

```wykres incydenty
```

```strojenie src/Paddock.Simulation/Racing/Incidents/IncidentConstants.cs
BaseIncidentRatePerCarLap | Bazowa szansa incydentu na okrążenie | %
FirstLapFactor | Ile razy groźniejsze jest pierwsze okrążenie | 
SecondLapFactor | Ile razy groźniejsze jest drugie okrążenie | 
WetnessWeight | Jak bardzo mokry tor podnosi ryzyko | 
ContactDensityWeight | O ile rośnie ryzyko za każde auto w pobliżu | 
SafetyCarRetirementLaps | Okrążenia za samochodem bezpieczeństwa po odpadnięciu auta | okr.
```

### Postoje i strateg

Postój kosztuje czas w alei serwisowej plus obsługę. Szybkość i błędy zależą od jakości i liczby mechaników. Strateg (zatrudniony człowiek, PP-029) wybiera plan postojów, opony i tempo. Słaby strateg gorzej ocenia zużycie opon i plany, więc częściej się myli.

```wykres mechanicy
```

```strojenie src/Paddock.Simulation/Racing/Pits/PitConstants.cs
FaultProbabilityAtQuality0 | Szansa wolnego postoju albo błędu przy najsłabszej ekipie | %
FaultProbabilityAtQuality100 | To samo przy najlepszej ekipie | %
ErrorMinSeconds | Najmniejsza strata przy błędzie (np. zablokowana nakrętka) | s
ErrorMaxSeconds | Największa strata przy błędzie | s
PushPaceGainSeconds | Zysk na okrążeniu w trybie ataku | s
PushWearFactor | Ile razy szybciej zużywają się opony w trybie ataku | 
PlanNoiseSdSecondsAtSkill0 | Jak bardzo myli się najsłabszy strateg w ocenie planu | s
WearBeliefSdAtSkill0 | Jak bardzo najsłabszy strateg myli się w zużyciu opon | 
DriverFatigueOnsetLaps | Po ilu okrążeniach kierowca zaczyna się męczyć | okr.
```

> **W grze:** wyścig z 1955 i z 1988 mają się wyraźnie różnić (bramka fazy 3). Jeśli wyprzedzania jest za dużo albo za mało, zgłoś `OvertakeMarginSeconds`. Jeśli co wyścig ktoś ma kraksę na pierwszym okrążeniu, zgłoś `FirstLapFactor`.

> **Stan:** działa w kodzie (silnik okrążeniowy, wszystkie warstwy, relacja z wyścigu, kalibracja #122). Wyścig na żywo z mapą jest w prototypie. Ciągła symulacja pozycji to plan po MVP (PP-052).

---

## 15. Tory: geometria, tworzenie i edycja

### Jak to działa

Każdy układ toru to jeden plik z **punktami kontrolnymi**: zamkniętą pętlą punktów w metrach (PP-048, PP-049). Gra przeprowadza przez nie gładką krzywą i skaluje ją do prawdziwej długości toru. Z tej samej krzywej korzystają symulacja i UI, więc mapa w grze i tor w symulacji to zawsze ten sam kształt.

Tory mają **wersje układu**: Monza z owalem i bez, stara i nowa Spa to osobne układy z własną długością i charakterem. Każdy wyścig kalendarza jest przypisany do układu.

**Brak pliku nie psuje gry:** wtedy gra i UI rysują zastępczy owal o właściwej długości.

### Gdzie co leży

| Plik | Co w nim jest |
|---|---|
| `data/authored/tracks/circuits.json` | tory i ich układy: lata, długość, charakter (np. uliczny, szybki), przybliżony profil |
| `data/authored/tracks/race_layout_map.json` | który wyścig jechano na którym układzie |
| `data/authored/tracks/geometry/<układ>.json` | punkty kontrolne układu i nazwy zakrętów |

### Jak poprawić istniejący tor

1. Otwórz plik układu w edytorze torów (PR [#146](https://github.com/DatJikun/paddock-principal/pull/146): wczytaj JSON, przesuń punkty, zapisz) albo zmień punkty ręcznie.
2. Uruchom walidację. Sprawdza, czy długość się zgadza, czy tor się nie przecina i nie ma ostrych załamań, czy punkty nie leżą za blisko siebie i czy nazwy zakrętów są poprawne.
3. Przebuduj kształty dla UI: `node ui/prototype/tools/build-track-geometry.mjs`.
4. Obejrzyj wszystkie tory naraz na stronie `ui/prototype/track-preview.html`.

Szczegóły i dokładne komendy: TECH §6.5.

### Jak dodać nowy tor

1. Dopisz tor albo nowy układ do `circuits.json` (id, lata, długość, charakter).
2. Przypisz wyścigi do układu w `race_layout_map.json`.
3. Narysuj punkty kontrolne w edytorze i zapisz jako `geometry/<id układu>.json`. Punkt 0 leży na linii mety.
4. Dodaj nazwy zakrętów z epoki (np. Gazomètre w Monako 1950, a nie późniejsza Rascasse).
5. Walidacja i przebudowa jak wyżej.

Dziś pliki mają tory kalendarza 1955 i Monza 1972. Kształty są przybliżone, rysowane z ogólnej wiedzy. Za dobry uznałeś na razie tylko Indianapolis, resztę poprawia się w tych samych plikach. Obrazów i śladów z zewnętrznych źródeł nie commitujemy (PP-041).

> **W grze:** sylwetka toru na mapie powinna być od razu rozpoznawalna. Jeśli nie jest, wskaż tor i zakręt.

> **Stan:** działa w kodzie (#170). Symulacja czasu okrążenia z krzywizny toru (prędkość w zakrętach z fizyki auta) jest w toku ([#138](https://github.com/DatJikun/paddock-principal/issues/138)).

---

## 16. Dane świata i edycja bazy

### Jak to działa

Świat gry powstaje z dwóch źródeł:

- **Prawdziwe wyniki z Jolpica-F1** (kierowcy, zespoły, tory, wyniki od 1950). Te dane mają licencję niekomercyjną, więc nie trafiają do repozytorium (PP-041). Każdy pobiera je u siebie, a gra buduje z nich lokalną bazę.
- **Pliki autorskie** w `data/authored/`: napisane przez nas, więc mogą być w repozytorium.

| Katalog | Co steruje grą |
|---|---|
| `regulations/` | przepisy rok po roku: punktacja, kwalifikacje, tankowanie, opony, bezpieczeństwo (41 wymiarów, 1950–2026) |
| `eras/` | oś czasu epok: budżety, model przychodów, inflacja |
| `tracks/` | tory, układy, geometria (rozdział 15) |
| `teams/` | dostawcy silników, ciągłość zespołów po zmianie nazwy, założyciele |
| `people/` | personel techniczny i jego role w sezonach |
| `tech/` | katalog technologii z najwcześniejszym realnym rokiem |
| `commercial/` | fikcyjni sponsorzy |
| `weather/` | klimat torów i pogoda prawdziwych wyścigów |
| `safety/` | bezpieczeństwo epok i historia wypadków |
| `names/` | pule imion i nazwisk według narodowości dla fikcyjnych ludzi |
| `events/` | zdarzenia z historii zespołów |
| `ratings/` | rankingi ekspertów do sprawdzania modelu ocen |

### Jak dziś zmienić coś w bazie

Edytora bazy jeszcze nie ma (faza 7+). Dziś zmianę robi Claude na Twoją prośbę (PP-051): np. „w 1976 Tyrrell ma mocniejsze auto” albo „dodaj fikcyjnego sponsora z Polski”. Każda zmiana pliku przechodzi walidację (`validate-authored`), która pilnuje spójności, np. czy każde miejsce sponsora ma co najmniej trzech kandydatów.

Zapis gry pamięta wersję bazy. Kariery nie da się wczytać na zmienionej bazie bez jawnej migracji, więc zmiana danych nie zepsuje po cichu trwającej kariery.

> **Stan:** działa w kodzie (pipeline, loadery, walidator). Edytor bazy i własne scenariusze startowe (PP-051) przyjdą później.

---

## 17. Inne zespoły (AI) i historia

### Jak to działa

AI używa tych samych zasad co Ty (filar 5). Nie widzi ukrytych atrybutów i nie zna przyszłości (D-010). Każda ważna decyzja AI zostawia ślad z uzasadnieniem (Paddock Spy), więc da się sprawdzić, dlaczego rywal zrobił to, co zrobił.

**Co AI robi już dziś:**

- **Inżynierowie każdego zespołu** wybierają projekty rozwojowe tą samą logiką co u Ciebie (rozdział 9).
- **Kierowcy i personel** oceniają oferty wszystkich zespołów według osobowości (rozdział 7).
- **Rywale przy sponsorach** mogą podpisać sponsora przed Tobą (rozdział 12).
- **Zarządy** oceniają swoich szefów AI i zwalniają ich. Nowy szef dostaje inny archetyp, więc zespół widocznie zmienia kierunek (rozdział 13).
- **Dostawcy** odpowiadają na propozycje umów (rozdział 10).

**Co jest zaprojektowane:**

- **Szefowie zespołów AI** z archetypami: *Pretendent* (wszystko na teraz), *Budowniczy* (długi plan), *Oportunista* (wybrane okazje), *Przetrwanie* (budżet, kierowcy z pieniędzmi). Transfery, priorytety i poświęcanie sezonu dla przyszłego auta. Issue [#109](https://github.com/DatJikun/paddock-principal/issues/109).
- **Propozycje historyczne:** prawdziwe zdarzenia (powstanie zespołu, transfer, wejście producenta) jako opcje z warunkami sensowności, a nie wymuszenia. Przy wysokiej sile historii AI chętniej je wybiera. Issue [#111](https://github.com/DatJikun/paddock-principal/issues/111).
- **Upadki, wykupy i powstawanie zespołów** w trakcie kariery.

> **W grze:** rywale powinni zachowywać się jak ludzie z charakterem: jeden zespół zbiera kierowców z pieniędzmi, inny inwestuje w przyszłość. Jeśli AI wydaje się bezmyślne albo wszechwiedzące, zgłoś konkretną sytuację.

> **Stan:** częściowo w kodzie (patrz wyżej). Szefowie AI i cykl życia zespołów czekają na swoje zadania.

---

## 18. Prawda i wiedza: co widzisz, a czego nie

Gra rozdziela **prawdę** (to, co liczy symulacja) od **wiedzy** (to, co wie Twój zespół). To niezmiennik całej gry (INV-003).

- Atrybuty cudzych kierowców widzisz jako pasma, a nie dokładne liczby.
- Sufit koncepcji auta i zysk z projektu to zawsze przedziały, a ich szerokość zależy od jakości Twoich ludzi.
- Finansów, umów i aut rywali nie znasz. Dobry negocjator wie tylko, że rywal rozmawia ze sponsorem.
- Prognoza pogody ma błąd.
- Wyniki, czasy okrążeń i klasyfikacje są zawsze widoczne, bo to fakty publiczne.

**Tryb bez liczb** (PP-031) pokazuje tę samą wiedzę słowami Twoich ludzi zamiast pasm: „młody jest szybki, ale zjada opony”. Słaby inżynier może się mylić.

> **W grze:** nigdy nie powinieneś zobaczyć dokładnej ukrytej wartości. Jeśli gdzieś ją widzisz, to błąd.

---

## 19. Czego jeszcze nie ma

Aktualna lista prac jest w [ROADMAP.md](ROADMAP.md) i w otwartych issues. Najważniejsze braki, które zobaczysz podczas gry:

- pętla kariery jeszcze nie woła finansów, sponsorów, aut i zarządu ([#172](https://github.com/DatJikun/paddock-principal/pull/172)),
- grywalny sezon w konsoli ([#112](https://github.com/DatJikun/paddock-principal/issues/112)) i bramka „sezon 1955 od A do Z” ([#113](https://github.com/DatJikun/paddock-principal/issues/113)),
- szefowie zespołów AI ([#109](https://github.com/DatJikun/paddock-principal/issues/109)) i cykl życia zespołów ([#111](https://github.com/DatJikun/paddock-principal/issues/111)),
- czas okrążenia z geometrii toru ([#138](https://github.com/DatJikun/paddock-principal/issues/138)),
- własny program silnikowy ([#164](https://github.com/DatJikun/paddock-principal/issues/164)),
- przełomy technologiczne od ludzi (PP-042), kronika rozbieżności, Hall of Fame, Paddock Monthly,
- prawdziwe UI (faza 6) i multiplayer (PP-045).
